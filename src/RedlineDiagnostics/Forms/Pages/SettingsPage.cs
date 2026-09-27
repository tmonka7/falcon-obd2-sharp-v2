using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Controls;
using RedlineDiagnostics.Localization;
using RedlineDiagnostics.Obd;

namespace RedlineDiagnostics.Forms.Pages
{
    public sealed class SettingsPage : BasePage
    {
        private readonly ThemeLabel _title;
        private readonly GlowPanel _langCard, _connCard, _dispCard, _aboutCard;
        private readonly ComboBox _adapter, _port, _baud, _protocol;
        private readonly TextBox _host, _model;
        private readonly NumericUpDown _tcpPort, _sampleMs;
        private readonly ToggleSwitch _metric, _autoRotate, _harness, _fullscreen, _modelFlip;
        private readonly ComboBox _modelCombo;
        private readonly ThemeLabel _modelInfo;
        private string[] _bundled = new string[0];
        private readonly NeonButton _save, _refreshPorts, _test, _browse, _exit;
        private readonly NeonButton[] _langButtons = new NeonButton[3];
        private readonly ThemeLabel _hint, _about, _portLabel, _hostLabel;
        private readonly ItemList _trace;
        private readonly System.Windows.Forms.Timer _traceTimer = new System.Windows.Forms.Timer { Interval = 700 };

        public override string TitleKey => "nav.settings";
        public event Action ExitRequested;
        public event Action FullScreenChanged;

