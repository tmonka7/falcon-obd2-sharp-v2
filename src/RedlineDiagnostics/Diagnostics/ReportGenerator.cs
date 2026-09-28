using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Database;
using RedlineDiagnostics.Localization;
using RedlineDiagnostics.Obd;

namespace RedlineDiagnostics.Diagnostics
{
    /// <summary>Builds a self-contained HTML diagnostic report in the current UI language.</summary>
    public static class ReportGenerator
    {
        public static string ReportsDirectory
        {
            get
            {
                var d = Path.Combine(Settings.DocumentsDirectory, "Reports");
                try { Directory.CreateDirectory(d); } catch { }
                return d;
            }
        }

        public static string Save(ScanResult r)
        {
            var path = Path.Combine(ReportsDirectory, "Report_" + r.Started.ToString("yyyyMMdd_HHmmss") + "_" + Loc.Code + ".html");
            File.WriteAllText(path, Html(r), Encoding.UTF8);
            r.ReportPath = path;
            return path;
        }

        public static List<FileInfo> ListReports()
        {
            try
            {
                return new DirectoryInfo(ReportsDirectory).GetFiles("*.html").OrderByDescending(f => f.LastWriteTime).ToList();
            }
            catch { return new List<FileInfo>(); }
        }

        private static string E(string s) => WebUtility.HtmlEncode(s ?? "");

        private static string StatusName(ModuleStatus s)
        {
            switch (s)
            {
                case ModuleStatus.Passed: return Loc.T("status.passed");
                case ModuleStatus.Warning: return Loc.T("status.warning");
                case ModuleStatus.Fault: return Loc.T("status.fault");
                case ModuleStatus.NoResponse: return Loc.T("status.noresponse");
                default: return Loc.T("status.pending");
            }
        }

        private static string StateName(DtcState s)
        {
            switch (s)
            {
                case DtcState.Pending: return Loc.T("dtc.pending");
                case DtcState.Permanent: return Loc.T("dtc.permanent");
                default: return Loc.T("dtc.stored");
            }
        }

