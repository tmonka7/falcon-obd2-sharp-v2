using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using RedlineDiagnostics.Database;
using RedlineDiagnostics.Obd;

namespace RedlineDiagnostics.Diagnostics
{
    /// <summary>
    /// Runs a full system scan on a background thread: for every control module it probes the ECU,
    /// reads stored / pending / permanent trouble codes (J1979 and UDS), identifies the ECU and
    /// samples its live-data channels. Progress is reported through events posted to the UI context.
    /// </summary>
    public sealed class ScanEngine
    {
        private readonly Elm327Adapter _adapter;
        private readonly IList<ControlModule> _modules;
        private readonly SynchronizationContext _ui;
        private readonly Stopwatch _clock = new Stopwatch();
        private volatile bool _cancel;
        private Thread _thread;

        public event Action<ControlModule> ModuleChanged;
        public event Action ProgressChanged;
        public event Action<ScanResult> Completed;

        public bool IsRunning { get; private set; }
        public ControlModule Current { get; private set; }
        public int CompletedCount { get; private set; }
        public int Total => _modules.Count;
        public TimeSpan Elapsed => _clock.Elapsed;
        public ScanResult Result { get; private set; }
        public int MinModuleMs { get; set; } = 1400;
        public string Vin = "";
        public string VehicleName = "";
        public string AdapterName = "";

        public ScanEngine(Elm327Adapter adapter, IList<ControlModule> modules, SynchronizationContext ui)
        {
            _adapter = adapter;
            _modules = modules;
            _ui = ui ?? new SynchronizationContext();
        }

        public void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
            _cancel = false;
            CompletedCount = 0;
            foreach (var m in _modules) m.Reset();
            _clock.Restart();
            _thread = new Thread(Run) { IsBackground = true, Name = "ScanEngine" };
            _thread.Start();
        }

        public void Cancel()
        {
            _cancel = true;
        }

        private void Post(Action a)
        {
            _ui.Post(_ => a(), null);
        }

        private void Raise(ControlModule m)
        {
            var h = ModuleChanged;
            if (h != null) Post(() => h(m));
        }

        private void Run()
        {
            var result = new ScanResult
            {
                Started = DateTime.Now,
                Vin = Vin,
                VehicleName = VehicleName,
                Adapter = AdapterName,
                Protocol = ObdProtocolInfo.ShortName(_adapter.Protocol)
            };
            bool transportDead = false;

            foreach (var m in _modules)
            {
                if (_cancel || transportDead) break;
                Current = m;
                m.Reset();
                m.Status = ModuleStatus.Scanning;
                m.StartedAt = DateTime.Now;
                m.ProtocolName = ObdProtocolInfo.ShortName(_adapter.Protocol);
                Raise(m);
                var sw = Stopwatch.StartNew();
                try
                {
                    ScanModule(m);
                }
                catch (ObdTransportException ex)
                {
                    m.Status = ModuleStatus.NoResponse;
                    m.Error = ex.Message;
                    if (!_adapter.Transport.IsOpen) transportDead = true;
                }
                catch (Exception ex)
                {
                    m.Status = ModuleStatus.NoResponse;
                    m.Error = ex.Message;
                }

                // Pace the visualisation so each module is visibly "scanned".
                int remaining = MinModuleMs - (int)sw.ElapsedMilliseconds;
                var finalStatus = m.Status;
                if (remaining > 0 && !_cancel)
                {
                    m.Status = ModuleStatus.Scanning;
                    int startP = m.Progress;
                    var t0 = sw.ElapsedMilliseconds;
                    while (sw.ElapsedMilliseconds - t0 < remaining && !_cancel)
                    {
                        float f = (sw.ElapsedMilliseconds - t0) / (float)remaining;
                        m.Progress = startP + (int)((100 - startP) * f);
                        Raise(m);
                        Thread.Sleep(60);
                    }
                }
                m.Progress = 100;
                m.Status = finalStatus;
                m.Elapsed = sw.Elapsed;
                CompletedCount++;
                Raise(m);
                var ph = ProgressChanged;
                if (ph != null) Post(ph);
            }

            try { _adapter.ClearTarget(); } catch { }

            result.Ended = DateTime.Now;
            result.Cancelled = _cancel;
            foreach (var m in _modules)
            {
                if (!m.IsDone) continue;
                var mr = new ModuleResult
                {
                    Short = m.Short,
                    Name = m.Name,
                    Status = (int)m.Status,
                    ResponseMs = m.ResponseTimeMs,
                    EcuId = m.EcuId,
                    Address = m.Address
                };
                foreach (var d in m.Dtcs)
                    mr.Dtcs.Add(new DtcEntry { Code = d.Code, State = (int)d.State, Description = DtcDatabase.Describe(d.Code) });
                result.Modules.Add(mr);
                result.Scanned++;
                if (m.Status == ModuleStatus.Fault) result.Faults++;
                else if (m.Status == ModuleStatus.Warning) result.Warnings++;
                else if (m.Status == ModuleStatus.NoResponse) result.NoResponse++;
            }
            Result = result;
            Current = null;
            IsRunning = false;
            _clock.Stop();
            var ch = Completed;
            if (ch != null) Post(() => ch(result));
        }