        public SettingsPage()
        {
            _title = new ThemeLabel { TextKey = "settings.title", FontSize = 18f, Bold = true, Bounds = new Rectangle(14, 10, 500, 36) };
            _save = new NeonButton { TextKey = "settings.save", Icon = "save", Bounds = new Rectangle(1016, 20, 180, 38) };
            _save.Click += (s, e) => Save();
            Controls.Add(_title);
            Controls.Add(_save);

            // ---- language ----
            _langCard = new GlowPanel { TitleKey = "settings.language", TitleIcon = "settings", Bounds = new Rectangle(10, 70, 380, 120) };
            string[] labels = { "English", "日本語", "中文" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var b = new NeonButton { Text = labels[i], Bounds = new Rectangle(16 + i * 118, 56, 110, 40), Filled = false, Accent = Theme.Red, FontSize = 10f };
                b.Click += (s, e) => { Loc.Current = (Language)idx; };
                _langButtons[i] = b;
                _langCard.Controls.Add(b);
            }

            // ---- display ----
            _dispCard = new GlowPanel { TitleKey = "settings.display", TitleIcon = "cube", Bounds = new Rectangle(10, 200, 380, 420), Accent = Theme.Cyan };
            int y = 54;
            _autoRotate = Toggle(_dispCard, "settings.autoRotate", ref y);
            _harness = Toggle(_dispCard, "settings.showHarness", ref y);
            _fullscreen = Toggle(_dispCard, "settings.fullscreen", ref y);
            _metric = Toggle(_dispCard, "settings.metric", ref y);
            _modelFlip = Toggle(_dispCard, "settings.modelFlip", ref y);
            _dispCard.Controls.Add(new ThemeLabel { TextKey = "settings.model", Bounds = new Rectangle(16, y, 340, 24), Color = Theme.TextMuted });
            y += 26;
            _modelCombo = Dark.Combo(346);
            _modelCombo.Location = new Point(16, y);
            _modelCombo.SelectedIndexChanged += (s, e) => OnModelComboChanged();
            _dispCard.Controls.Add(_modelCombo);
            y += 34;
            _model = Dark.TextBox(240);
            _model.Location = new Point(16, y);
            _browse = new NeonButton { TextKey = "settings.browse", Bounds = new Rectangle(262, y - 3, 100, 32), Filled = false, Accent = Theme.Cyan, FontSize = 9f };
            _browse.Click += (s, e) => BrowseModel();
            _dispCard.Controls.Add(_model);
            _dispCard.Controls.Add(_browse);
            y += 36;
            _modelInfo = new ThemeLabel { Bounds = new Rectangle(16, y, 346, 40), Color = Theme.TextMuted, FontSize = 8.5f, Wrap = true, Align = ContentAlignment.TopLeft };
            _dispCard.Controls.Add(_modelInfo);
            AppState.Instance.MeshChanged += UpdateModelInfo;

            // ---- connection ----
            _connCard = new GlowPanel { TitleKey = "settings.connection", TitleIcon = "plug", Bounds = new Rectangle(400, 70, 400, 430), Accent = Theme.Green };
            y = 54;
            _adapter = Combo(_connCard, "settings.adapterType", ref y);
            foreach (AdapterType t in Enum.GetValues(typeof(AdapterType))) _adapter.Items.Add(new AdapterItem(t));
            _adapter.SelectedIndexChanged += (s, e) => UpdateConnVisibility();
            _portLabel = new ThemeLabel { TextKey = "settings.port", Bounds = new Rectangle(16, y, 150, 28), Color = Theme.TextMuted };
            _port = Dark.Combo(150);
            _port.Location = new Point(170, y + 1);
            _refreshPorts = new NeonButton { TextKey = "settings.refreshPorts", Bounds = new Rectangle(326, y - 2, 60, 30), Filled = false, Accent = Theme.Cyan, FontSize = 8.5f };
            _refreshPorts.Click += (s, e) => RefreshPorts();
            _connCard.Controls.AddRange(new Control[] { _portLabel, _port, _refreshPorts });
            y += 40;
            _baud = Combo(_connCard, "settings.baud", ref y);
            foreach (var b in new[] { 9600, 19200, 38400, 57600, 115200, 230400, 500000 }) _baud.Items.Add(b);
            _hostLabel = new ThemeLabel { TextKey = "settings.host", Bounds = new Rectangle(16, y, 150, 28), Color = Theme.TextMuted };
            _host = Dark.TextBox(210);
            _host.Location = new Point(170, y + 1);
            _connCard.Controls.Add(_hostLabel);
            _connCard.Controls.Add(_host);
            y += 40;
            _connCard.Controls.Add(new ThemeLabel { TextKey = "settings.tcpPort", Bounds = new Rectangle(16, y, 150, 28), Color = Theme.TextMuted });
            _tcpPort = Dark.Numeric(1, 65535, 35000, 110);
            _tcpPort.Location = new Point(170, y + 1);
            _connCard.Controls.Add(_tcpPort);
            y += 40;
            _protocol = Combo(_connCard, "settings.protocol", ref y);
            foreach (ObdProtocol p in Enum.GetValues(typeof(ObdProtocol)))
                if (p != ObdProtocol.Unknown) _protocol.Items.Add(new ProtocolItem(p));
            _connCard.Controls.Add(new ThemeLabel { TextKey = "live.rate", Bounds = new Rectangle(16, y, 150, 28), Color = Theme.TextMuted });
            _sampleMs = Dark.Numeric(150, 5000, 500, 110);
            _sampleMs.Location = new Point(170, y + 1);
            _connCard.Controls.Add(_sampleMs);
            y += 44;
            _hint = new ThemeLabel { TextKey = "settings.simulatorHint", Bounds = new Rectangle(16, y, 368, 44), Color = Theme.TextMuted, FontSize = 8.5f, Wrap = true, Align = ContentAlignment.TopLeft };
            _connCard.Controls.Add(_hint);
            y += 48;
            _test = new NeonButton { TextKey = "settings.testConnection", Icon = "plug", Bounds = new Rectangle(16, y, 200, 36), Accent = Theme.Green };
            _test.Click += (s, e) => TestConnection();
            _connCard.Controls.Add(_test);

            // ---- about / trace ----
            _aboutCard = new GlowPanel { TitleKey = "settings.about", TitleIcon = "reports", Bounds = new Rectangle(810, 70, 386, 430), Accent = Theme.Purple };
            _about = new ThemeLabel { Bounds = new Rectangle(16, 50, 354, 44), Color = Theme.TextMuted, FontSize = 8.5f, Wrap = true, Align = ContentAlignment.TopLeft };
            _trace = new ItemList { Bounds = new Rectangle(8, 98, 370, 280), ItemHeight = 22, DrawItem = DrawTrace };
            _exit = new NeonButton { TextKey = "settings.exit", Icon = "cross", Bounds = new Rectangle(16, 384, 180, 34), Filled = false, Accent = Theme.Red };
            _exit.Click += (s, e) => ExitRequested?.Invoke();
            _aboutCard.Controls.AddRange(new Control[] { _about, _trace, _exit });

            Controls.AddRange(new Control[] { _langCard, _dispCard, _connCard, _aboutCard });
            _traceTimer.Tick += (s, e) => { if (Visible) RefreshTrace(); };
            Loc.LanguageChanged += UpdateLangButtons;
            ApplyLocalization();
            LoadFromSettings();
        }

        private sealed class AdapterItem
        {
            public AdapterType Type;
            public AdapterItem(AdapterType t) { Type = t; }
            public override string ToString() => ObdProtocolInfo.AdapterName(Type);
        }

        private sealed class ProtocolItem
        {
            public ObdProtocol Protocol;
            public ProtocolItem(ObdProtocol p) { Protocol = p; }
            public override string ToString() => Protocol == ObdProtocol.Auto ? Loc.T("settings.protocolAuto") : ObdProtocolInfo.Name(Protocol);
        }