        public static string Html(ScanResult r)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"").Append(Loc.Code).Append("\"><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\">");
            sb.Append("<title>").Append(E(Loc.T("report.heading"))).Append("</title>");
            sb.Append("<style>");
            sb.Append("body{font-family:'Segoe UI','Yu Gothic UI','Microsoft YaHei UI',sans-serif;background:#000000;color:#e8ecf5;margin:0;padding:32px;}");
            sb.Append("h1{font-size:26px;margin:0 0 4px 0;} h2{font-size:16px;color:#22d3ee;margin:28px 0 10px 0;text-transform:uppercase;letter-spacing:1px;}");
            sb.Append(".brand{color:#e5173c;font-weight:800;letter-spacing:2px;font-size:13px;}");
            sb.Append(".muted{color:#8b95ab;font-size:13px;}");
            sb.Append("table{border-collapse:collapse;width:100%;background:#0e0e10;border:1px solid #2a2a2f;border-radius:8px;overflow:hidden;}");
            sb.Append("th,td{padding:9px 12px;text-align:left;border-bottom:1px solid #1d1d21;font-size:13px;vertical-align:top;} th{color:#8b95ab;font-weight:600;background:#0a0a0c;}");
            sb.Append(".pill{display:inline-block;padding:2px 10px;border-radius:12px;font-size:12px;font-weight:600;}");
            sb.Append(".Passed{background:#14532d;color:#86efac} .Fault{background:#7f1d1d;color:#fca5a5} .Warning{background:#78350f;color:#fcd34d} .NoResponse{background:#1f2937;color:#9ca3af}");
            sb.Append(".summary{display:flex;gap:16px;margin-top:16px;} .card{flex:1;background:#0e0e10;border:1px solid #2a2a2f;border-radius:10px;padding:14px 18px;} .card .v{font-size:28px;font-weight:700;} .card .l{color:#8b95ab;font-size:12px;}");
            sb.Append(".code{font-family:Consolas,monospace;font-weight:700;color:#fff;}");
            sb.Append("</style></head><body>");
            sb.Append("<div class=\"brand\">REDLINE DIAGNOSTICS</div>");
            sb.Append("<h1>").Append(E(Loc.T("report.heading"))).Append("</h1>");
            sb.Append("<div class=\"muted\">").Append(E(Loc.T("common.vehicle"))).Append(": <b>").Append(E(r.VehicleName)).Append("</b> &nbsp;·&nbsp; VIN: <span class=\"code\">").Append(E(r.Vin)).Append("</span>");
            sb.Append(" &nbsp;·&nbsp; ").Append(E(Loc.T("report.generatedAt"))).Append(": ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.Append(" &nbsp;·&nbsp; ").Append(E(Loc.T("report.tool"))).Append(": ").Append(E(r.Adapter)).Append(" / ").Append(E(r.Protocol)).Append("</div>");

            sb.Append("<h2>").Append(E(Loc.T("report.summary"))).Append("</h2><div class=\"summary\">");
            Card(sb, r.Scanned.ToString(), Loc.T("common.modules"));
            Card(sb, r.Faults.ToString(), Loc.T("common.faults"));
            Card(sb, r.Warnings.ToString(), Loc.T("common.warnings"));
            Card(sb, r.TotalDtcs.ToString(), Loc.T("panel.dtcs"));
            Card(sb, r.Duration.ToString(@"mm\:ss"), Loc.T("history.duration"));
            sb.Append("</div>");

            sb.Append("<h2>").Append(E(Loc.T("report.moduleResults"))).Append("</h2><table><tr><th>").Append(E(Loc.T("scan.modules")))
              .Append("</th><th>").Append(E(Loc.T("panel.moduleAddress"))).Append("</th><th>").Append(E(Loc.T("panel.ecuId")))
              .Append("</th><th>").Append(E(Loc.T("panel.responseTime"))).Append("</th><th>").Append(E(Loc.T("common.status")))
              .Append("</th><th>").Append(E(Loc.T("panel.dtcs"))).Append("</th></tr>");
            foreach (var m in r.Modules)
            {
                var def = ModuleCatalog.ByShort(m.Short);
                var name = def != null ? def.Name : m.Name;
                sb.Append("<tr><td><b>").Append(E(m.Short)).Append("</b><br><span class=\"muted\">").Append(E(name)).Append("</span></td>");
                sb.Append("<td>").Append(E(m.Address)).Append("</td><td>").Append(E(m.EcuId)).Append("</td>");
                sb.Append("<td>").Append(m.ResponseMs >= 0 ? m.ResponseMs + " ms" : "—").Append("</td>");
                sb.Append("<td><span class=\"pill ").Append(m.ModuleStatus).Append("\">").Append(E(StatusName(m.ModuleStatus))).Append("</span></td><td>");
                if (m.Dtcs.Count == 0) sb.Append("<span class=\"muted\">").Append(E(Loc.T("panel.noDtcs"))).Append("</span>");
                foreach (var d in m.Dtcs)
                {
                    sb.Append("<div><span class=\"code\">").Append(E(d.Code)).Append("</span> <span class=\"muted\">[").Append(E(StateName(d.DtcState))).Append("]</span> ")
                      .Append(E(DtcDatabase.Describe(d.Code))).Append("</div>");
                }
                sb.Append("</td></tr>");
            }
            sb.Append("</table>");
            sb.Append("<p class=\"muted\" style=\"margin-top:30px\">Redline Diagnostics · ").Append(DateTime.Now.Year).Append("</p>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static void Card(StringBuilder sb, string value, string label)
        {
            sb.Append("<div class=\"card\"><div class=\"v\">").Append(E(value)).Append("</div><div class=\"l\">").Append(E(label)).Append("</div></div>");
        }
    }
}