        private void Step(ControlModule m, int progress)
        {
            m.Progress = progress;
            Raise(m);
        }

        private void ScanModule(ControlModule m)
        {
            var def = m.Definition;
            _adapter.SetTarget(def.RequestId, def.ResponseId);
            Step(m, 8);

            // ---- 1. Probe ----
            var t0 = Stopwatch.StartNew();
            bool present = false, obd = false;
            ObdResponse udsDtc = null;
            var r = _adapter.Request("0100", 1500);
            if (r.AnyPositive) { present = true; obd = true; }
            else if (r.AnyNegative) present = true;
            if (!present)
            {
                r = _adapter.Request("3E00", 1200);
                if (r.AnyPositive || r.AnyNegative) present = true;
            }
            if (!present)
            {
                r = _adapter.Request("1902FF", 1500);
                if (r.AnyPositive || r.AnyNegative) { present = true; udsDtc = r; }
            }
            m.ResponseTimeMs = (int)t0.ElapsedMilliseconds;
            if (!present)
            {
                m.Status = ModuleStatus.NoResponse;
                m.Progress = 100;
                return;
            }
            Step(m, 20);
            if (_cancel) return;

            // ---- 2. ECU identification ----
            m.EcuId = ReadAscii("22F187", 0x62, 2);
            if (string.IsNullOrEmpty(m.EcuId) && obd)
            {
                var name = ReadAscii("090A", 0x49, 2);
                if (!string.IsNullOrEmpty(name)) m.EcuId = name;
            }
            if (string.IsNullOrEmpty(m.EcuId)) m.EcuId = def.ResponseId.ToString("X3") + "-" + def.RequestId.ToString("X3");
            Step(m, 30);
            if (_cancel) return;

            // ---- 3. Live data (early, so the diagnostic panel fills while the module is being scanned) ----
            var pars = LiveParam.For(m.Short);
            int idx = 0;
            foreach (var p in pars)
            {
                if (_cancel) return;
                var resp = _adapter.Request(p.Request, 1200);
                var payload = resp.Payload(p.ResponseService, p.HeaderBytes);
                var val = p.Decode(payload);
                if (val.HasValue) m.LiveValues[p.Key] = val.Value;
                idx++;
                m.Progress = 30 + (int)(35.0 * idx / Math.Max(1, pars.Length));
                Raise(m);
            }
            Step(m, 66);
            if (_cancel) return;

            // ---- 4. Trouble codes ----
            if (obd)
            {
                ReadJ1979Dtcs(m, "03", 0x43, DtcState.Stored);
                Step(m, 74);
                ReadJ1979Dtcs(m, "07", 0x47, DtcState.Pending);
                Step(m, 82);
                ReadJ1979Dtcs(m, "0A", 0x4A, DtcState.Permanent);
            }
            if (udsDtc == null) udsDtc = _adapter.Request("1902FF", 1500);
            if (udsDtc.AnyPositive)
            {
                var payload = udsDtc.Payload(0x59, 1);
                foreach (var rec in DtcParser.ParseUds(payload, m.Short))
                    if (!m.Dtcs.Any(d => d.Code == rec.Code)) m.Dtcs.Add(rec);
            }
            Step(m, 92);

            // ---- 5. Verdict ----
            if (m.Dtcs.Any(d => d.State != DtcState.Pending)) m.Status = ModuleStatus.Fault;
            else if (m.Dtcs.Count > 0) m.Status = ModuleStatus.Warning;
            else m.Status = ModuleStatus.Passed;
        }

        private void ReadJ1979Dtcs(ControlModule m, string cmd, byte service, DtcState state)
        {
            var r = _adapter.Request(cmd, 2000);
            var payload = r.Payload(service, 0);
            if (payload == null) return;
            foreach (var code in DtcParser.ParseJ1979(payload))
                if (!m.Dtcs.Any(d => d.Code == code)) m.Dtcs.Add(new DtcRecord { Code = code, State = state, ModuleId = m.Short });
        }

        private string ReadAscii(string cmd, byte service, int headerBytes)
        {
            try
            {
                var r = _adapter.Request(cmd, 2000);
                var payload = r.Payload(service, headerBytes);
                if (payload == null || payload.Length == 0) return "";
                int start = 0;
                if (payload[0] == 0x01 && payload.Length > 1) start = 1; // message count byte (mode 09)
                var sb = new StringBuilder();
                for (int i = start; i < payload.Length; i++)
                {
                    char c = (char)payload[i];
                    if (c >= 0x20 && c < 0x7F) sb.Append(c);
                }
                return sb.ToString().Trim();
            }
            catch (ObdTransportException) { throw; }
            catch { return ""; }
        }
    }
}
