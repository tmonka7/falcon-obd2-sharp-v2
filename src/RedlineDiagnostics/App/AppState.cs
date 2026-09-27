using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using RedlineDiagnostics.Database;
using RedlineDiagnostics.Diagnostics;
using RedlineDiagnostics.Localization;
using RedlineDiagnostics.Obd;
using RedlineDiagnostics.Rendering3D;

namespace RedlineDiagnostics.App
{
    public enum ConnectionState { Disconnected, Connecting, Connected, Error }

    /// <summary>Application-wide state: settings, adapter connection, modules, scan engine, vehicles, history.</summary>
    public sealed class AppState
    {
        public static readonly AppState Instance = new AppState();

        public Settings Settings { get; private set; } = new Settings();
        public Elm327Adapter Adapter { get; private set; }
        public ConnectionState Connection { get; private set; } = ConnectionState.Disconnected;
        public string ConnectionError { get; private set; } = "";
        public List<ControlModule> Modules { get; } = new List<ControlModule>();
        public ScanEngine Scan { get; private set; }
        public ScanResult LastScan { get; private set; }
        public List<ScanResult> History { get; private set; } = new List<ScanResult>();
        public List<VehicleProfile> Vehicles { get; private set; } = new List<VehicleProfile>();
        public VehicleProfile ActiveVehicle { get; private set; }
        public string DetectedVin { get; private set; } = "";
        public VinInfo VinInfo { get; private set; }
        public double? BatteryVoltage { get; private set; }
        public Mesh VehicleMesh { get; private set; }
        public LiveDataMonitor Monitor { get; private set; }
        public SynchronizationContext Ui { get; set; }
        public List<string> TraceLog { get; } = new List<string>();

        /// <summary>Recent adapter round-trip times (ms), newest last. Drives the communication graph.</summary>
        public List<int> CommSamples { get; } = new List<int>();
        public DateTime LastCommAt { get; private set; }

        public event Action ConnectionChanged;
        public event Action VehicleChanged;
        public event Action ScanStateChanged;
        public event Action HistoryChanged;
        public event Action<ControlModule> ModuleChanged;
        public event Action ScanProgress;
        public event Action MeshChanged;

        private readonly object _commLock = new object();

        public bool IsConnected => Connection == ConnectionState.Connected && Adapter != null;
        public bool IsScanning => Scan != null && Scan.IsRunning;

        public void Initialize()
        {
            Settings = Settings.Load();
            Loc.Current = Loc.FromCode(Settings.Language);
            DtcDatabase.Load();
            PidDatabase.Load();
            ModuleCatalog.Load();
            VinDecoder.Load();
            Modules.Clear();
            foreach (var def in ModuleCatalog.All) Modules.Add(new ControlModule(def));
            Vehicles = VehicleStore.Load();
            if (Vehicles.Count == 0)
            {
                Vehicles.Add(new VehicleProfile { Vin = "JTNB4RBE0M3034567", Make = "Toyota", Model = "Camry", Year = 2021, Engine = "2.5L Hybrid" });
                VehicleStore.Save(Vehicles);
            }
            ActiveVehicle = Vehicles.FirstOrDefault(v => v.Id == Settings.ActiveVehicleId) ?? Vehicles.FirstOrDefault();
            History = HistoryStore.LoadAll();
            LastScan = History.FirstOrDefault();
            VehicleMesh = CarMeshFactory.CreateSedan();
            LoadMeshAsync(Settings.ModelPath);
        }

        /// <summary>Loads (and simplifies) a model on a worker thread; the procedural car shows until it is ready.</summary>
        public void LoadMeshAsync(string path)
        {
            var t = new Thread(() =>
            {
                Mesh mesh;
                string name, info;
                LoadMeshCore(path, out mesh, out name, out info);
                for (int i = 0; i < 100 && Ui == null; i++) Thread.Sleep(100);
                Post(() =>
                {
                    ActiveModelName = name;
                    ActiveModelInfo = info;
                    VehicleMesh = mesh ?? CarMeshFactory.CreateSedan();
                    MeshChanged?.Invoke();
                });
            }) { IsBackground = true, Name = "ModelLoader" };
            t.Start();
        }

        // ------------------------------------------------------------------ mesh

