using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using RedlineDiagnostics.Diagnostics;

namespace RedlineDiagnostics.Obd
{
    /// <summary>Transport that talks to an in-process ELM327 emulator with a virtual vehicle behind it.</summary>
    public sealed class SimulatorTransport : IObdTransport
    {
        private readonly Elm327Simulator _sim = new Elm327Simulator();
        private bool _open;

        public string Name => "Simulator";
        public bool IsOpen => _open;
        public VirtualVehicle Vehicle => _sim.Vehicle;

        public void Open() { _open = true; }
        public void Close() { _open = false; }

        public string SendCommand(string command, int timeoutMs)
        {
            if (!_open) throw new ObdTransportException("Simulator not open.");
            int delay;
            var reply = _sim.Execute(command, out delay);
            Thread.Sleep(Math.Min(delay, Math.Max(50, timeoutMs)));
            return reply;
        }

        public void Dispose() => Close();
    }

    /// <summary>Time-based signal model of a hybrid sedan driving slowly around a workshop yard.</summary>
    public sealed class VirtualVehicle
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Random _rng = new Random(7);
        public string Vin = "JTNB4RBE0M3034567";
        public bool MilOn;

        public double T => _clock.Elapsed.TotalSeconds;

        private double Noise(double amp) => (_rng.NextDouble() * 2 - 1) * amp;

        public double Speed => Math.Max(0, 12 + 4 * Math.Sin(T / 6) + 0.3 * Math.Sin(T * 1.7));
        public double Rpm => 1050 + Speed * 38 + 120 * Math.Sin(T / 2.5) + Noise(12);
        public double Coolant => 86 + 2 * Math.Sin(T / 40);
        public double Load => 20 + 8 * Math.Sin(T / 4);
        public double Throttle => 12 + 6 * Math.Sin(T / 4 + 1);
        public double Iat => 31 + Math.Sin(T / 50);
        public double Maf => 4.5 + Load * 0.12;
        public double Stft => 1.5 * Math.Sin(T / 3);
        public double Ltft => 2.3;
        public double Voltage => 13.8 + 0.08 * Math.Sin(T / 1.3) + Noise(0.02);
        public double Timing => 12 + 3 * Math.Sin(T / 5);
        public double Fuel => 62.5;

        /// <summary>Value for a live parameter key defined in <see cref="LiveParam"/>.</summary>
        public double Get(string key)
        {
            double t = T;
            switch (key)
            {
                case "tcm.gear": return 2;
                case "tcm.atf": return 78 + Math.Sin(t / 30);
                case "tcm.inSpeed": return Rpm;
                case "tcm.outSpeed": return Speed * 30;
                case "tcm.linePressure": return 650 + 20 * Math.Sin(t / 3);
                case "abs.wheelFL": return Speed + 0.2 * Math.Sin(t * 2);
                case "abs.wheelFR": return Speed + 0.5 + 0.2 * Math.Sin(t * 2 + 1);
                case "abs.wheelRL": return Speed - 0.3 + 0.2 * Math.Sin(t * 2 + 2);
                case "abs.wheelRR": return Speed - 0.1 + 0.2 * Math.Sin(t * 2 + 3);
                case "abs.voltage": return Voltage;
                case "abs.brake": return 0.4 + 0.3 * Math.Max(0, Math.Sin(t / 7));
                case "abs.yaw": return 0.8 * Math.Sin(t / 8);
                case "srs.voltage": return Voltage;
                case "srs.squibD": return 2.1 + Noise(0.01);
                case "srs.squibP": return 2.2 + Noise(0.01);
                case "srs.status": return 0;
                case "bcm.voltage": return Voltage;
                case "bcm.temp": return 38 + Math.Sin(t / 20);
                case "bcm.msg": return (t * 50) % 65535;
                case "bcm.doors": return 0;
                case "tpms.fl": return 2.35;
                case "tpms.fr": return 2.33;
                case "tpms.rl": return 1.95;
                case "tpms.rr": return 2.30;
                case "tpms.temp": return 28 + Math.Sin(t / 60);
                case "hvac.cabin": return 24.5 - 0.02 * Math.Min(t, 60);
                case "hvac.set": return 22;
                case "hvac.blower": return 3;
                case "hvac.evap": return 6.5 + 0.5 * Math.Sin(t / 9);
                case "eps.angle": return 6 * Math.Sin(t / 8);
                case "eps.torque": return 0.4 * Math.Sin(t / 8);
                case "eps.current": return 3 + 2 * Math.Abs(Math.Sin(t / 8));
                case "eps.motorTemp": return 44 + Math.Sin(t / 25);
                case "ipc.odo": return 48230;
                case "ipc.fuel": return Fuel;
                case "ipc.voltage": return Voltage;
                case "gtw.load": return 32 + 5 * Math.Sin(t / 2);
                case "gtw.msg": return (t * 900) % 65535;
                case "gtw.voltage": return Voltage;
                case "hv.soc": return 58 + 3 * Math.Sin(t / 20);
                case "hv.volt": return 244 + 2 * Math.Sin(t / 4);
                case "hv.motor": return Speed * 90;
                case "hv.invTemp": return 41 + Math.Sin(t / 15);
                case "bms.soc": return 58 + 3 * Math.Sin(t / 20);
                case "bms.pack": return 244 + 2 * Math.Sin(t / 4);
                case "bms.temp": return 31 + 0.5 * Math.Sin(t / 30);
                case "bms.delta": return 12 + Math.Round(2 * Math.Sin(t / 5));
                case "pcs.status": return 1;
                case "pcs.dist": return 24 + 6 * Math.Sin(t / 5);
                case "pcs.camTemp": return 39 + Math.Sin(t / 20);
                case "pkb.current": return 0;
                case "pkb.status": return 0;
                case "pkb.voltage": return Voltage;
                case "smk.key": return 1;
                case "smk.voltage": return Voltage;
                case "smk.lf": return -62 + Noise(1);
                case "mid.voltage": return Voltage;
                case "mid.temp": return 42 + Math.Sin(t / 20);
                case "mid.status": return 1;
                case "tel.signal": return -78;
                case "tel.status": return 1;
                case "tel.voltage": return Voltage;
                case "scu.pos": return 120;
                case "scu.current": return 0;
                case "scu.voltage": return Voltage;
                case "lcm.voltage": return Voltage;
                case "lcm.current": return 4.2 + Noise(0.05);
                case "lcm.status": return 1;
                case "pdm.window": return 100;
                case "pdm.lock": return 1;
                case "pdm.voltage": return Voltage;
                case "afs.swivel": return 1.2 * Math.Sin(t / 8);
                case "afs.level": return -0.5;
                case "afs.voltage": return Voltage;
                default: return 0;
            }
        }

