using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RedlineDiagnostics.Obd
{
    /// <summary>
    /// Drives an ELM327 / STN (OBDLink) command interpreter: initialisation, protocol selection,
    /// per-module addressing (AT SH / AT CRA) and request/response with ISO-TP reassembly.
    /// </summary>
    public sealed class Elm327Adapter : IDisposable
    {
        private readonly IObdTransport _transport;
        private readonly object _lock = new object();

        public string Version { get; private set; } = "";
        public ObdProtocol Protocol { get; private set; } = ObdProtocol.Auto;
        public bool HeadersOn { get; private set; } = true;
        public int Timeout { get; set; } = 2500;
        public int CurrentRequestId { get; private set; } = -1;
        public int CurrentResponseId { get; private set; } = -1;
        public bool IsStn { get; private set; }
        public IObdTransport Transport => _transport;

        /// <summary>Raised for every command/reply pair with the round-trip time in milliseconds.</summary>
        public event Action<string, string, int> Trace;

        public int LastRoundTripMs { get; private set; }

        public Elm327Adapter(IObdTransport transport)
        {
            _transport = transport;
        }

        public string Raw(string command, int timeoutMs = 0)
        {
            lock (_lock)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                string reply;
                try
                {
                    reply = _transport.SendCommand(command, timeoutMs > 0 ? timeoutMs : Timeout) ?? "";
                }
                catch (ObdTransportException)
                {
                    LastRoundTripMs = (int)sw.ElapsedMilliseconds;
                    Trace?.Invoke(command, "<timeout>", LastRoundTripMs);
                    throw;
                }
                LastRoundTripMs = (int)sw.ElapsedMilliseconds;
                var lines = reply.Replace("\n", "\r").Split(new[] { '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                if (lines.Count > 0 && string.Equals(lines[0], command, StringComparison.OrdinalIgnoreCase))
                    lines.RemoveAt(0);
                var cleaned = string.Join("\r", lines);
                Trace?.Invoke(command, cleaned, LastRoundTripMs);
                return cleaned;
            }
        }

        public void Initialize(ObdProtocol preferred)
        {
            _transport.Open();
            string rz;
            try { rz = Raw("ATZ", 5000); }
            catch (ObdTransportException)
            {
                // some adapters need a wake-up character first
                try { Raw("", 800); } catch { }
                rz = Raw("ATZ", 5000);
            }
            Version = rz.Split('\r').Select(l => l.Trim()).FirstOrDefault(l => l.IndexOf("ELM", StringComparison.OrdinalIgnoreCase) >= 0 || l.IndexOf("STN", StringComparison.OrdinalIgnoreCase) >= 0 || l.IndexOf("OBD", StringComparison.OrdinalIgnoreCase) >= 0) ?? rz.Trim();
            Raw("ATE0", 1500);
            Raw("ATL0", 1500);
            Raw("ATS0", 1500);
            Raw("ATH1", 1500);
            Raw("ATAT1", 1500);
            Raw("ATST32", 1500);
            HeadersOn = true;
            try
            {
                var sti = Raw("STI", 1200);
                IsStn = sti.IndexOf("STN", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { IsStn = false; }

            Raw("ATSP" + (int)preferred, 1500);
            Protocol = preferred;
            var probe = Request("0100", 12000);
            if (probe.Error && !probe.AnyPositive)
                throw new ObdTransportException("Vehicle not responding (" + probe.ErrorText + "). Check ignition and protocol.");
            DetectProtocol();
            ClearTarget();
        }

        public void DetectProtocol()
        {
            try
            {
                var dpn = Raw("ATDPN", 1500).Trim();
                if (dpn.Length > 0)
                {
                    var p = ObdProtocolInfo.FromElmDigit(dpn[dpn.Length - 1]);
                    if (p != ObdProtocol.Unknown) Protocol = p;
                }
            }
            catch { }
        }

        public double? ReadVoltage()
        {
            try
            {
                var v = Raw("ATRV", 1500).Trim().TrimEnd('V', 'v');
                double d;
                if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            }
            catch { }
            return null;
        }

        /// <summary>Address one control module (physical addressing).</summary>
        public void SetTarget(int requestId, int responseId)
        {
            if (requestId == CurrentRequestId && responseId == CurrentResponseId) return;
            if (ObdProtocolInfo.IsCan29(Protocol))
            {
                Raw("ATCP" + ((requestId >> 24) & 0xFF).ToString("X2"), 1200);
                Raw("ATSH" + (requestId & 0xFFFFFF).ToString("X6"), 1200);
                Raw("ATCRA" + responseId.ToString("X8"), 1200);
                Raw("ATFCSH" + (requestId & 0xFFFFFF).ToString("X6"), 1200);
            }
            else
            {
                Raw("ATSH" + requestId.ToString("X3"), 1200);
                Raw("ATCRA" + responseId.ToString("X3"), 1200);
                Raw("ATFCSH" + requestId.ToString("X3"), 1200);
            }
            Raw("ATFCSD300000", 1200);
            Raw("ATFCSM1", 1200);
            CurrentRequestId = requestId;
            CurrentResponseId = responseId;
        }

        /// <summary>Back to functional (broadcast) addressing.</summary>
        public void ClearTarget()
        {
            if (ObdProtocolInfo.IsCan29(Protocol))
            {
                Raw("ATCP18", 1200);
                Raw("ATSHDB33F1", 1200);
            }
            else if (ObdProtocolInfo.IsCan(Protocol))
            {
                Raw("ATSH7DF", 1200);
            }
            else
            {
                Raw("ATSH6810F1", 1200);
            }
            try { Raw("ATAR", 1200); } catch { }
            try { Raw("ATFCSM0", 1200); } catch { }
            CurrentRequestId = -1;
            CurrentResponseId = -1;
        }

        public ObdResponse Request(string hexCommand, int timeoutMs = 0)
        {
            var raw = Raw(hexCommand, timeoutMs);
            return ObdResponse.Parse(raw, Protocol, HeadersOn);
        }

        public ObdResponse Request(byte mode, int pid = -1, int timeoutMs = 0)
        {
            var sb = new StringBuilder();
            sb.Append(mode.ToString("X2"));
            if (pid >= 0) sb.Append(pid > 0xFF ? pid.ToString("X4") : pid.ToString("X2"));
            return Request(sb.ToString(), timeoutMs);
        }

        public void Close()
        {
            try { _transport.Close(); } catch { }
        }

        public void Dispose()
        {
            Close();
            _transport.Dispose();
        }
    }
}
