using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Controls;
using RedlineDiagnostics.Database;
using RedlineDiagnostics.Diagnostics;
using RedlineDiagnostics.Localization;
using RedlineDiagnostics.Obd;

namespace RedlineDiagnostics.Forms.Pages
{
    public sealed class HomePage : BasePage
    {
        private readonly GlowPanel _vehicleCard, _adapterCard, _statusCard, _actionsCard, _recentCard, _dbCard;
        private readonly KeyValueList _vehicleKv, _adapterKv, _statusKv;
        private readonly NeonButton _connectBtn, _scanBtn, _readBtn, _clearBtn, _liveBtn;
        private readonly ItemList _recent, _dbResults;
        private readonly TextBox _dbSearch;
        private readonly ThemeLabel _welcome, _welcomeSub;

        public override string TitleKey => "nav.home";

        public HomePage()
        {
            _welcome = new ThemeLabel { TextKey = "home.welcome", FontSize = 18f, Bold = true, Bounds = new Rectangle(14, 10, 600, 36) };
            _welcomeSub = new ThemeLabel { TextKey = "home.subtitle", FontSize = 10f, Color = Theme.TextMuted, Bounds = new Rectangle(15, 44, 600, 22) };
            Controls.Add(_welcome);
            Controls.Add(_welcomeSub);

            // Row 1: three cards
            _vehicleCard = new GlowPanel { TitleKey = "home.activeVehicle", TitleIcon = "vehicle", Bounds = new Rectangle(10, 76, 392, 190) };
            _vehicleKv = new KeyValueList { Bounds = new Rectangle(16, 50, 360, 130), BackColor = Theme.Surface };
            _vehicleCard.Controls.Add(_vehicleKv);

            _adapterCard = new GlowPanel { TitleKey = "home.adapter", TitleIcon = "plug", Bounds = new Rectangle(412, 76, 392, 190), Accent = Theme.Cyan };
            _adapterKv = new KeyValueList { Bounds = new Rectangle(16, 50, 360, 80), BackColor = Theme.Surface };
            _connectBtn = new NeonButton { TextKey = "home.connect", Icon = "plug", Bounds = new Rectangle(16, 140, 180, 34), Accent = Theme.Cyan };
            _connectBtn.Click += (s, e) => ToggleConnect();
            _adapterCard.Controls.Add(_adapterKv);
            _adapterCard.Controls.Add(_connectBtn);

            _statusCard = new GlowPanel { TitleKey = "home.systemStatus", TitleIcon = "diagnose", Bounds = new Rectangle(814, 76, 382, 190), Accent = Theme.Green };
            _statusKv = new KeyValueList { Bounds = new Rectangle(16, 50, 350, 130), BackColor = Theme.Surface };
            _statusCard.Controls.Add(_statusKv);

            // Row 2: quick actions
            _actionsCard = new GlowPanel { TitleKey = "home.quickActions", TitleIcon = "scan", Bounds = new Rectangle(10, 276, 1186, 110) };
            _scanBtn = new NeonButton { TextKey = "home.fullScan", Icon = "scan", Bounds = new Rectangle(16, 54, 270, 40), FontSize = 10f };
            _scanBtn.Click += (s, e) => { Navigate(1); };
            _readBtn = new NeonButton { TextKey = "home.readDtcs", Icon = "reports", Bounds = new Rectangle(302, 54, 270, 40), Filled = false, Accent = Theme.Cyan, FontSize = 10f };
            _readBtn.Click += (s, e) => ReadDtcs();
            _clearBtn = new NeonButton { TextKey = "home.clearDtcs", Icon = "erase", Bounds = new Rectangle(588, 54, 270, 40), Filled = false, Accent = Theme.Orange, FontSize = 10f };
            _clearBtn.Click += (s, e) => ClearDtcs();
            _liveBtn = new NeonButton { TextKey = "home.liveData", Icon = "livedata", Bounds = new Rectangle(874, 54, 296, 40), Filled = false, Accent = Theme.Green, FontSize = 10f };
            _liveBtn.Click += (s, e) => Navigate(2);
            _actionsCard.Controls.AddRange(new Control[] { _scanBtn, _readBtn, _clearBtn, _liveBtn });

            // Row 3: recent scans + database lookup
            _recentCard = new GlowPanel { TitleKey = "home.recentScans", TitleIcon = "history", Bounds = new Rectangle(10, 396, 700, 306) };
            _recent = new ItemList { Bounds = new Rectangle(8, 48, 684, 250), ItemHeight = 58, EmptyTextKey = "history.none", DrawItem = DrawRecent };
            _recent.ItemActivated += o => Navigate(5);
            _recentCard.Controls.Add(_recent);

            _dbCard = new GlowPanel { TitleKey = "common.dtcLookup", TitleIcon = "reports", Bounds = new Rectangle(720, 396, 476, 306), Accent = Theme.Purple };
            _dbSearch = Dark.TextBox(440);
            _dbSearch.Location = new Point(16, 52);
            _dbSearch.TextChanged += (s, e) => RefreshDb();
            _dbResults = new ItemList { Bounds = new Rectangle(8, 88, 460, 210), ItemHeight = 46, DrawItem = DrawDtc };
            _dbCard.Controls.Add(_dbSearch);
            _dbCard.Controls.Add(_dbResults);

            Controls.AddRange(new Control[] { _vehicleCard, _adapterCard, _statusCard, _actionsCard, _recentCard, _dbCard });

            var st = AppState.Instance;
            st.ConnectionChanged += RefreshAll;
            st.VehicleChanged += RefreshAll;
            st.ScanStateChanged += RefreshAll;
            st.HistoryChanged += RefreshAll;
            ApplyLocalization();
            RefreshAll();
        }

