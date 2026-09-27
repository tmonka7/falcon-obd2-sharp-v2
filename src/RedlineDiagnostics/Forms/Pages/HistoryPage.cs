using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Controls;
using RedlineDiagnostics.Database;
using RedlineDiagnostics.Diagnostics;
using RedlineDiagnostics.Localization;
using RedlineDiagnostics.Obd;

namespace RedlineDiagnostics.Forms.Pages
{
    public sealed class HistoryPage : BasePage
    {
        private readonly ThemeLabel _title;
        private readonly GlowPanel _listCard, _detailCard;
        private readonly ItemList _list, _modules;
        private readonly KeyValueList _summary;
        private readonly NeonButton _clear, _report;

        public override string TitleKey => "nav.history";

        public HistoryPage()
        {
            _title = new ThemeLabel { TextKey = "history.title", FontSize = 18f, Bold = true, Bounds = new Rectangle(14, 10, 500, 36) };
            _clear = new NeonButton { TextKey = "history.clear", Icon = "trash", Bounds = new Rectangle(1016, 20, 180, 38), Filled = false, Accent = Theme.Red };
            _clear.Click += (s, e) => { if (Confirm(Loc.T("history.clearConfirm")) == DialogResult.Yes) AppState.Instance.ClearHistory(); };
            Controls.Add(_title);
            Controls.Add(_clear);

            _listCard = new GlowPanel { TitleKey = "home.recentScans", TitleIcon = "history", Bounds = new Rectangle(10, 70, 440, 632) };
            _list = new ItemList { Bounds = new Rectangle(8, 48, 424, 576), ItemHeight = 64, DrawItem = DrawScan, EmptyTextKey = "history.none" };
            _list.SelectionChanged += ShowDetails;
            _listCard.Controls.Add(_list);

            _detailCard = new GlowPanel { TitleKey = "history.details", TitleIcon = "reports", Bounds = new Rectangle(460, 70, 736, 632), Accent = Theme.Cyan };
            _summary = new KeyValueList { Bounds = new Rectangle(16, 50, 704, 156), BackColor = Theme.Surface };
            _modules = new ItemList { Bounds = new Rectangle(8, 214, 720, 370), ItemHeight = 46, DrawItem = DrawModule };
            _report = new NeonButton { TextKey = "report.generate", Icon = "reports", Bounds = new Rectangle(16, 590, 180, 34) };
            _report.Click += (s, e) => GenerateReport();
            _detailCard.Controls.AddRange(new Control[] { _summary, _modules, _report });
            Controls.Add(_detailCard);
            Controls.Add(_listCard);

            AppState.Instance.HistoryChanged += RefreshList;
            ApplyLocalization();
        }

        protected override void OnLocalize() => ShowDetails();

        public override void OnShown() => RefreshList();

        private void RefreshList()
        {
            _list.SetItems(AppState.Instance.History.Cast<object>(), true);
            if (_list.SelectedIndex < 0 && _list.Items.Count > 0) _list.SelectedIndex = 0;
            ShowDetails();
        }

        private void ShowDetails()
        {
            var s = _list.SelectedItem as ScanResult;
            if (s == null)
            {
                _summary.SetRows(null);
                _modules.SetItems(null);
                _report.Enabled = false;
                return;
            }
            _report.Enabled = true;
            _summary.SetRows(new[]
            {
                new KeyValueRow(Loc.T("common.vehicle"), s.VehicleName + (s.Vin.Length > 0 ? "  ·  " + s.Vin : "")),
                new KeyValueRow(Loc.T("common.date"), s.Started.ToString("yyyy-MM-dd HH:mm:ss")),
                new KeyValueRow(Loc.T("history.duration"), s.Duration.ToString(@"mm\:ss") + (s.Cancelled ? "  (" + Loc.T("scan.cancelled") + ")" : "")),
                new KeyValueRow(Loc.T("common.modules"), s.Scanned.ToString()),
                new KeyValueRow(Loc.T("common.faults"), s.Faults.ToString(), s.Faults > 0 ? Theme.Red : Theme.Text),
                new KeyValueRow(Loc.T("common.warnings"), s.Warnings.ToString(), s.Warnings > 0 ? Theme.Orange : Theme.Text),
            });
            var rows = new List<object>();
            foreach (var m in s.Modules)
            {
                rows.Add(m);
                foreach (var d in m.Dtcs) rows.Add(d);
            }
            _modules.SetItems(rows);
        }

        private void DrawScan(Graphics g, Rectangle r, object item, bool sel, bool hov)
        {
            var s = (ScanResult)item;
            var c = s.Faults > 0 ? Theme.Red : s.Warnings > 0 ? Theme.Orange : Theme.Green;
            Theme.GlowDot(g, new PointF(r.X + 10, r.Y + r.Height / 2f), 5, c, 3);
            using (var f = F(9.5f, true))
                Theme.DrawText(g, s.Started.ToString("yyyy-MM-dd HH:mm"), f, Theme.Text, new Point(r.X + 28, r.Y + 2));
            using (var f = F(8.5f))
            {
                Theme.DrawText(g, s.VehicleName, f, Theme.TextMuted, new Rectangle(r.X + 28, r.Y + 22, r.Width - 28, 18), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                Theme.DrawText(g, Loc.T("scan.summary", s.Scanned, s.Faults, s.Warnings), f, c, new Point(r.X + 28, r.Y + 38));
            }
        }

        private void DrawModule(Graphics g, Rectangle r, object item, bool sel, bool hov)
        {
            var m = item as ModuleResult;
            if (m != null)
            {
                var c = Theme.StatusColor(m.ModuleStatus);
                Icons.StatusDot(g, new RectangleF(r.X, r.Y + 8, 16, 16), m.ModuleStatus);
                var def = ModuleCatalog.ByShort(m.Short);
                using (var f = F(9.5f, true))
                    Theme.DrawText(g, m.Short + "  ·  " + (def != null ? def.Name : m.Name), f, Theme.Text, new Rectangle(r.X + 26, r.Y, 400, r.Height), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                using (var f = F(8.5f))
                {
                    var txt = m.Address + "   " + m.EcuId + "   " + (m.ResponseMs >= 0 ? m.ResponseMs + " ms" : "");
                    Theme.DrawText(g, txt, f, Theme.TextMuted, new Rectangle(r.X, r.Y, r.Width, r.Height), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }
                return;
            }
            var d = item as DtcEntry;
            if (d != null)
            {
                var c = d.DtcState == DtcState.Pending ? Theme.Orange : Theme.Red;
                using (var f = new Font("Consolas", 9.5f, FontStyle.Bold))
                    Theme.DrawText(g, d.Code, f, c, new Point(r.X + 40, r.Y + 6));
                using (var f = F(8.5f))
                    Theme.DrawText(g, "[" + Loc.T("dtc." + d.DtcState.ToString().ToLowerInvariant()) + "]  " + DtcDatabase.Describe(d.Code), f, Theme.Text, new Rectangle(r.X + 110, r.Y, r.Width - 110, r.Height), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private void GenerateReport()
        {
            var s = _list.SelectedItem as ScanResult;
            if (s == null) return;
            try
            {
                var path = ReportGenerator.Save(s);
                Info(Loc.T("report.generated", System.IO.Path.GetFileName(path)));
            }
            catch (Exception ex) { Error(ex.Message); }
        }
    }
}