        private ComboBox Combo(GlowPanel card, string key, ref int y)
        {
            card.Controls.Add(new ThemeLabel { TextKey = key, Bounds = new Rectangle(16, y, 150, 28), Color = Theme.TextMuted });
            var cb = Dark.Combo(210);
            cb.Location = new Point(170, y + 1);
            card.Controls.Add(cb);
            y += 40;
            return cb;
        }

        private ToggleSwitch Toggle(GlowPanel card, string key, ref int y)
        {
            card.Controls.Add(new ThemeLabel { TextKey = key, Bounds = new Rectangle(16, y, 280, 28), Color = Theme.Text });
            var t = new ToggleSwitch { Location = new Point(316, y + 2) };
            card.Controls.Add(t);
            y += 38;
            return t;
        }

        protected override void OnLocalize()
        {
            _about.Text = Loc.T("settings.aboutText", "1.0.0");
            UpdateLangButtons();
            // refresh combo display strings
            int ai = _adapter?.SelectedIndex ?? -1, pi = _protocol?.SelectedIndex ?? -1;
            if (_adapter != null)
            {
                var items = _adapter.Items.Cast<object>().ToArray();
                _adapter.Items.Clear(); _adapter.Items.AddRange(items); _adapter.SelectedIndex = ai;
            }
            if (_protocol != null)
            {
                var items = _protocol.Items.Cast<object>().ToArray();
                _protocol.Items.Clear(); _protocol.Items.AddRange(items); _protocol.SelectedIndex = pi;
            }
        }

        private void UpdateLangButtons()
        {
            for (int i = 0; i < 3; i++)
            {
                if (_langButtons[i] == null) continue;
                _langButtons[i].Filled = (int)Loc.Current == i;
                _langButtons[i].Invalidate();
            }
        }

        public override void OnShown()
        {
            LoadFromSettings();
            RefreshTrace();
            _traceTimer.Start();
        }

        public override void OnHidden() => _traceTimer.Stop();

        private void LoadFromSettings()
        {
            var s = AppState.Instance.Settings;
            _adapter.SelectedIndex = (int)s.Adapter;
            RefreshPorts();
            if (!string.IsNullOrEmpty(s.SerialPort))
            {
                if (!_port.Items.Contains(s.SerialPort)) _port.Items.Add(s.SerialPort);
                _port.SelectedItem = s.SerialPort;
            }
            _baud.SelectedItem = _baud.Items.Contains(s.BaudRate) ? (object)s.BaudRate : 38400;
            _host.Text = s.Host;
            _tcpPort.Value = Math.Max(1, Math.Min(65535, s.TcpPort));
            _protocol.SelectedIndex = Math.Max(0, _protocol.Items.Cast<ProtocolItem>().ToList().FindIndex(p => (int)p.Protocol == s.Protocol));
            _sampleMs.Value = Math.Max(150, Math.Min(5000, s.LiveSampleMs));
            _metric.Checked = s.Metric;
            _autoRotate.Checked = s.AutoRotate;
            _harness.Checked = s.ShowHarness;
            _fullscreen.Checked = s.FullScreen;
            _modelFlip.Checked = s.ModelFlip;
            PopulateModelCombo(s.ModelPath);
            UpdateConnVisibility();
            UpdateLangButtons();
            UpdateModelInfo();
        }

        private sealed class ModelItem
        {
            public string Path;     // "" = default bundled, AppState.BuiltInModel, full path, or null for custom
            public string Label;
            public override string ToString() => Label;
        }