        protected override void OnLocalize()
        {
            RefreshAll();
        }

        public override void OnShown() => RefreshAll();

        private void RefreshAll()
        {
            var st = AppState.Instance;
            var v = st.ActiveVehicle;
            var vinInfo = st.VinInfo ?? (v != null && v.Vin.Length > 0 ? VinDecoder.Decode(v.Vin) : null);
            _vehicleKv.SetRows(new[]
            {
                new KeyValueRow(Loc.T("vehicle.make"), v?.Make ?? vinInfo?.Manufacturer ?? "—"),
                new KeyValueRow(Loc.T("vehicle.model"), string.IsNullOrEmpty(v?.Model) ? "—" : v.Model),
                new KeyValueRow(Loc.T("vehicle.year"), (v?.Year ?? vinInfo?.Year ?? 0) > 0 ? (v?.Year ?? vinInfo.Year).ToString() : "—"),
                new KeyValueRow(Loc.T("vehicle.engine"), string.IsNullOrEmpty(v?.Engine) ? "—" : v.Engine),
                new KeyValueRow(Loc.T("vehicle.vin"), string.IsNullOrEmpty(st.CurrentVin) ? "—" : st.CurrentVin),
            });

            string connText; Color connColor;
            switch (st.Connection)
            {
                case ConnectionState.Connected: connText = Loc.T("status.connected"); connColor = Theme.Green; break;
                case ConnectionState.Connecting: connText = Loc.T("status.connecting"); connColor = Theme.Orange; break;
                case ConnectionState.Error: connText = Loc.T("status.error"); connColor = Theme.Red; break;
                default: connText = Loc.T("status.disconnected"); connColor = Theme.Grey; break;
            }
            _adapterKv.SetRows(new[]
            {
                new KeyValueRow(Loc.T("settings.adapterType"), ObdProtocolInfo.AdapterName(st.Settings.Adapter)),
                new KeyValueRow(Loc.T("common.status"), connText, connColor, connColor),
                new KeyValueRow(Loc.T("settings.protocol"), st.IsConnected ? ObdProtocolInfo.ShortName(st.Adapter.Protocol) : (st.Connection == ConnectionState.Error ? st.ConnectionError : "—")),
            });
            _connectBtn.TextKey = st.IsConnected ? "home.disconnect" : "home.connect";
            _connectBtn.Accent = st.IsConnected ? Theme.Grey : Theme.Cyan;
            _connectBtn.Enabled = st.Connection != ConnectionState.Connecting;
            _connectBtn.ApplyLocalization();

            var last = st.LastScan;
            _statusKv.SetRows(new[]
            {
                new KeyValueRow(Loc.T("home.lastScan"), last != null ? last.Started.ToString("yyyy-MM-dd HH:mm") : Loc.T("home.never")),
                new KeyValueRow(Loc.T("common.faults"), last != null ? last.Faults.ToString() : "—", last != null && last.Faults > 0 ? Theme.Red : Theme.Text),
                new KeyValueRow(Loc.T("common.warnings"), last != null ? last.Warnings.ToString() : "—", last != null && last.Warnings > 0 ? Theme.Orange : Theme.Text),
                new KeyValueRow(Loc.T("vehicle.battery"), st.BatteryVoltage.HasValue ? st.BatteryVoltage.Value.ToString("0.0") + " V" : "—"),
                new KeyValueRow(Loc.T("home.database"), Loc.T("home.dbCodes", DtcDatabase.Count, PidDatabase.Count, ModuleCatalog.Count)),
            });

            _recent.SetItems(st.History.Take(20).Cast<object>());
            _scanBtn.Enabled = _readBtn.Enabled = _clearBtn.Enabled = _liveBtn.Enabled = true;
            RefreshDb();
        }

