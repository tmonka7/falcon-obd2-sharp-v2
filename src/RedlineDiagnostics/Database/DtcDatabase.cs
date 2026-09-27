using System;
using System.Collections.Generic;
using System.Linq;
using RedlineDiagnostics.Localization;

namespace RedlineDiagnostics.Database
{
    public sealed class DtcInfo
    {
        public string Code = "";
        public string En = "", Ja = "", Zh = "";
        public bool IsGeneric;
        public string Description => Loc.Pick(En, Ja, Zh);
        public char System => Code.Length > 0 ? Code[0] : 'P';
        public string SystemName => Loc.T("dtc.system." + System);
    }

    /// <summary>Diagnostic trouble code definitions in EN/JA/ZH with family fallbacks for unknown codes.</summary>
    public static class DtcDatabase
    {
        private static Dictionary<string, DtcInfo> _codes;

        public static void Load()
        {
            _codes = new Dictionary<string, DtcInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in Csv.Read("dtc_codes.csv"))
            {
                var code = Csv.Get(row, 0).ToUpperInvariant();
                if (code.Length != 5) continue;
                _codes[code] = new DtcInfo { Code = code, En = Csv.Get(row, 1), Ja = Csv.Get(row, 2), Zh = Csv.Get(row, 3) };
            }
        }

        public static int Count => _codes?.Count ?? 0;

        public static DtcInfo Lookup(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            DtcInfo info;
            if (_codes != null && _codes.TryGetValue(code, out info)) return info;
            return Fallback(code);
        }

        public static string Describe(string code) => Lookup(code).Description;

        public static IEnumerable<DtcInfo> Search(string text, int max = 50)
        {
            if (_codes == null) yield break;
            text = (text ?? "").Trim();
            int n = 0;
            foreach (var kv in _codes.OrderBy(k => k.Key))
            {
                var d = kv.Value;
                if (text.Length == 0 || kv.Key.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                    d.En.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.Ja.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.Zh.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    yield return d;
                    if (++n >= max) yield break;
                }
            }
        }

        private static DtcInfo Fallback(string code)
        {
            var info = new DtcInfo { Code = code, IsGeneric = true };
            if (code.Length != 5)
            {
                info.En = info.Ja = info.Zh = "";
                return info;
            }
            char sys = code[0];
            char d1 = code[1];
            string family = code.Substring(1, 2);
            string en = null, ja = null, zh = null;
            if (sys == 'P' && (d1 == '0' || d1 == '2' || d1 == '3'))
            {
                switch (family)
                {
                    case "00": case "01": case "02":
                        en = "Fuel and air metering"; ja = "燃料・空気計量系"; zh = "燃油和空气计量"; break;
                    case "03": en = "Ignition system or misfire"; ja = "点火系またはミスファイア"; zh = "点火系统或失火"; break;
                    case "04": en = "Auxiliary emission controls"; ja = "補助排出ガス制御"; zh = "辅助排放控制"; break;
                    case "05": en = "Vehicle speed control and idle control"; ja = "車速制御・アイドル制御"; zh = "车速控制和怠速控制"; break;
                    case "06": en = "Computer output circuit"; ja = "コンピューター出力回路"; zh = "计算机输出电路"; break;
                    case "07": case "08": case "09":
                        en = "Transmission"; ja = "トランスミッション"; zh = "变速箱"; break;
                    case "0A": case "0B": case "0C":
                        en = "Hybrid propulsion system"; ja = "ハイブリッド駆動システム"; zh = "混合动力推进系统"; break;
                    default:
                        if (d1 == '2') { en = "Fuel and air metering / emission control"; ja = "燃料・空気計量 / 排出ガス制御"; zh = "燃油空气计量 / 排放控制"; }
                        break;
                }
            }
            if (en == null)
            {
                bool mfr = d1 == '1' || d1 == '2' && sys != 'P' || d1 == '3' && sys != 'P';
                if (mfr)
                {
                    en = Loc.T("dtc.unknown"); ja = en; zh = en;
                    info.En = info.Ja = info.Zh = en;
                    return info;
                }
                switch (sys)
                {
                    case 'B': en = "Body system fault"; ja = "ボディ系の異常"; zh = "车身系统故障"; break;
                    case 'C': en = "Chassis system fault"; ja = "シャシー系の異常"; zh = "底盘系统故障"; break;
                    case 'U': en = "Network communication fault"; ja = "ネットワーク通信の異常"; zh = "网络通信故障"; break;
                    default: en = "Powertrain fault"; ja = "パワートレイン系の異常"; zh = "动力系统故障"; break;
                }
            }
            info.En = en + " (" + code + ")";
            info.Ja = ja + " (" + code + ")";
            info.Zh = zh + " (" + code + ")";
            return info;
        }
    }
}