        private void PopulateModelCombo(string current)
        {
            _bundled = AppState.BundledModels();
            _modelCombo.Items.Clear();
            _modelCombo.Items.Add(new ModelItem { Path = AppState.BuiltInModel, Label = Loc.T("settings.modelBuiltIn") });
            foreach (var f in _bundled)
                _modelCombo.Items.Add(new ModelItem { Path = f, Label = Loc.T("settings.modelBundled") + ": " + System.IO.Path.GetFileName(f) });
            _modelCombo.Items.Add(new ModelItem { Path = null, Label = Loc.T("settings.modelCustom") });

            string effective = string.IsNullOrWhiteSpace(current) ? AppState.DefaultModelPath : current;
            int idx = -1;
            for (int i = 0; i < _modelCombo.Items.Count; i++)
            {
                var it = (ModelItem)_modelCombo.Items[i];
                if (it.Path != null && string.Equals(it.Path, effective, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
            }
            if (idx < 0)
            {
                if (!string.IsNullOrWhiteSpace(current) && current != AppState.BuiltInModel) { idx = _modelCombo.Items.Count - 1; _model.Text = current; }
                else idx = 0;
            }
            else _model.Text = "";
            _modelCombo.SelectedIndex = idx;
            OnModelComboChanged();
        }

        private void OnModelComboChanged()
        {
            var it = _modelCombo.SelectedItem as ModelItem;
            bool custom = it != null && it.Path == null;
            _model.Enabled = custom;
            _browse.Enabled = custom;
        }

        private string SelectedModelPath()
        {
            var it = _modelCombo.SelectedItem as ModelItem;
            if (it == null) return "";
            if (it.Path == null) return _model.Text.Trim();
            return it.Path;
        }

        private void UpdateModelInfo()
        {
            var st = AppState.Instance;
            _modelInfo.Text = string.IsNullOrEmpty(st.ActiveModelName)
                ? Loc.T("settings.modelInfo") + ": " + Loc.T("settings.modelBuiltIn")
                : Loc.T("settings.modelInfo") + ": " + st.ActiveModelName + "  ·  " + st.ActiveModelInfo;
        }

        private void UpdateConnVisibility()
        {
            var t = (_adapter.SelectedItem as AdapterItem)?.Type ?? AdapterType.Simulator;
            bool serial = t == AdapterType.Elm327Usb || t == AdapterType.Elm327Bluetooth || t == AdapterType.ObdLink;
            bool wifi = t == AdapterType.Elm327WiFi;
            _port.Enabled = _refreshPorts.Enabled = _baud.Enabled = serial;
            _host.Enabled = _tcpPort.Enabled = wifi;
            _hint.Visible = t == AdapterType.Simulator;
        }

        private void RefreshPorts()
        {
            var cur = _port.SelectedItem as string;
            _port.Items.Clear();
            foreach (var p in SerialTransport.AvailablePorts()) _port.Items.Add(p);
            if (cur != null && _port.Items.Contains(cur)) _port.SelectedItem = cur;
            else if (_port.Items.Count > 0) _port.SelectedIndex = 0;
        }

        private void BrowseModel()
        {
            using (var dlg = new OpenFileDialog { Filter = "3D models (*.glb;*.gltf;*.obj)|*.glb;*.gltf;*.obj|glTF (*.glb;*.gltf)|*.glb;*.gltf|Wavefront OBJ (*.obj)|*.obj|All files (*.*)|*.*", Title = Loc.T("settings.model") })
            {
                if (dlg.ShowDialog() == DialogResult.OK) _model.Text = dlg.FileName;
            }
        }

        private void Save()
        {
            var st = AppState.Instance;
            var s = st.Settings;
            s.Language = Loc.Code;
            s.Adapter = (_adapter.SelectedItem as AdapterItem)?.Type ?? AdapterType.Simulator;
            s.SerialPort = _port.SelectedItem as string ?? "";
            s.BaudRate = _baud.SelectedItem is int ? (int)_baud.SelectedItem : 38400;
            s.Host = _host.Text.Trim();
            s.TcpPort = (int)_tcpPort.Value;
            s.Protocol = (int)((_protocol.SelectedItem as ProtocolItem)?.Protocol ?? ObdProtocol.Auto);
            s.LiveSampleMs = (int)_sampleMs.Value;
            s.Metric = _metric.Checked;
            s.AutoRotate = _autoRotate.Checked;
            s.ShowHarness = _harness.Checked;
            bool fsChanged = s.FullScreen != _fullscreen.Checked;
            s.FullScreen = _fullscreen.Checked;
            var newModel = SelectedModelPath();
            bool modelChanged = s.ModelPath != newModel || s.ModelFlip != _modelFlip.Checked;
            s.ModelPath = newModel;
            s.ModelFlip = _modelFlip.Checked;
            s.Save();
            if (modelChanged) st.LoadMesh(s.ModelPath);
            if (fsChanged) FullScreenChanged?.Invoke();
            Info(Loc.T("settings.saved"));
        }

        private void TestConnection()
        {
            Save();
            var st = AppState.Instance;
            _test.Enabled = false;
            Action handler = null;
            handler = () =>
            {
                if (st.Connection == ConnectionState.Connecting) return;
                st.ConnectionChanged -= handler;
                _test.Enabled = true;
                if (st.IsConnected) Info(Loc.T("conn.success", ObdProtocolInfo.Name(st.Adapter.Protocol)) + "\n" + st.Adapter.Version);
                else Error(Loc.T("conn.failed", st.ConnectionError));
            };
            st.ConnectionChanged += handler;
            st.ConnectAsync();
        }

        private void RefreshTrace()
        {
            var log = AppState.Instance.TraceLog;
            string[] snapshot;
            lock (log) snapshot = log.Skip(Math.Max(0, log.Count - 60)).Reverse().ToArray();
            _trace.SetItems(snapshot);
        }

        private void DrawTrace(Graphics g, Rectangle r, object item, bool sel, bool hov)
        {
            using (var f = new Font("Consolas", 7.5f))
                Theme.DrawText(g, (string)item, f, Theme.TextMuted, new Rectangle(r.X, r.Y, r.Width, r.Height), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