        private void RefreshDb()
        {
            var text = _dbSearch.Text.Trim();
            _dbResults.SetItems(text.Length == 0 ? new object[0] : DtcDatabase.Search(text, 40).Cast<object>());
        }

        private void DrawRecent(Graphics g, Rectangle r, object item, bool sel, bool hov)
        {
            var s = (ScanResult)item;
            var c = s.Faults > 0 ? Theme.Red : s.Warnings > 0 ? Theme.Orange : Theme.Green;
            Theme.GlowDot(g, new PointF(r.X + 10, r.Y + r.Height / 2f), 5, c, 3);
            using (var f = F(9.5f, true))
                Theme.DrawText(g, s.VehicleName + "  ·  " + s.Started.ToString("yyyy-MM-dd HH:mm"), f, Theme.Text, new Point(r.X + 28, r.Y + 2));
            using (var f = F(8.5f))
                Theme.DrawText(g, Loc.T("scan.summary", s.Scanned, s.Faults, s.Warnings) + (s.Cancelled ? "  (" + Loc.T("scan.cancelled") + ")" : ""), f, Theme.TextMuted, new Point(r.X + 28, r.Y + 24));
        }

        private void DrawDtc(Graphics g, Rectangle r, object item, bool sel, bool hov)
        {
            var d = (DtcInfo)item;
            var c = d.System == 'P' ? Theme.Red : d.System == 'C' ? Theme.Orange : d.System == 'B' ? Theme.Cyan : Theme.Purple;
            using (var f = new Font("Consolas", 10f, FontStyle.Bold))
                Theme.DrawText(g, d.Code, f, c, new Point(r.X, r.Y + 2));
            using (var f = F(8.5f))
                Theme.DrawText(g, d.Description, f, Theme.Text, new Rectangle(r.X + 70, r.Y, r.Width - 70, r.Height), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }

        private void ToggleConnect()
        {
            var st = AppState.Instance;
            if (st.IsConnected) st.Disconnect();
            else st.ConnectAsync();
        }

        private void ReadDtcs()
        {
            var st = AppState.Instance;
            if (!st.IsConnected) { Info(Loc.T("scan.notConnected")); return; }
            if (st.IsScanning) return;
            st.StopMonitor();
            _readBtn.Enabled = false;
            var ui = st.Ui;
            new Thread(() =>
            {
                string msg;
                try
                {
                    var list = st.ReadEcmDtcs();
                    if (list.Count == 0) msg = Loc.T("panel.noDtcs");
                    else msg = Loc.T("home.dtcsFound", list.Count) + "\n\n" + string.Join("\n", list.Select(d => d.Code + "  [" + Loc.T("dtc." + d.State.ToString().ToLowerInvariant()) + "]  " + DtcDatabase.Describe(d.Code)));
                }
                catch (Exception ex) { msg = ex.Message; }
                ui.Post(_ => { _readBtn.Enabled = true; Info(msg); }, null);
            }) { IsBackground = true }.Start();
        }

        private void ClearDtcs()
        {
            var st = AppState.Instance;
            if (!st.IsConnected) { Info(Loc.T("scan.notConnected")); return; }
            if (st.IsScanning) return;
            if (Confirm(Loc.T("home.clearConfirm")) != DialogResult.Yes) return;
            st.StopMonitor();
            _clearBtn.Enabled = false;
            var ui = st.Ui;
            new Thread(() =>
            {
                string msg;
                try { msg = st.ClearEcmDtcs() ? Loc.T("home.cleared") : Loc.T("status.noresponse"); }
                catch (Exception ex) { msg = ex.Message; }
                ui.Post(_ => { _clearBtn.Enabled = true; Info(msg); }, null);
            }) { IsBackground = true }.Start();
        }
    }
}
