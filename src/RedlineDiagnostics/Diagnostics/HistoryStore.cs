using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Xml.Serialization;
using RedlineDiagnostics.App;

namespace RedlineDiagnostics.Diagnostics
{
    /// <summary>Scan results as JSON files under %AppData%\RedlineDiagnostics\history.</summary>
    public static class HistoryStore
    {
        private static string Dir
        {
            get
            {
                var d = Path.Combine(Settings.Directory, "history");
                try { Directory.CreateDirectory(d); } catch { }
                return d;
            }
        }

        public static string Save(ScanResult result)
        {
            var path = Path.Combine(Dir, "scan_" + result.Started.ToString("yyyyMMdd_HHmmss") + ".json");
            try
            {
                var ser = new DataContractJsonSerializer(typeof(ScanResult));
                using (var fs = File.Create(path))
                    ser.WriteObject(fs, result);
            }
            catch { }
            return path;
        }

        public static List<ScanResult> LoadAll()
        {
            var list = new List<ScanResult>();
            try
            {
                var ser = new DataContractJsonSerializer(typeof(ScanResult));
                foreach (var f in Directory.GetFiles(Dir, "scan_*.json"))
                {
                    try
                    {
                        using (var fs = File.OpenRead(f))
                        {
                            var r = ser.ReadObject(fs) as ScanResult;
                            if (r != null) list.Add(r);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return list.OrderByDescending(r => r.Started).ToList();
        }

        public static void Clear()
        {
            try
            {
                foreach (var f in Directory.GetFiles(Dir, "scan_*.json")) File.Delete(f);
            }
            catch { }
        }
    }

    /// <summary>Garage vehicles as XML.</summary>
    public static class VehicleStore
    {
        private static string FilePath => Path.Combine(Settings.Directory, "vehicles.xml");

        public static List<VehicleProfile> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var ser = new XmlSerializer(typeof(List<VehicleProfile>));
                    using (var fs = File.OpenRead(FilePath))
                        return (List<VehicleProfile>)ser.Deserialize(fs) ?? new List<VehicleProfile>();
                }
            }
            catch { }
            return new List<VehicleProfile>();
        }

        public static void Save(List<VehicleProfile> vehicles)
        {
            try
            {
                var ser = new XmlSerializer(typeof(List<VehicleProfile>));
                using (var fs = File.Create(FilePath))
                    ser.Serialize(fs, vehicles);
            }
            catch { }
        }
    }
}