        public byte[] EncodePid(int pid, out bool supported)
        {
            supported = true;
            switch (pid)
            {
                case 0x00: return Mask(0x00);
                case 0x20: return Mask(0x20);
                case 0x40: return Mask(0x40);
                case 0x01:
                    return new byte[] { (byte)(MilOn ? 0x81 : 0x00), 0x07, 0xE5, 0x04 };
                case 0x04: return new[] { Pct(Load) };
                case 0x05: return new[] { (byte)(Coolant + 40) };
                case 0x06: return new[] { (byte)(Stft * 128 / 100 + 128) };
                case 0x07: return new[] { (byte)(Ltft * 128 / 100 + 128) };
                case 0x0B: return new byte[] { 35 };
                case 0x0C: return U16(Rpm * 4);
                case 0x0D: return new[] { (byte)Speed };
                case 0x0E: return new[] { (byte)((Timing + 64) * 2) };
                case 0x0F: return new[] { (byte)(Iat + 40) };
                case 0x10: return U16(Maf * 100);
                case 0x11: return new[] { Pct(Throttle) };
                case 0x1F: return U16(T);
                case 0x21: return U16(0);
                case 0x2F: return new[] { Pct(Fuel) };
                case 0x31: return U16(1240);
                case 0x33: return new byte[] { 101 };
                case 0x42: return U16(Voltage * 1000);
                case 0x46: return new byte[] { 29 + 40 };
                case 0x5C: return new byte[] { 92 + 40 };
                case 0x5E: return U16(2.4 * 20);
                default:
                    supported = false;
                    return null;
            }
        }

        public static readonly int[] SupportedPidList = { 0x01, 0x04, 0x05, 0x06, 0x07, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x1F, 0x20, 0x21, 0x2F, 0x31, 0x33, 0x40, 0x42, 0x46, 0x5C, 0x5E };

        private static byte[] Mask(int basePid)
        {
            var m = new byte[4];
            foreach (var p in SupportedPidList)
            {
                int off = p - basePid - 1;
                if (off < 0 || off >= 32) continue;
                m[off / 8] |= (byte)(0x80 >> (off % 8));
            }
            return m;
        }

