using System;
using System.IO;
using System.Xml.Serialization;
using RedlineDiagnostics.Obd;

namespace RedlineDiagnostics.App
{
    /// <summary>User settings persisted as XML under %AppData%\RedlineDiagnostics.</summary>
    [Serializable]
    public class Settings
    {
        public string Language = "en";
        public AdapterType Adapter = AdapterType.Simulator;
        public string SerialPort = "";
        public int BaudRate = 38400;
        public string Host = "192.168.0.10";
        public int TcpPort = 35000;
        public int Protocol = 0;
        public bool Metric = true;
        public string ModelPath = "";
        public bool ModelFlip = false;
        public bool AutoRotate = true;
        public bool ShowHarness = true;
        public bool FullScreen = true;
        public string ActiveVehicleId = "";
        public int LiveSampleMs = 500;

        public static string Directory
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RedlineDiagnostics");
                try { System.IO.Directory.CreateDirectory(dir); } catch { }
                return dir;
            }
        }

        public static string DocumentsDirectory
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RedlineDiagnostics");
                try { System.IO.Directory.CreateDirectory(dir); } catch { }
                return dir;
            }
        }

        private static string FilePath => Path.Combine(Directory, "settings.xml");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var ser = new XmlSerializer(typeof(Settings));
                    using (var fs = File.OpenRead(FilePath))
                        return (Settings)ser.Deserialize(fs) ?? new Settings();
                }
            }
            catch { }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                var ser = new XmlSerializer(typeof(Settings));
                using (var fs = File.Create(FilePath))
                    ser.Serialize(fs, this);
            }
            catch { }
        }
    }
}