        /// <summary>Folder of models shipped next to the executable.</summary>
        public static string ModelsDirectory
        {
            get
            {
                var dir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
                return System.IO.Path.Combine(dir, "Models");
            }
        }

        /// <summary>Bundled model used when no custom path is configured (the concept car, else the low-poly sedan).</summary>
        public static string DefaultModelPath
        {
            get
            {
                foreach (var name in new[] { "CarConcept.glb", "sedan.obj" })
                {
                    var p = System.IO.Path.Combine(ModelsDirectory, name);
                    if (System.IO.File.Exists(p)) return p;
                }
                return "";
            }
        }

        public static string[] BundledModels()
        {
            try
            {
                if (!System.IO.Directory.Exists(ModelsDirectory)) return new string[0];
                var list = new List<string>();
                foreach (var f in System.IO.Directory.GetFiles(ModelsDirectory))
                {
                    var ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".obj" || ext == ".glb" || ext == ".gltf") list.Add(f);
                }
                list.Sort(StringComparer.OrdinalIgnoreCase);
                return list.ToArray();
            }
            catch { return new string[0]; }
        }

        /// <summary>Settings.ModelPath value that selects the procedural car instead of a file.</summary>
        public const string BuiltInModel = "builtin";

        public string ActiveModelName { get; private set; } = "";
        public string ActiveModelInfo { get; private set; } = "";

        public static bool IsModelFile(string path)
        {
            var ext = System.IO.Path.GetExtension(path ?? "").ToLowerInvariant();
            return ext == ".obj" || ext == ".glb" || ext == ".gltf";
        }

        public void LoadMesh(string path)
        {
            Mesh mesh;
            string name, info;
            LoadMeshCore(path, out mesh, out name, out info);
            ActiveModelName = name;
            ActiveModelInfo = info;
            VehicleMesh = mesh ?? CarMeshFactory.CreateSedan();
            MeshChanged?.Invoke();
        }

