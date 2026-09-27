using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Controls;
using RedlineDiagnostics.Diagnostics;
using RedlineDiagnostics.Localization;

namespace RedlineDiagnostics.Forms.Pages
{
    public sealed class ReportsPage : BasePage
    {
        private readonly ThemeLabel _title;
        private readonly GlowPanel _card;
        private readonly ItemList _list;
        private readonly NeonButton _generate, _open, _folder, _delete;
        private readonly WebBrowser _preview;

        public override string TitleKey => "nav.reports";

        public ReportsPage()
        {
            _title = new ThemeLabel { TextKey = "report.title", FontSize = 18f, Bold = true, Bounds = new Rectangle(14, 10, 500, 36) };
            _generate = new NeonButton { TextKey = "report.generate", Icon = "reports", Bounds = new Rectangle(1016, 20, 180, 38) };
            _generate.Click += (s, e) => Generate();
            Controls.Add(_title);
            Controls.Add(_generate);

            _card = new GlowPanel { TitleKey = "report.title", TitleIcon = "reports", Bounds = new Rectangle(10, 70, 420, 632) };
            _list = new ItemList { Bounds = new Rectangle(8, 48, 404, 520), ItemHeight = 54, DrawItem = DrawItem, EmptyTextKey = "report.none" };
            _list.SelectionChanged += Preview;
            _list.ItemActivated += o => Open();
            _open = new NeonButton { TextKey = "report.open", Icon = "forward", Bounds = new Rectangle(12, 580, 120, 34), Filled = false, Accent = Theme.Cyan };
            _open.Click += (s, e) => Open();
            _folder = new NeonButton { TextKey = "report.openFolder", Icon = "folder", Bounds = new Rectangle(140, 580, 150, 34), Filled = false, Accent = Theme.Cyan };
            _folder.Click += (s, e) => { try { Process.Start("explorer.exe", ReportGenerator.ReportsDirectory); } catch { } };
            _delete = new NeonButton { TextKey = "report.delete", Icon = "trash", Bounds = new Rectangle(298, 580, 110, 34), Filled = false, Accent = Theme.Red };
            _delete.Click += (s, e) => Delete();
            _card.Controls.AddRange(new Control[] { _list, _open, _folder, _delete });
            Controls.Add(_card);

            _preview = new WebBrowser { Bounds = new Rectangle(440, 70, 756, 632), ScriptErrorsSuppressed = true, AllowNavigation = true };
            Controls.Add(_preview);

            AppState.Instance.ScanStateChanged += () => _generate.Enabled = !AppState.Instance.IsScanning;
            ApplyLocalization();
        }

        public override void OnShown() => RefreshList();

        private void RefreshList()
        {
            _list.SetItems(ReportGenerator.ListReports().Cast<object>(), true);
        }

        private void DrawItem(Graphics g, Rectangle r, object item, bool sel, bool hov)
        {
            var f0 = (FileInfo)item;
            Icons.Draw(g, "reports", new RectangleF(r.X, r.Y + 6, 20, 24), Theme.Cyan, 1.5f);
            using (var f = F(9.5f, true))
                Theme.DrawText(g, Path.GetFileNameWithoutExtension(f0.Name), f, Theme.Text, new Rectangle(r.X + 30, r.Y + 2, r.Width - 30, 20), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            using (var f = F(8.5f))
                Theme.DrawText(g, f0.LastWriteTime.ToString("yyyy-MM-dd HH:mm") + "  ·  " + (f0.Length / 1024) + " KB", f, Theme.TextMuted, new Point(r.X + 30, r.Y + 24));
        }

        private void Preview()
        {
            var f = _list.SelectedItem as FileInfo;
            try
            {
                if (f != null) _preview.Navigate(f.FullName);
                else _preview.DocumentText = "<html><body style='background:#0a0e1a'></body></html>";
            }
            catch { }
        }

        /// <summary>Generates a report for the last scan without any dialogs (used by the automated UI test).</summary>
        public string GenerateSilent()
        {
            var st = AppState.Instance;
            if (st.LastScan == null) return null;
            var path = ReportGenerator.Save(st.LastScan);
            RefreshList();
            var idx = _list.Items.ToList().FindIndex(o => ((FileInfo)o).FullName == path);
            if (idx >= 0) _list.SelectedIndex = idx;
            return path;
        }

        private void Generate()
        {
            var st = AppState.Instance;
            if (st.LastScan == null) { Info(Loc.T("report.noScan")); return; }
            try
            {
                var path = ReportGenerator.Save(st.LastScan);
                RefreshList();
                var idx = _list.Items.ToList().FindIndex(o => ((FileInfo)o).FullName == path);
                if (idx >= 0) _list.SelectedIndex = idx;
                Info(Loc.T("report.generated", Path.GetFileName(path)));
            }
            catch (Exception ex) { Error(ex.Message); }
        }

        private void Open()
        {
            var f = _list.SelectedItem as FileInfo;
            if (f == null) return;
            try { Process.Start(new ProcessStartInfo(f.FullName) { UseShellExecute = true }); }
            catch (Exception ex) { Error(ex.Message); }
        }

        private void Delete()
        {
            var f = _list.SelectedItem as FileInfo;
            if (f == null) return;
            if (Confirm(Loc.T("report.delete") + ": " + f.Name + "?") != DialogResult.Yes) return;
            try { f.Delete(); } catch (Exception ex) { Error(ex.Message); }
            RefreshList();
        }
    }
}
