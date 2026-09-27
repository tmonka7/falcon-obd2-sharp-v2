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
    /// <summary>Vehicle identification, VIN decoding, readiness monitors and supported PIDs.</summary>
    public sealed class VehiclePage : BasePage
    {
        private readonly GlowPanel _profileCard, _ecuCard, _monCard, _pidCard;
        private readonly TextBox _vin, _make, _model, _year, _engine, _plate;
        private readonly NeonButton _save, _read;
        private readonly KeyValueList _ecuKv, _monKv;
        private readonly ItemList _pids;
        private readonly ThemeLabel _title;
        private PidDecoder.MonitorStatus _monitors;
        private List<int> _supported = new List<int>();

        public override string TitleKey => "nav.vehicle";

        public VehiclePage()
        {
            _title = new ThemeLabel { TextKey = "vehicle.title", FontSize = 18f, Bold = true, Bounds = new Rectangle(14, 10, 600, 36) };
            Controls.Add(_title);

            _profileCard = new GlowPanel { TitleKey = "garage.editTitle", TitleIcon = "vehicle", Bounds = new Rectangle(10, 60, 480, 330) };
            int y = 54;
            _vin = AddField(_profileCard, "vehicle.vin", ref y);
            _make = AddField(_profileCard, "vehicle.make", ref y);
            _model = AddField(_profileCard, "vehicle.model", ref y);
            _year = AddField(_profileCard, "vehicle.year", ref y);
            _engine = AddField(_profileCard, "vehicle.engine", ref y);
            _plate = AddField(_profileCard, "vehicle.plate", ref y);
            _save = new NeonButton { TextKey = "vehicle.save", Icon = "save", Bounds = new Rectangle(150, y + 6, 170, 34) };
            _save.Click += (s, e) => SaveProfile();
            _profileCard.Controls.Add(_save);

            _ecuCard = new GlowPanel { TitleKey = "panel.ecuId", TitleIcon = "engine", Bounds = new Rectangle(500, 60, 696, 330), Accent = Theme.Cyan };
            _ecuKv = new KeyValueList { Bounds = new Rectangle(16, 50, 500, 240), BackColor = Theme.Surface, RowHeight = 30 };
            _read = new NeonButton { TextKey = "vehicle.readFromEcu", Icon = "refresh", Bounds = new Rectangle(526, 52, 154, 34), Accent = Theme.Cyan };
            _read.Click += (s, e) => ReadFromEcu();
            _ecuCard.Controls.Add(_ecuKv);
            _ecuCard.Controls.Add(_read);

            _monCard = new GlowPanel { TitleKey = "vehicle.readiness", TitleIcon = "check", Bounds = new Rectangle(10, 400, 590, 302), Accent = Theme.Green };
            _monKv = new KeyValueList { Bounds = new Rectangle(16, 50, 558, 244), BackColor = Theme.Surface, RowHeight = 24 };
            _monCard.Controls.Add(_monKv);

            _pidCard = new GlowPanel { TitleKey = "vehicle.supportedPids", TitleIcon = "livedata", Bounds = new Rectangle(610, 400, 586, 302), Accent = Theme.Purple };
            _pids = new ItemList { Bounds = new Rectangle(8, 48, 570, 246), ItemHeight = 30, DrawItem = DrawPid, EmptyTextKey = "vehicle.na" };
            _pidCard.Controls.Add(_pids);

            Controls.AddRange(new Control[] { _profileCard, _ecuCard, _monCard, _pidCard });

            var st = AppState.Instance;
            st.VehicleChanged += RefreshPage;
            st.ConnectionChanged += RefreshPage;
            ApplyLocalization();
            RefreshPage();
        }

        private TextBox AddField(GlowPanel card, string key, ref int y)
        {
            var label = new ThemeLabel { TextKey = key, Bounds = new Rectangle(16, y, 130, 28), Color = Theme.TextMuted };
            var tb = Dark.TextBox(300);
            tb.Location = new Point(150, y + 1);
            card.Controls.Add(label);
            card.Controls.Add(tb);
            y += 38;
            return tb;
        }

        protected override void OnLocalize() => RefreshPage();

        public override void OnShown() => RefreshPage();

        private void RefreshPage()
        {
            var st = AppState.Instance;
            var v = st.ActiveVehicle;
            if (v != null && !_vin.Focused && !_make.Focused && !_model.Focused && !_year.Focused && !_engine.Focused && !_plate.Focused)
            {
                _vin.Text = v.Vin;
                _make.Text = v.Make;
                _model.Text = v.Model;
                _year.Text = v.Year > 0 ? v.Year.ToString() : "";
                _engine.Text = v.Engine;
                _plate.Text = v.Plate;
            }
            var info = st.VinInfo ?? (v != null && v.Vin.Length > 0 ? VinDecoder.Decode(v.Vin) : null);
            var rows = new List<KeyValueRow>
            {
                new KeyValueRow(Loc.T("vehicle.vin"), string.IsNullOrEmpty(st.CurrentVin) ? "—" : st.CurrentVin),
                new KeyValueRow(Loc.T("vehicle.make"), info?.Manufacturer ?? "—"),
                new KeyValueRow(Loc.T("vehicle.country"), info?.Country ?? "—"),
                new KeyValueRow(Loc.T("vehicle.year"), info != null && info.Year > 0 ? info.Year.ToString() : "—"),
                new KeyValueRow(Loc.T("vehicle.protocol"), st.IsConnected ? ObdProtocolInfo.Name(st.Adapter.Protocol) : "—"),
                new KeyValueRow(Loc.T("vehicle.battery"), st.BatteryVoltage.HasValue ? st.BatteryVoltage.Value.ToString("0.0") + " V" : "—"),
                new KeyValueRow(Loc.T("vehicle.mil"), _monitors == null ? "—" : (_monitors.MilOn ? Loc.T("vehicle.milOn") : Loc.T("vehicle.milOff")), _monitors == null ? Theme.Text : (_monitors.MilOn ? Theme.Red : Theme.Green)),
                new KeyValueRow(Loc.T("vehicle.dtcCount"), _monitors == null ? "—" : _monitors.DtcCount.ToString()),
            };
            _ecuKv.SetRows(rows);

            var mons = new List<KeyValueRow>();
            if (_monitors != null)
            {
                foreach (var kv in _monitors.Monitors)
                {
                    string val = kv.Value == null ? Loc.T("vehicle.na") : kv.Value.Value ? Loc.T("vehicle.ready") : Loc.T("vehicle.notReady");
                    Color c = kv.Value == null ? Theme.TextDim : kv.Value.Value ? Theme.Green : Theme.Orange;
                    mons.Add(new KeyValueRow(Loc.T(kv.Key), val, c, c));
                }
            }
            else
            {
                foreach (var key in new[] { "mon.misfire", "mon.fuel", "mon.comp", "mon.catalyst", "mon.heatedCat", "mon.evap", "mon.secAir", "mon.o2", "mon.o2Heater", "mon.egr" })
                    mons.Add(new KeyValueRow(Loc.T(key), "—", Theme.TextDim, Theme.TextDim));
            }
            _monKv.SetRows(mons);
            _pids.SetItems(_supported.Cast<object>());
            _read.Enabled = st.IsConnected && !st.IsScanning;
        }

        private void DrawPid(Graphics g, Rectangle r, object item, bool sel, bool hov)
        {
            int pid = (int)item;
            using (var f = new Font("Consolas", 9.5f, FontStyle.Bold))
                Theme.DrawText(g, "01 " + pid.ToString("X2"), f, Theme.Cyan, new Point(r.X, r.Y + 1));
            using (var f = F(9f))
                Theme.DrawText(g, PidDatabase.Name(pid), f, Theme.Text, new Rectangle(r.X + 70, r.Y, r.Width - 130, r.Height), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            var unit = PidDatabase.Get(pid)?.Unit ?? "";
            using (var f = F(8.5f))
                Theme.DrawText(g, unit, f, Theme.TextMuted, new Rectangle(r.X, r.Y, r.Width, r.Height), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        private void SaveProfile()
        {
            var st = AppState.Instance;
            var v = st.ActiveVehicle;
            if (v == null)
            {
                v = new VehicleProfile();
                st.Vehicles.Add(v);
            }
            v.Vin = _vin.Text.Trim().ToUpperInvariant();
            v.Make = _make.Text.Trim();
            v.Model = _model.Text.Trim();
            int year;
            v.Year = int.TryParse(_year.Text.Trim(), out year) ? year : 0;
            v.Engine = _engine.Text.Trim();
            v.Plate = _plate.Text.Trim();
            st.SaveVehicles();
            if (st.ActiveVehicle != v) st.SetActiveVehicle(v);
            RaiseTitleChanged();
        }

        public void ReadFromEcu()
        {
            var st = AppState.Instance;
            if (!st.IsConnected || st.IsScanning) return;
            st.StopMonitor();
            _read.Enabled = false;
            var adapter = st.Adapter;
            var ui = st.Ui;
            new Thread(() =>
            {
                PidDecoder.MonitorStatus mon = null;
                var supported = new List<int>();
                string err = null;
                try
                {
                    adapter.ClearTarget();
                    var r = adapter.Request("0101", 3000);
                    var p = r.Payload(0x41, 1);
                    if (p != null) mon = PidDecoder.DecodeMonitorStatus(p);
                    int basePid = 0x00;
                    while (basePid <= 0xC0)
                    {
                        var rr = adapter.Request("01" + basePid.ToString("X2"), 3000);
                        var mask = rr.Payload(0x41, 1);
                        if (mask == null) break;
                        var list = PidDecoder.SupportedPids(basePid, mask);
                        supported.AddRange(list);
                        if (!list.Contains(basePid + 0x20)) break;
                        basePid += 0x20;
                    }
                }
                catch (Exception ex) { err = ex.Message; }
                ui.Post(_ =>
                {
                    _monitors = mon;
                    _supported = supported.Distinct().OrderBy(x => x).ToList();
                    RefreshPage();
                    if (err != null) Error(err);
                }, null);
            }) { IsBackground = true }.Start();
        }
    }
}
