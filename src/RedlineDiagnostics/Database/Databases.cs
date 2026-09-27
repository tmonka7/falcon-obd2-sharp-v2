using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using RedlineDiagnostics.Localization;
using RedlineDiagnostics.Rendering3D;

namespace RedlineDiagnostics.Database
{
    /// <summary>
    /// Reads the embedded CSV tables. If a file with the same name exists in a "Data" folder next to
    /// the executable its rows are appended, so workshops can extend the database without rebuilding.
    /// </summary>
    internal static class Csv
    {
        public static IEnumerable<string[]> Read(string fileName)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream("Data." + fileName))
            {
                if (s != null)
                    foreach (var row in ReadStream(s)) yield return row;
            }
            var ext = Path.Combine(Path.GetDirectoryName(asm.Location) ?? ".", "Data", fileName);
            if (File.Exists(ext))
            {
                using (var fs = File.OpenRead(ext))
                    foreach (var row in ReadStream(fs)) yield return row;
            }
        }

        private static IEnumerable<string[]> ReadStream(Stream s)
        {
            using (var r = new StreamReader(s, Encoding.UTF8, true))
            {
                bool first = true;
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (first) { first = false; continue; } // header
                    if (line.Trim().Length == 0 || line.StartsWith("#")) continue;
                    yield return ParseLine(line);
                }
            }
        }

        public static string[] ParseLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else quoted = false;
                    }
                    else sb.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            fields.Add(sb.ToString());
            return fields.ToArray();
        }

        public static string Get(string[] row, int i) => i < row.Length ? row[i].Trim() : "";
    }

    // ------------------------------------------------------------------------------------------

    public sealed class PidInfo
    {
        public int Pid;
        public string En, Ja, Zh, Unit;
        public string Name => Loc.Pick(En, Ja, Zh);
    }

    public static class PidDatabase
    {
        private static Dictionary<int, PidInfo> _pids;

        public static void Load()
        {
            _pids = new Dictionary<int, PidInfo>();
            foreach (var row in Csv.Read("pids.csv"))
            {
                int pid;
                if (!int.TryParse(Csv.Get(row, 0), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out pid)) continue;
                _pids[pid] = new PidInfo { Pid = pid, En = Csv.Get(row, 1), Ja = Csv.Get(row, 2), Zh = Csv.Get(row, 3), Unit = Csv.Get(row, 4) };
            }
        }

        public static int Count => _pids?.Count ?? 0;

        public static PidInfo Get(int pid)
        {
            PidInfo p;
            return _pids != null && _pids.TryGetValue(pid, out p) ? p : null;
        }

        public static string Name(int pid)
        {
            var p = Get(pid);
            return p != null ? p.Name : "PID " + pid.ToString("X2");
        }
    }

    // ------------------------------------------------------------------------------------------

    public sealed class ModuleDefinition
    {
        public int Id;
        public string Short;
        public string En, Ja, Zh;
        public int RequestId, ResponseId;
        public Vec3 Position;
        public string Icon;
        public string Name => Loc.Pick(En, Ja, Zh);
    }

    public static class ModuleCatalog
    {
        private static List<ModuleDefinition> _all;

        public static void Load()
        {
            _all = new List<ModuleDefinition>();
            foreach (var row in Csv.Read("modules.csv"))
            {
                int id, req, resp;
                float x, y, z;
                if (!int.TryParse(Csv.Get(row, 0), out id)) continue;
                int.TryParse(Csv.Get(row, 5), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out req);
                int.TryParse(Csv.Get(row, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out resp);
                float.TryParse(Csv.Get(row, 7), NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                float.TryParse(Csv.Get(row, 8), NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                float.TryParse(Csv.Get(row, 9), NumberStyles.Float, CultureInfo.InvariantCulture, out z);
                _all.Add(new ModuleDefinition
                {
                    Id = id,
                    Short = Csv.Get(row, 1),
                    En = Csv.Get(row, 2),
                    Ja = Csv.Get(row, 3),
                    Zh = Csv.Get(row, 4),
                    RequestId = req,
                    ResponseId = resp,
                    Position = new Vec3(x, y, z),
                    Icon = Csv.Get(row, 10)
                });
            }
        }

        public static IReadOnlyList<ModuleDefinition> All => _all ?? new List<ModuleDefinition>();
        public static int Count => _all?.Count ?? 0;
        public static ModuleDefinition ByShort(string s) => _all?.FirstOrDefault(m => string.Equals(m.Short, s, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------------------------------

    public sealed class VinInfo
    {
        public string Vin = "";
        public string Manufacturer = "";
        public string Country = "";
        public int Year;
        public bool IsValid;
        public string Serial = "";
    }

    public static class VinDecoder
    {
        private static Dictionary<string, KeyValuePair<string, string>> _wmi;
        private const string YearCodes = "ABCDEFGHJKLMNPRSTVWXY123456789";

        public static void Load()
        {
            _wmi = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in Csv.Read("vin_wmi.csv"))
            {
                var key = Csv.Get(row, 0);
                if (key.Length == 0) continue;
                _wmi[key] = new KeyValuePair<string, string>(Csv.Get(row, 1), Csv.Get(row, 2));
            }
        }

        public static VinInfo Decode(string vin)
        {
            var info = new VinInfo { Vin = (vin ?? "").Trim().ToUpperInvariant() };
            var v = info.Vin;
            if (v.Length < 11) return info;
            info.IsValid = v.Length == 17 && !v.Any(c => c == 'I' || c == 'O' || c == 'Q');
            KeyValuePair<string, string> kv;
            if (_wmi != null)
            {
                if (_wmi.TryGetValue(v.Substring(0, 3), out kv) || _wmi.TryGetValue(v.Substring(0, 2), out kv))
                {
                    info.Manufacturer = kv.Key;
                    info.Country = kv.Value;
                }
            }
            if (info.Country.Length == 0) info.Country = CountryFromChar(v[0]);
            // Model year: position 10. Letters A-Y (2010-2030) or digits (2001-2009); ambiguity with 1980-2000
            // is resolved by preferring the 2010+ cycle for VINs with a numeric 7th character rule.
            char yc = v[9];
            int idx = YearCodes.IndexOf(yc);
            if (idx >= 0)
            {
                int year = 2010 + idx;
                if (year > DateTime.Now.Year + 1) year -= 30;
                info.Year = year;
            }
            if (v.Length == 17) info.Serial = v.Substring(11);
            return info;
        }

        private static string CountryFromChar(char c)
        {
            if (c >= '1' && c <= '5') return "United States";
            if (c == '6' || c == '7') return "Australia";
            if (c == '8' || c == '9') return "South America";
            if (c == 'J') return "Japan";
            if (c == 'K') return "South Korea";
            if (c == 'L') return "China";
            if (c == 'M') return "India";
            if (c == 'S') return "United Kingdom";
            if (c == 'T') return "Czech Republic";
            if (c == 'V') return "France / Spain";
            if (c == 'W') return "Germany";
            if (c == 'Y') return "Sweden";
            if (c == 'Z') return "Italy";
            return "";
        }
    }
}
