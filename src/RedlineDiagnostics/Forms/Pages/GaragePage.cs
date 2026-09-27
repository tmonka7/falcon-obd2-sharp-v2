using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Controls;
using RedlineDiagnostics.Database;
using RedlineDiagnostics.Diagnostics;
using RedlineDiagnostics.Localization;

namespace RedlineDiagnostics.Forms.Pages
{
    public sealed class GaragePage : BasePage
    {
        private readonly ThemeLabel _title, _sub;
        private readonly GlowPanel _listCard, _editCard;
        private readonly ItemList _list;
        private readonly TextBox _vin, _make, _model, _year, _engine, _plate, _notes;
        private readonly NeonButton _add, _save, _remove, _activate;
        private VehicleProfile _editing;

        public override string TitleKey => "nav.garage";

        public GaragePage()
        {
            _title = new ThemeLabel { TextKey = "garage.title", FontSize = 18f, Bold = true, Bounds = new Rectangle(14, 10, 500, 36) };
            _sub = new ThemeLabel { TextKey = "garage.subtitle", FontSize = 10f, Color = Theme.TextMuted, Bounds = new Rectangle(15, 44, 500, 22) };
            _add = new NeonButton { TextKey = "garage.add", Icon = "plus", Bounds = new Rectangle(1016, 20, 180, 38) };
            _add.Click += (s, e) => { _editing = new VehicleProfile(); LoadEditor(); _list.SelectedIndex = -1; };
            Controls.AddRange(new Control[] { _title, _sub, _add });

            _listCard = new GlowPanel { TitleKey = "garage.subtitle", TitleIcon = "garage", Bounds = new Rectangle(10, 76, 560, 626) };
            _list = new ItemList { Bounds = new Rectangle(8, 48, 544, 570), ItemHeight = 74, DrawItem = DrawVehicle, EmptyTextKey = "garage.none" };
            _list.SelectionChanged += () => { var v = _list.SelectedItem as VehicleProfile; if (v != null) { _editing = v; LoadEditor(); } };
            _listCard.Controls.Add(_list);

            _editCard = new GlowPanel { TitleKey = "garage.editTitle", TitleIcon = "vehicle", Bounds = new Rectangle(580, 76, 616, 626), Accent = Theme.Cyan };
            int y = 56;
            _vin = Field("vehicle.vin", ref y);
            _make = Field("vehicle.make", ref y);
            _model = Field("vehicle.model", ref y);
            _year = Field("vehicle.year", ref y);
            _engine = Field("vehicle.engine", ref y);
            _plate = Field("vehicle.plate", ref y);
            _notes = Field("common.description", ref y);
            _vin.TextChanged += (s, e) => AutoFillFromVin();
            _save = new NeonButton { TextKey = "vehicle.save", Icon = "save", Bounds = new Rectangle(160, y + 12, 150, 36) };
            _save.Click += (s, e) => Save();
            _activate = new NeonButton { TextKey = "garage.select", Icon = "check", Bounds = new Rectangle(320, y + 12, 140, 36), Filled = false, Accent = Theme.Green };
            _activate.Click += (s, e) => { if (_editing != null && AppState.Instance.Vehicles.Contains(_editing)) { AppState.Instance.SetActiveVehicle(_editing); RefreshList(); RaiseTitleChanged(); } };
            _remove = new NeonButton { TextKey = "garage.remove", Icon = "trash", Bounds = new Rectangle(470, y + 12, 130, 36), Filled = false, Accent = Theme.Red };
            _remove.Click += (s, e) => Remove();
            _editCard.Controls.AddRange(new Control[] { _save, _activate, _remove });
            Controls.Add(_listCard);
            Controls.Add(_editCard);

            AppState.Instance.VehicleChanged += RefreshList;
            ApplyLocalization();
            RefreshList();
        }

        private TextBox Field(string key, ref int y)
        {
            var label = new ThemeLabel { TextKey = key, Bounds = new Rectangle(16, y, 140, 28), Color = Theme.TextMuted };
            var tb = Dark.TextBox(430);
            tb.Location = new Point(160, y + 1);
            _editCard.Controls.Add(label);
            _editCard.Controls.Add(tb);
            y += 40;
            return tb;
        }

        public override void OnShown() => RefreshList();