        private void LoadMeshCore(string path, out Mesh mesh, out string name, out string info)
        {
            mesh = null; name = ""; info = "";
            if (string.IsNullOrWhiteSpace(path)) path = DefaultModelPath;
            if (string.Equals(path, BuiltInModel, StringComparison.OrdinalIgnoreCase)) path = "";
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                mesh = ext == ".obj" ? ObjLoader.Load(path) : GltfLoader.Load(path);
                if (Settings.ModelFlip) mesh.FlipFrontBack();
                name = System.IO.Path.GetFileName(path);
                info = mesh.Vertices.Count + " vertices · " + mesh.Faces.Count + " faces · " + sw.ElapsedMilliseconds + " ms";
                Log("Model loaded: " + path + " (" + info + ")");
            }
            catch (Exception ex)
            {
                mesh = null;
                Log("Model load failed: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ vehicles

        public void SetActiveVehicle(VehicleProfile v)
        {
            ActiveVehicle = v;
            Settings.ActiveVehicleId = v?.Id ?? "";
            Settings.Save();
            VehicleChanged?.Invoke();
        }

        public void SaveVehicles()
        {
            VehicleStore.Save(Vehicles);
            VehicleChanged?.Invoke();
        }

        public string VehicleTitle
        {
            get
            {
                if (ActiveVehicle != null) return ActiveVehicle.DisplayName;
                if (VinInfo != null && VinInfo.Manufacturer.Length > 0) return VinInfo.Manufacturer + (VinInfo.Year > 0 ? " " + VinInfo.Year : "");
                return Loc.T("home.noVehicle");
            }
        }

        public string VehicleEngine => ActiveVehicle?.Engine ?? "";

        public string CurrentVin
        {
            get
            {
                if (!string.IsNullOrEmpty(DetectedVin)) return DetectedVin;
                return ActiveVehicle?.Vin ?? "";
            }
        }

        public string AdapterLabel => ObdProtocolInfo.AdapterShortName(Settings.Adapter);

        // ------------------------------------------------------------------ connection

        public void ConnectAsync()
        {
            if (Connection == ConnectionState.Connecting || IsScanning) return;
            Disconnect();
            Connection = ConnectionState.Connecting;
            ConnectionError = "";
            ConnectionChanged?.Invoke();
            var settings = Settings;
            var t = new Thread(() =>
            {
                Elm327Adapter adapter = null;
                try
                {
                    IObdTransport transport;
                    switch (settings.Adapter)
                    {
                        case AdapterType.Elm327WiFi:
                            transport = new TcpTransport(settings.Host, settings.TcpPort);
                            break;
                        case AdapterType.Elm327Usb:
                        case AdapterType.Elm327Bluetooth:
                        case AdapterType.ObdLink:
                            if (string.IsNullOrWhiteSpace(settings.SerialPort)) throw new ObdTransportException(Loc.T("conn.noPort"));
                            transport = new SerialTransport(settings.SerialPort, settings.BaudRate);
                            break;
                        default:
                            transport = new SimulatorTransport();
                            break;
                    }
                    adapter = new Elm327Adapter(transport);
                    adapter.Trace += OnTrace;
                    adapter.Initialize((ObdProtocol)settings.Protocol);
                    var vin = ReadVin(adapter);
                    var volt = adapter.ReadVoltage();
                    Post(() =>
                    {
                        Adapter = adapter;
                        DetectedVin = vin;
                        VinInfo = vin.Length > 0 ? VinDecoder.Decode(vin) : null;
                        BatteryVoltage = volt;
                        Connection = ConnectionState.Connected;
                        MatchVehicleToVin(vin);
                        Log("Connected: " + adapter.Version + " / " + ObdProtocolInfo.Name(adapter.Protocol));
                        ConnectionChanged?.Invoke();
                        VehicleChanged?.Invoke();
                    });
                }
                catch (Exception ex)
                {
                    try { adapter?.Dispose(); } catch { }
                    Post(() =>
                    {
                        Connection = ConnectionState.Error;
                        ConnectionError = ex.Message;
                        Log("Connection failed: " + ex.Message);
                        ConnectionChanged?.Invoke();
                    });
                }
            }) { IsBackground = true, Name = "Connect" };
            t.Start();
        }

        private void MatchVehicleToVin(string vin)
        {
            if (string.IsNullOrEmpty(vin)) return;
            var existing = Vehicles.FirstOrDefault(v => string.Equals(v.Vin, vin, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                var info = VinDecoder.Decode(vin);
                existing = new VehicleProfile { Vin = vin, Make = info.Manufacturer, Year = info.Year };
                Vehicles.Add(existing);
                VehicleStore.Save(Vehicles);
            }
            if (ActiveVehicle != existing)
            {
                ActiveVehicle = existing;
                Settings.ActiveVehicleId = existing.Id;
                Settings.Save();
            }
        }

        private static string ReadVin(Elm327Adapter adapter)
        {
            try
            {
                var r = adapter.Request("0902", 5000);
                var payload = r.Payload(0x49, 1);
                var vin = AsciiFrom(payload, true);
                if (vin.Length >= 17) return vin.Substring(vin.Length - 17);
                r = adapter.Request("22F190", 3000);
                payload = r.Payload(0x62, 2);
                vin = AsciiFrom(payload, false);
                if (vin.Length >= 17) return vin.Substring(vin.Length - 17);
            }
            catch { }
            return "";
        }

        private static string AsciiFrom(byte[] payload, bool skipCount)
        {
            if (payload == null) return "";
            var sb = new StringBuilder();
            int start = skipCount && payload.Length > 0 && payload[0] == 0x01 ? 1 : 0;
            for (int i = start; i < payload.Length; i++)
            {
                char c = (char)payload[i];
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        public void Disconnect()
        {
            StopMonitor();
            if (Scan != null && Scan.IsRunning) Scan.Cancel();
            var a = Adapter;
            Adapter = null;
            if (a != null)
            {
                a.Trace -= OnTrace;
                try { a.Dispose(); } catch { }
            }
            bool changed = Connection != ConnectionState.Disconnected;
            Connection = ConnectionState.Disconnected;
            DetectedVin = "";
            BatteryVoltage = null;
            if (changed) ConnectionChanged?.Invoke();
        }

        private void OnTrace(string cmd, string reply, int ms)
        {
            lock (_commLock)
            {
                CommSamples.Add(ms);
                if (CommSamples.Count > 160) CommSamples.RemoveAt(0);
                LastCommAt = DateTime.Now;
                var line = DateTime.Now.ToString("HH:mm:ss.fff") + " > " + cmd + "  [" + ms + " ms]  " + reply.Replace("\r", " | ");
                TraceLog.Add(line);
                if (TraceLog.Count > 500) TraceLog.RemoveAt(0);
            }
        }

        public int[] CommSnapshot()
        {
            lock (_commLock) return CommSamples.ToArray();
        }

        // ------------------------------------------------------------------ scanning

        public bool StartScan()
        {
            if (!IsConnected || IsScanning) return false;
            StopMonitor();
            var engine = new ScanEngine(Adapter, Modules, Ui)
            {
                Vin = CurrentVin,
                VehicleName = VehicleTitle,
                AdapterName = AdapterLabel + (Adapter.Version.Length > 0 ? " (" + Adapter.Version + ")" : "")
            };
            engine.ModuleChanged += m => ModuleChanged?.Invoke(m);
            engine.ProgressChanged += () => ScanProgress?.Invoke();
            engine.Completed += OnScanCompleted;
            Scan = engine;
            engine.Start();
            ScanStateChanged?.Invoke();
            return true;
        }

        public void CancelScan()
        {
            Scan?.Cancel();
        }

        private void OnScanCompleted(ScanResult result)
        {
            LastScan = result;
            HistoryStore.Save(result);
            History.Insert(0, result);
            Log("Scan finished: " + result.Scanned + " modules, " + result.Faults + " faults, " + result.Warnings + " warnings");
            ScanStateChanged?.Invoke();
            HistoryChanged?.Invoke();
        }

        public void ClearHistory()
        {
            HistoryStore.Clear();
            History.Clear();
            HistoryChanged?.Invoke();
        }

        // ------------------------------------------------------------------ live data

        public LiveDataMonitor StartMonitor(LiveParam[] parameters, ModuleDefinition target)
        {
            if (!IsConnected || IsScanning) return null;
            StopMonitor();
            Monitor = new LiveDataMonitor(Adapter, parameters, target, Ui) { IntervalMs = Math.Max(150, Settings.LiveSampleMs) };
            Monitor.Failed += msg =>
            {
                Log("Live data failed: " + msg);
                if (Adapter != null && !Adapter.Transport.IsOpen) Disconnect();
            };
            Monitor.Start();
            return Monitor;
        }

        public void StopMonitor()
        {
            var m = Monitor;
            Monitor = null;
            if (m != null)
            {
                try { m.Stop(); } catch { }
            }
        }

        // ------------------------------------------------------------------ quick DTC operations (call from a worker thread)

        public List<DtcRecord> ReadEcmDtcs()
        {
            var list = new List<DtcRecord>();
            var a = Adapter;
            if (a == null) return list;
            a.ClearTarget();
            var pairs = new[] { new KeyValuePair<string, DtcState>("03", DtcState.Stored), new KeyValuePair<string, DtcState>("07", DtcState.Pending), new KeyValuePair<string, DtcState>("0A", DtcState.Permanent) };
            foreach (var p in pairs)
            {
                var r = a.Request(p.Key, 3000);
                foreach (var reply in r.Replies)
                {
                    if (reply.Data.Count == 0 || reply.IsNegative) continue;
                    var payload = reply.Data.Skip(1).ToArray();
                    foreach (var code in DtcParser.ParseJ1979(payload))
                        if (!list.Any(d => d.Code == code)) list.Add(new DtcRecord { Code = code, State = p.Value, ModuleId = reply.EcuId.ToString("X3") });
                }
            }
            return list;
        }

        public bool ClearEcmDtcs()
        {
            var a = Adapter;
            if (a == null) return false;
            a.ClearTarget();
            var r = a.Request("04", 4000);
            return r.AnyPositive;
        }

        // ------------------------------------------------------------------ misc

        public void Log(string message)
        {
            lock (_commLock)
            {
                TraceLog.Add(DateTime.Now.ToString("HH:mm:ss.fff") + " # " + message);
                if (TraceLog.Count > 500) TraceLog.RemoveAt(0);
            }
        }

        private void Post(Action a)
        {
            var ui = Ui;
            if (ui != null) ui.Post(_ => a(), null);
            else a();
        }
    }
}
