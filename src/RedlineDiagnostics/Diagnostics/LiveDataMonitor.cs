using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RedlineDiagnostics.Database;
using RedlineDiagnostics.Obd;

namespace RedlineDiagnostics.Diagnostics
{
    /// <summary>Polls a set of live parameters on a background thread and keeps a short history per channel.</summary>
    public sealed class LiveDataMonitor : IDisposable
    {
        private readonly Elm327Adapter _adapter;
        private readonly LiveParam[] _params;
        private readonly ModuleDefinition _target;
        private readonly SynchronizationContext _ui;
        private readonly Dictionary<string, List<double>> _history = new Dictionary<string, List<double>>();
        private readonly object _lock = new object();
        private Thread _thread;
        private volatile bool _stop;

        public event Action<Dictionary<string, double>> Sample;
        public event Action<string> Failed;

        public int IntervalMs { get; set; } = 500;
        public int MaxHistory { get; set; } = 120;
        public bool IsRunning { get; private set; }
        public DateTime LastSampleAt { get; private set; }
        public int SamplesPerSecondX10 { get; private set; }

        public LiveDataMonitor(Elm327Adapter adapter, LiveParam[] parameters, ModuleDefinition target, SynchronizationContext ui)
        {
            _adapter = adapter;
            _params = parameters;
            _target = target;
            _ui = ui ?? new SynchronizationContext();
        }

        public void Start()
        {
            if (IsRunning) return;
            _stop = false;
            IsRunning = true;
            _thread = new Thread(Run) { IsBackground = true, Name = "LiveDataMonitor" };
            _thread.Start();
        }

        public void Stop()
        {
            _stop = true;
            var t = _thread;
            if (t != null && t.IsAlive && Thread.CurrentThread != t) t.Join(3000);
            IsRunning = false;
        }

        public IReadOnlyList<double> History(string key)
        {
            lock (_lock)
            {
                List<double> l;
                return _history.TryGetValue(key, out l) ? l.ToArray() : new double[0];
            }
        }

        private void Run()
        {
            try
            {
                if (_target != null) _adapter.SetTarget(_target.RequestId, _target.ResponseId);
                else _adapter.ClearTarget();
                var sw = new Stopwatch();
                while (!_stop)
                {
                    sw.Restart();
                    var sample = new Dictionary<string, double>();
                    foreach (var p in _params)
                    {
                        if (_stop) break;
                        ObdResponse r;
                        try { r = _adapter.Request(p.Request, 1200); }
                        catch (ObdTransportException ex)
                        {
                            var fh = Failed;
                            if (fh != null) _ui.Post(_ => fh(ex.Message), null);
                            IsRunning = false;
                            return;
                        }
                        var payload = r.Payload(p.ResponseService, p.HeaderBytes);
                        var v = p.Decode(payload);
                        if (!v.HasValue) continue;
                        sample[p.Key] = v.Value;
                        lock (_lock)
                        {
                            List<double> l;
                            if (!_history.TryGetValue(p.Key, out l)) _history[p.Key] = l = new List<double>();
                            l.Add(v.Value);
                            if (l.Count > MaxHistory) l.RemoveAt(0);
                        }
                    }
                    LastSampleAt = DateTime.Now;
                    long ms = Math.Max(1, sw.ElapsedMilliseconds);
                    SamplesPerSecondX10 = (int)(10000 / ms);
                    var h = Sample;
                    if (h != null && sample.Count > 0) _ui.Post(_ => h(sample), null);
                    int wait = IntervalMs - (int)sw.ElapsedMilliseconds;
                    if (wait > 0) Thread.Sleep(Math.Min(wait, 1000));
                }
            }
            catch (Exception ex)
            {
                var fh = Failed;
                if (fh != null) _ui.Post(_ => fh(ex.Message), null);
            }
            finally
            {
                IsRunning = false;
                try { if (_target != null) _adapter.ClearTarget(); } catch { }
            }
        }

        public void Dispose() => Stop();
    }
}