        private void RefreshList()
        {
            var st = AppState.Instance;
            _list.SetItems(st.Vehicles.Cast<object>(), true);
            if (_editing == null || !st.Vehicles.Contains(_editing))
            {
                _editing = st.ActiveVehicle ?? st.Vehicles.FirstOrDefault();
                int idx = _editing != null ? st.Vehicles.IndexOf(_editing) : -1;
                _list.SelectedIndex = idx;
            }
            LoadEditor();
        }

        private void LoadEditor()
        {
            var v = _editing;
            _vin.Text = v?.Vin ?? "";
            _make.Text = v?.Make ?? "";
            _model.Text = v?.Model ?? "";
            _year.Text = v != null && v.Year > 0 ? v.Year.ToString() : "";
            _engine.Text = v?.Engine ?? "";
            _plate.Text = v?.Plate ?? "";
            _notes.Text = v?.Notes ?? "";
            bool saved = v != null && AppState.Instance.Vehicles.Contains(v);
            _remove.Enabled = saved;
            _activate.Enabled = saved && AppState.Instance.ActiveVehicle != v;
        }

        private void AutoFillFromVin()
        {
            var vin = _vin.Text.Trim();
            if (vin.Length < 11) return;
            var info = VinDecoder.Decode(vin);
            if (_make.Text.Length == 0 && info.Manufacturer.Length > 0) _make.Text = info.Manufacturer;
            if (_year.Text.Length == 0 && info.Year > 0) _year.Text = info.Year.ToString();
        }

        private void DrawVehicle(Graphics g, Rectangle r, object item, bool sel, bool hov)
        {
            var v = (VehicleProfile)item;
            bool active = AppState.Instance.ActiveVehicle == v;
            Icons.Badge(g, "vehicle", new RectangleF(r.X, r.Y + 6, 44, 44), active ? Theme.Green : Theme.Cyan, 8);
            using (var f = F(11f, true))
                Theme.DrawText(g, v.DisplayName, f, Theme.Text, new Rectangle(r.X + 58, r.Y + 4, r.Width - 160, 22), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            using (var f = F(8.5f))
            {
                Theme.DrawText(g, "VIN: " + (v.Vin.Length > 0 ? v.Vin : "—") + (v.Engine.Length > 0 ? "   ·   " + v.Engine : ""), f, Theme.TextMuted, new Rectangle(r.X + 58, r.Y + 28, r.Width - 60, 18), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                if (v.Plate.Length > 0) Theme.DrawText(g, v.Plate, f, Theme.TextMuted, new Point(r.X + 58, r.Y + 46));
            }
            if (active)
            {
                using (var f = F(8f, true))
                {
                    var t = Loc.T("garage.active").ToUpperInvariant();
                    var sz = Theme.Measure(t, f);
                    var pill = new RectangleF(r.Right - sz.Width - 18, r.Y + 8, sz.Width + 16, 20);
                    Theme.FillRounded(g, pill, 10, Theme.WithAlpha(Theme.Green, 40));
                    Theme.DrawRounded(g, pill, 10, Theme.Green);
                    Theme.DrawText(g, t, f, Theme.Green, Rectangle.Round(pill), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        private void Save()
        {
            var st = AppState.Instance;
            if (_editing == null) _editing = new VehicleProfile();
            _editing.Vin = _vin.Text.Trim().ToUpperInvariant();
            _editing.Make = _make.Text.Trim();
            _editing.Model = _model.Text.Trim();
            int year;
            _editing.Year = int.TryParse(_year.Text.Trim(), out year) ? year : 0;
            _editing.Engine = _engine.Text.Trim();
            _editing.Plate = _plate.Text.Trim();
            _editing.Notes = _notes.Text.Trim();
            if (!st.Vehicles.Contains(_editing)) st.Vehicles.Add(_editing);
            st.SaveVehicles();
            if (st.ActiveVehicle == null) st.SetActiveVehicle(_editing);
            RefreshList();
            RaiseTitleChanged();
        }

        private void Remove()
        {
            var st = AppState.Instance;
            if (_editing == null || !st.Vehicles.Contains(_editing)) return;
            if (Confirm(Loc.T("garage.removeConfirm", _editing.DisplayName)) != DialogResult.Yes) return;
            st.Vehicles.Remove(_editing);
            if (st.ActiveVehicle == _editing) st.SetActiveVehicle(st.Vehicles.FirstOrDefault());
            st.SaveVehicles();
            _editing = null;
            RefreshList();
            RaiseTitleChanged();
        }
    }
}