        private static byte Pct(double v) => (byte)Math.Max(0, Math.Min(255, v * 255 / 100));
        private static byte[] U16(double v)
        {
            int i = (int)Math.Max(0, Math.Min(65535, v));
            return new[] { (byte)(i >> 8), (byte)(i & 0xFF) };
        }
    }

    internal sealed class SimEcu
    {
        public string Short;
        public int ReqId, RespId;
        public string PartNumber;
        public bool Obd;         // answers J1979 services 01/03/07/09
        public bool Present = true;
        public List<KeyValuePair<string, bool>> Dtcs = new List<KeyValuePair<string, bool>>(); // code, stored(true)/pending(false)
        public string Name;
    }

    /// <summary>Emulates the ELM327 command interpreter over a CAN 11-bit / 500 kbps virtual bus.</summary>
    public sealed class Elm327Simulator
    {
        public VirtualVehicle Vehicle { get; } = new VirtualVehicle();

        private bool _echo = true, _headers, _spaces = true;
        private int _header = 0x7DF;
        private int _cra = -1;
        private int _protocol;
        private readonly Random _rng = new Random(3);
        private readonly List<SimEcu> _ecus = new List<SimEcu>();

        public Elm327Simulator()
        {
            Add("ECM", 0x7E0, 0x7E8, "89661-06L20", true, "ECM-EngineControl");
            Add("TCM", 0x7E1, 0x7E9, "89535-33010", true, "TCM-Transmission");
            Add("ABS", 0x7B0, 0x7B8, "89541-33210", false, "ABS-BrakeControl", "C0035:1", "C0040:1");
            Add("SRS", 0x780, 0x788, "89170-06B70", false, "SRS-AirbagSensor", "B0001:0");
            Add("BCM", 0x750, 0x758, "89221-06180", false, "BCM-BodyControl");
            Add("TPMS", 0x7A0, 0x7A8, "89769-06050", false, "TPMS-TirePressure", "C2126:0");
            Add("HVAC", 0x7C4, 0x7CC, "88650-06510", false, "HVAC-AirConditioner");
            Add("EPS", 0x7A1, 0x7A9, "89650-06450", false, "EPS-PowerSteering");
            Add("IPC", 0x7C0, 0x7C8, "83800-06M40", false, "IPC-MeterCluster");
            Add("GTW", 0x7D0, 0x7D8, "89111-06010", false, "GTW-CentralGateway");
            Add("HV", 0x7E2, 0x7EA, "89981-33450", true, "HV-HybridControl");
            Add("BMS", 0x7E3, 0x7EB, "89892-33060", false, "BMS-BatteryMonitor", "P0A80:0");
            Add("PCS", 0x7A2, 0x7AA, "88210-06140", false, "PCS-PreCollision");
            Add("PKB", 0x7B1, 0x7B9, "89680-06030", false, "PKB-ParkingBrake");
            Add("SMK", 0x7B2, 0x7BA, "89990-06020", false, "SMK-SmartKey");
            Add("MID", 0x7D1, 0x7D9, "86140-06920", false, "MID-Multimedia");
            Add("TEL", 0x7D2, 0x7DA, "86741-06010", false, "TEL-Telematics").Present = false;
            Add("SCU", 0x7D3, 0x7DB, "89710-06030", false, "SCU-SeatControl");
            Add("LCM", 0x7D4, 0x7DC, "81140-06E80", false, "LCM-LightControl");
            Add("PDM", 0x7D5, 0x7DD, "85720-06180", false, "PDM-DoorControl");
            Add("AFS", 0x7D6, 0x7DE, "89940-06020", false, "AFS-AdaptiveLight");
        }

        private SimEcu Add(string s, int req, int resp, string part, bool obd, string name, params string[] dtcs)
        {
            var e = new SimEcu { Short = s, ReqId = req, RespId = resp, PartNumber = part, Obd = obd, Name = name };
            foreach (var d in dtcs)
            {
                var p = d.Split(':');
                e.Dtcs.Add(new KeyValuePair<string, bool>(p[0], p.Length < 2 || p[1] == "1"));
            }
            _ecus.Add(e);
            return e;
        }

        public string Execute(string command, out int delayMs)
        {
            delayMs = 20 + _rng.Next(15);
            var cmd = (command ?? "").Trim();
            var sb = new StringBuilder();
            if (_echo) sb.Append(cmd).Append('\r');
            var up = cmd.Replace(" ", "").ToUpperInvariant();

            if (up.Length == 0)
            {
                sb.Append("\r");
                return sb.ToString();
            }

            if (up.StartsWith("AT") || up.StartsWith("ST"))
            {
                sb.Append(ExecuteAt(up));
                sb.Append("\r\r");
                return sb.ToString();
            }

            if (!up.All(c => Uri.IsHexDigit(c)) || up.Length % 2 != 0)
            {
                sb.Append("?\r\r");
                return sb.ToString();
            }

            var req = ObdResponse.HexToBytes(up);
            var targets = _header == 0x7DF ? _ecus.Where(e => e.Obd).ToList() : _ecus.Where(e => e.ReqId == _header).ToList();
            var lines = new List<string>();
            foreach (var ecu in targets)
            {
                if (!ecu.Present) continue;
                if (_cra >= 0 && ecu.RespId != _cra) continue;
                var data = Respond(ecu, req);
                if (data == null) continue;
                lines.AddRange(FormatFrames(ecu.RespId, data));
            }
            if (lines.Count == 0)
            {
                delayMs = targets.Any(e => !e.Present) ? 450 : 200;
                sb.Append("NO DATA\r\r");
                return sb.ToString();
            }
            foreach (var l in lines) sb.Append(l).Append('\r');
            sb.Append('\r');
            return sb.ToString();
        }

        private string ExecuteAt(string up)
        {
            string arg = up.Length > 2 ? up.Substring(2) : "";
            if (arg == "Z")
            {
                _echo = true; _headers = false; _spaces = true; _header = 0x7DF; _cra = -1; _protocol = 0;
                return "\r\rELM327 v1.5";
            }
            if (arg == "I") return "ELM327 v1.5";
            if (arg == "@1") return "OBDII to RS232 Interpreter";
            if (arg == "E0") { _echo = false; return "OK"; }
            if (arg == "E1") { _echo = true; return "OK"; }
            if (arg == "H0") { _headers = false; return "OK"; }
            if (arg == "H1") { _headers = true; return "OK"; }
            if (arg == "S0") { _spaces = false; return "OK"; }
            if (arg == "S1") { _spaces = true; return "OK"; }
            if (arg == "RV") return Vehicle.Voltage.ToString("0.0", CultureInfo.InvariantCulture) + "V";
            if (arg == "DPN") return _protocol == 0 ? "A6" : _protocol.ToString();
            if (arg == "DP") return "AUTO, ISO 15765-4 (CAN 11/500)";
            if (arg.StartsWith("SP")) { int.TryParse(arg.Substring(2), out _protocol); return "OK"; }
            if (arg.StartsWith("SH"))
            {
                int h;
                if (int.TryParse(arg.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out h)) _header = h & 0xFFF;
                return "OK";
            }
            if (arg.StartsWith("CRA"))
            {
                if (arg.Length == 3) { _cra = -1; return "OK"; }
                int h;
                if (int.TryParse(arg.Substring(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out h)) _cra = h & 0xFFF;
                return "OK";
            }
            if (arg == "AR") { _cra = -1; return "OK"; }
            if (up.StartsWith("STI")) return "?";
            if (up.StartsWith("ST")) return "?";
            return "OK";
        }

        private byte[] Respond(SimEcu ecu, byte[] req)
        {
            if (req.Length == 0) return null;
            byte mode = req[0];
            switch (mode)
            {
                case 0x01:
                    {
                        if (!ecu.Obd) return Negative(mode, 0x11);
                        if (req.Length < 2) return Negative(mode, 0x12);
                        int pid = req[1];
                        bool sup;
                        var data = Vehicle.EncodePid(pid, out sup);
                        if (!sup) return null;
                        return Concat(new byte[] { 0x41, (byte)pid }, data);
                    }
                case 0x03:
                case 0x07:
                case 0x0A:
                    {
                        if (!ecu.Obd) return Negative(mode, 0x11);
                        var codes = mode == 0x0A ? new List<string>() : ecu.Dtcs.Where(d => d.Value == (mode == 0x03)).Select(d => d.Key).ToList();
                        var list = new List<byte> { (byte)(mode + 0x40), (byte)codes.Count };
                        foreach (var c in codes) list.AddRange(EncodeDtc(c));
                        return list.ToArray();
                    }
                case 0x04:
                    ecu.Dtcs.RemoveAll(d => d.Value);
                    Vehicle.MilOn = false;
                    return new byte[] { 0x44 };
                case 0x09:
                    {
                        if (!ecu.Obd) return Negative(mode, 0x11);
                        if (req.Length < 2) return Negative(mode, 0x12);
                        switch (req[1])
                        {
                            case 0x00: return new byte[] { 0x49, 0x00, 0x55, 0x40, 0x00, 0x00 };
                            case 0x02:
                                if (ecu.Short != "ECM") return null;
                                return Concat(new byte[] { 0x49, 0x02, 0x01 }, Encoding.ASCII.GetBytes(Vehicle.Vin));
                            case 0x0A:
                                return Concat(new byte[] { 0x49, 0x0A, 0x01 }, Encoding.ASCII.GetBytes(ecu.Name.PadRight(20).Substring(0, 20)));
                            default: return null;
                        }
                    }
                case 0x14:
                    ecu.Dtcs.Clear();
                    return new byte[] { 0x54 };
                case 0x19:
                    {
                        if (req.Length < 2) return Negative(mode, 0x13);
                        if (req[1] == 0x02)
                        {
                            var list = new List<byte> { 0x59, 0x02, 0xFF };
                            foreach (var d in ecu.Dtcs)
                            {
                                list.AddRange(EncodeDtc(d.Key));
                                list.Add(0x00);
                                list.Add(d.Value ? (byte)0x09 : (byte)0x04);
                            }
                            return list.ToArray();
                        }
                        return Negative(mode, 0x12);
                    }
                case 0x22:
                    {
                        if (req.Length < 3) return Negative(mode, 0x13);
                        int did = (req[1] << 8) | req[2];
                        if (did == 0xF187)
                            return Concat(new byte[] { 0x62, 0xF1, 0x87 }, Encoding.ASCII.GetBytes(ecu.PartNumber));
                        if (did == 0xF190 && ecu.Short == "ECM")
                            return Concat(new byte[] { 0x62, 0xF1, 0x90 }, Encoding.ASCII.GetBytes(Vehicle.Vin));
                        var p = LiveParam.For(ecu.Short).FirstOrDefault(x => x.Mode == 0x22 && x.Id == did);
                        if (p == null) return Negative(mode, 0x31);
                        return Concat(new byte[] { 0x62, req[1], req[2] }, p.Encode(Vehicle.Get(p.Key)));
                    }
                case 0x3E:
                    return new byte[] { 0x7E, 0x00 };
                case 0x10:
                    return new byte[] { 0x50, req.Length > 1 ? req[1] : (byte)0x01, 0x00, 0x32, 0x01, 0xF4 };
                default:
                    return Negative(mode, 0x11);
            }
        }

        private static byte[] Negative(byte mode, byte nrc) => new byte[] { 0x7F, mode, nrc };

        private static byte[] Concat(byte[] a, byte[] b)
        {
            var r = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, r, 0, a.Length);
            Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
            return r;
        }

        public static byte[] EncodeDtc(string code)
        {
            code = (code ?? "P0000").ToUpperInvariant();
            int sys;
            switch (code[0])
            {
                case 'C': sys = 1; break;
                case 'B': sys = 2; break;
                case 'U': sys = 3; break;
                default: sys = 0; break;
            }
            int d1 = code[1] - '0';
            int d2 = int.Parse(code.Substring(2, 1), NumberStyles.HexNumber);
            int rest = int.Parse(code.Substring(3, 2), NumberStyles.HexNumber);
            return new[] { (byte)((sys << 6) | (d1 << 4) | d2), (byte)rest };
        }

        private IEnumerable<string> FormatFrames(int respId, byte[] data)
        {
            var frames = new List<byte[]>();
            if (data.Length <= 7)
            {
                var f = new byte[8];
                f[0] = (byte)data.Length;
                Buffer.BlockCopy(data, 0, f, 1, data.Length);
                frames.Add(f);
            }
            else
            {
                var first = new byte[8];
                first[0] = (byte)(0x10 | ((data.Length >> 8) & 0x0F));
                first[1] = (byte)(data.Length & 0xFF);
                Buffer.BlockCopy(data, 0, first, 2, 6);
                frames.Add(first);
                int pos = 6, seq = 1;
                while (pos < data.Length)
                {
                    var cf = new byte[8];
                    cf[0] = (byte)(0x20 | (seq & 0x0F));
                    int n = Math.Min(7, data.Length - pos);
                    Buffer.BlockCopy(data, pos, cf, 1, n);
                    frames.Add(cf);
                    pos += n;
                    seq++;
                }
            }
            foreach (var f in frames)
            {
                var sb = new StringBuilder();
                if (_headers) sb.Append(respId.ToString("X3"));
                foreach (var b in f)
                {
                    if (_spaces && sb.Length > 0) sb.Append(' ');
                    sb.Append(b.ToString("X2"));
                }
                yield return sb.ToString();
            }
        }
    }
}
