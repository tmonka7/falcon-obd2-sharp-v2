using System;

namespace RedlineDiagnostics.Obd
{
    /// <summary>OBD-II physical/link protocols, numbered as the ELM327 "AT SP n" command expects.</summary>
    public enum ObdProtocol
    {
        Auto = 0,
        J1850PWM = 1,
        J1850VPW = 2,
        ISO9141 = 3,
        KWP2000Slow = 4,
        KWP2000Fast = 5,
        Can11Bit500K = 6,
        Can29Bit500K = 7,
        Can11Bit250K = 8,
        Can29Bit250K = 9,
        Unknown = 99
    }

    public enum AdapterType
    {
        Simulator = 0,
        Elm327Usb = 1,
        Elm327Bluetooth = 2,
        Elm327WiFi = 3,
        ObdLink = 4
    }

    public static class ObdProtocolInfo
    {
        public static string Name(ObdProtocol p)
        {
            switch (p)
            {
                case ObdProtocol.Auto: return "Auto";
                case ObdProtocol.J1850PWM: return "SAE J1850 PWM (41.6 kbps)";
                case ObdProtocol.J1850VPW: return "SAE J1850 VPW (10.4 kbps)";
                case ObdProtocol.ISO9141: return "ISO 9141-2 (5 baud init)";
                case ObdProtocol.KWP2000Slow: return "ISO 14230-4 KWP (5 baud init)";
                case ObdProtocol.KWP2000Fast: return "ISO 14230-4 KWP (fast init)";
                case ObdProtocol.Can11Bit500K: return "CAN 500 kbps";
                case ObdProtocol.Can29Bit500K: return "CAN 500 kbps (29-bit)";
                case ObdProtocol.Can11Bit250K: return "CAN 250 kbps";
                case ObdProtocol.Can29Bit250K: return "CAN 250 kbps (29-bit)";
                default: return "Unknown";
            }
        }

        public static string ShortName(ObdProtocol p)
        {
            switch (p)
            {
                case ObdProtocol.J1850PWM: return "J1850 PWM";
                case ObdProtocol.J1850VPW: return "J1850 VPW";
                case ObdProtocol.ISO9141: return "ISO 9141-2";
                case ObdProtocol.KWP2000Slow:
                case ObdProtocol.KWP2000Fast: return "KWP2000";
                case ObdProtocol.Can11Bit500K: return "CAN 500 kbps";
                case ObdProtocol.Can29Bit500K: return "CAN 500 kbps";
                case ObdProtocol.Can11Bit250K: return "CAN 250 kbps";
                case ObdProtocol.Can29Bit250K: return "CAN 250 kbps";
                case ObdProtocol.Auto: return "Auto";
                default: return "—";
            }
        }

        public static bool IsCan(ObdProtocol p) => p >= ObdProtocol.Can11Bit500K && p <= ObdProtocol.Can29Bit250K;
        public static bool IsCan29(ObdProtocol p) => p == ObdProtocol.Can29Bit500K || p == ObdProtocol.Can29Bit250K;

        public static ObdProtocol FromElmDigit(char c)
        {
            switch (char.ToUpperInvariant(c))
            {
                case '1': return ObdProtocol.J1850PWM;
                case '2': return ObdProtocol.J1850VPW;
                case '3': return ObdProtocol.ISO9141;
                case '4': return ObdProtocol.KWP2000Slow;
                case '5': return ObdProtocol.KWP2000Fast;
                case '6': return ObdProtocol.Can11Bit500K;
                case '7': return ObdProtocol.Can29Bit500K;
                case '8': return ObdProtocol.Can11Bit250K;
                case '9': return ObdProtocol.Can29Bit250K;
                default: return ObdProtocol.Unknown;
            }
        }

        public static string AdapterName(AdapterType t)
        {
            switch (t)
            {
                case AdapterType.Elm327Usb: return Localization.Loc.T("adapter.elm327usb");
                case AdapterType.Elm327Bluetooth: return Localization.Loc.T("adapter.elm327bt");
                case AdapterType.Elm327WiFi: return Localization.Loc.T("adapter.elm327wifi");
                case AdapterType.ObdLink: return Localization.Loc.T("adapter.obdlink");
                default: return Localization.Loc.T("adapter.simulator");
            }
        }

        public static string AdapterShortName(AdapterType t)
        {
            switch (t)
            {
                case AdapterType.Elm327Usb: return "ELM327 USB";
                case AdapterType.Elm327Bluetooth: return "ELM327 BT";
                case AdapterType.Elm327WiFi: return "ELM327 WiFi";
                case AdapterType.ObdLink: return "OBDLink MX+";
                default: return "Simulator";
            }
        }
    }
}
