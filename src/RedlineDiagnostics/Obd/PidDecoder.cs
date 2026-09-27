using System;
using System.Collections.Generic;

namespace RedlineDiagnostics.Obd
{
    /// <summary>SAE J1979 mode 01 PID formulas.</summary>
    public static class PidDecoder
    {
        public static double? Decode(int pid, byte[] d)
        {
            if (d == null) return null;
            Func<int, int> B = i => i < d.Length ? d[i] : 0;
            int A = B(0), Bb = B(1), C = B(2), D = B(3);
            switch (pid)
            {
                case 0x04: return A * 100.0 / 255;
                case 0x05: return A - 40;
                case 0x06:
                case 0x07:
                case 0x08:
                case 0x09: return (A - 128) * 100.0 / 128;
                case 0x0A: return A * 3;
                case 0x0B: return A;
                case 0x0C: return (A * 256 + Bb) / 4.0;
                case 0x0D: return A;
                case 0x0E: return A / 2.0 - 64;
                case 0x0F: return A - 40;
                case 0x10: return (A * 256 + Bb) / 100.0;
                case 0x11: return A * 100.0 / 255;
                case 0x14:
                case 0x15:
                case 0x16:
                case 0x17:
                case 0x18:
                case 0x19:
                case 0x1A:
                case 0x1B: return A / 200.0;
                case 0x1F: return A * 256 + Bb;
                case 0x21: return A * 256 + Bb;
                case 0x22: return (A * 256 + Bb) * 0.079;
                case 0x23: return (A * 256 + Bb) * 10;
                case 0x2C: return A * 100.0 / 255;
                case 0x2D: return (A - 128) * 100.0 / 128;
                case 0x2E: return A * 100.0 / 255;
                case 0x2F: return A * 100.0 / 255;
                case 0x30: return A;
                case 0x31: return A * 256 + Bb;
                case 0x32: return ((A * 256 + Bb) - 32768) / 4.0;
                case 0x33: return A;
                case 0x3C:
                case 0x3D:
                case 0x3E:
                case 0x3F: return (A * 256 + Bb) / 10.0 - 40;
                case 0x42: return (A * 256 + Bb) / 1000.0;
                case 0x43: return (A * 256 + Bb) * 100.0 / 255;
                case 0x44: return (A * 256 + Bb) / 32768.0;
                case 0x45:
                case 0x47:
                case 0x48:
                case 0x49:
                case 0x4A:
                case 0x4B:
                case 0x4C: return A * 100.0 / 255;
                case 0x46: return A - 40;
                case 0x4D:
                case 0x4E: return A * 256 + Bb;
                case 0x52: return A * 100.0 / 255;
                case 0x5A: return A * 100.0 / 255;
                case 0x5B: return A * 100.0 / 255;
                case 0x5C: return A - 40;
                case 0x5D: return ((A * 256 + Bb) - 26880) / 128.0;
                case 0x5E: return (A * 256 + Bb) / 20.0;
                case 0x61:
                case 0x62: return A - 125;
                case 0x63: return A * 256 + Bb;
                case 0x67: return Bb - 40;
                case 0xA6: return (uint)((A << 24) | (Bb << 16) | (C << 8) | D) / 10.0;
                default:
                    if (d.Length >= 2) return A * 256 + Bb;
                    if (d.Length == 1) return A;
                    return null;
            }
        }

        public static string Unit(int pid)
        {
            switch (pid)
            {
                case 0x04: case 0x11: case 0x2C: case 0x2E: case 0x2F: case 0x43: case 0x45: case 0x47: case 0x48:
                case 0x49: case 0x4A: case 0x4B: case 0x4C: case 0x52: case 0x5A: case 0x5B:
                    return "%";
                case 0x06: case 0x07: case 0x08: case 0x09: case 0x2D: return "%";
                case 0x05: case 0x0F: case 0x3C: case 0x3D: case 0x3E: case 0x3F: case 0x46: case 0x5C: case 0x67: return "°C";
                case 0x0A: case 0x0B: case 0x22: case 0x23: case 0x33: return "kPa";
                case 0x0C: return "rpm";
                case 0x0D: return "km/h";
                case 0x0E: return "°";
                case 0x10: return "g/s";
                case 0x14: case 0x15: case 0x16: case 0x17: case 0x18: case 0x19: case 0x1A: case 0x1B: return "V";
                case 0x1F: case 0x4D: case 0x4E: return "s";
                case 0x21: case 0x31: case 0xA6: return "km";
                case 0x42: return "V";
                case 0x5E: return "L/h";
                case 0x5D: return "°";
                case 0x61: case 0x62: return "%";
                case 0x63: return "Nm";
                default: return "";
            }
        }

        /// <summary>Decodes a "supported PIDs" bitmask (PIDs 00/20/40/...).</summary>
        public static List<int> SupportedPids(int basePid, byte[] mask)
        {
            var list = new List<int>();
            if (mask == null) return list;
            for (int i = 0; i < Math.Min(4, mask.Length); i++)
                for (int bit = 0; bit < 8; bit++)
                    if ((mask[i] & (0x80 >> bit)) != 0) list.Add(basePid + i * 8 + bit + 1);
            return list;
        }

        public sealed class MonitorStatus
        {
            public bool MilOn;
            public int DtcCount;
            public bool IsCompression;
            // name -> (available, complete)
            public List<KeyValuePair<string, bool?>> Monitors = new List<KeyValuePair<string, bool?>>();
        }

        /// <summary>PID 01: MIL, DTC count and readiness monitors.</summary>
        public static MonitorStatus DecodeMonitorStatus(byte[] d)
        {
            var s = new MonitorStatus();
            if (d == null || d.Length < 4) return s;
            s.MilOn = (d[0] & 0x80) != 0;
            s.DtcCount = d[0] & 0x7F;
            s.IsCompression = (d[1] & 0x08) != 0;
            AddMonitor(s, "mon.misfire", (d[1] & 0x01) != 0, (d[1] & 0x10) == 0);
            AddMonitor(s, "mon.fuel", (d[1] & 0x02) != 0, (d[1] & 0x20) == 0);
            AddMonitor(s, "mon.comp", (d[1] & 0x04) != 0, (d[1] & 0x40) == 0);
            if (!s.IsCompression)
            {
                string[] names = { "mon.catalyst", "mon.heatedCat", "mon.evap", "mon.secAir", "mon.o2", "mon.o2Heater", "mon.egr" };
                int[] bits = { 0x01, 0x02, 0x04, 0x08, 0x20, 0x40, 0x80 };
                for (int i = 0; i < names.Length; i++)
                    AddMonitor(s, names[i], (d[2] & bits[i]) != 0, (d[3] & bits[i]) == 0);
            }
            return s;
        }

        private static void AddMonitor(MonitorStatus s, string key, bool available, bool complete)
        {
            s.Monitors.Add(new KeyValuePair<string, bool?>(key, available ? complete : (bool?)null));
        }
    }
}
