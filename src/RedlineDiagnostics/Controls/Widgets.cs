using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Localization;

namespace RedlineDiagnostics.Controls
{
    /// <summary>Double-buffered, custom-painted base for every control in the app.</summary>
    public class BaseControl : Control, ILocalizable
    {
        public BaseControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            DoubleBuffered = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var b = new SolidBrush(BackColor)) e.Graphics.FillRectangle(b, ClientRectangle);
        }

        public void ApplyLocalization()
        {
            OnLocalize();
            foreach (Control c in Controls)
            {
                var l = c as ILocalizable;
                if (l != null) l.ApplyLocalization();
            }
            Invalidate();
        }

        protected virtual void OnLocalize() { }

        protected Font F(float size, bool bold = false) => bold ? Theme.Bold(size) : Theme.Font(size);
    }

    /// <summary>Rounded button with icon, filled (accent) or outline style.</summary>
    public sealed class NeonButton : BaseControl
    {
        private bool _hover, _down;
        public string Icon { get; set; }
        public Color Accent { get; set; } = Theme.Red;
        public bool Filled { get; set; } = true;
        public string TextKey { get; set; }
        public float FontSize { get; set; } = 9.5f;

        public NeonButton()
        {
            Size = new Size(150, 36);
            Cursor = Cursors.Hand;
        }

        protected override void OnLocalize()
        {
            if (TextKey != null) Text = Loc.T(TextKey);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            var accent = Enabled ? Accent : Theme.Grey;
            if (Filled)
            {
                var c1 = _down ? Theme.Lerp(accent, Color.Black, 0.25f) : _hover ? Theme.Lerp(accent, Color.White, 0.12f) : accent;
                using (var p = Theme.RoundedRect(r, 8))
                using (var b = new LinearGradientBrush(new RectangleF(-1, -1, Width + 2, Height + 2), Theme.Lerp(c1, Color.White, 0.08f), Theme.Lerp(c1, Color.Black, 0.15f), LinearGradientMode.Vertical))
                    g.FillPath(b, p);
                if (_hover && Enabled) Theme.Glow(g, r, 8, accent, 5, 120);
            }
            else
            {
                Theme.FillRounded(g, r, 8, _hover ? Theme.WithAlpha(accent, 40) : Theme.WithAlpha(accent, 18));
                Theme.DrawRounded(g, r, 8, Theme.WithAlpha(accent, _hover ? 220 : 150));
            }
            var textColor = Filled ? Color.White : (Enabled ? Theme.Lerp(accent, Color.White, 0.25f) : Theme.TextDim);
            using (var f = F(FontSize, true))
            {
                var sz = Theme.Measure(Text, f);
                float iconW = Icon != null ? 18 : 0;
                float total = sz.Width + iconW + (Icon != null ? 6 : 0);
                float x = (Width - total) / 2;
                if (Icon != null)
                {
                    Icons.Draw(g, Icon, new RectangleF(x, Height / 2f - 8, 16, 16), textColor, 1.7f);
                    x += iconW + 6;
                }
                Theme.DrawText(g, Text, f, textColor, new Point((int)x, (Height - sz.Height) / 2));
            }
        }
    }

    /// <summary>Rounded card container with optional title row.</summary>
    public class GlowPanel : BaseControl
    {
        public string TitleKey { get; set; }
        public string TitleIcon { get; set; }
        public Color Accent { get; set; } = Theme.Red;
        public float Radius { get; set; } = 12f;
        public Color Fill { get; set; } = Theme.Surface;
        public string TitleText { get; set; }
        public string RightText { get; set; }

        public GlowPanel()
        {
            BackColor = Theme.Background;
        }

        public int ContentTop => string.IsNullOrEmpty(TitleKey) && string.IsNullOrEmpty(TitleText) ? 12 : 46;

        protected override void OnLocalize()
        {
            if (TitleKey != null) TitleText = Loc.T(TitleKey);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Theme.Panel(g, r, Radius, Fill, Theme.Border);
            if (!string.IsNullOrEmpty(TitleText))
            {
                float x = 16;
                if (!string.IsNullOrEmpty(TitleIcon))
                {
                    Theme.GlowDot(g, new PointF(x + 11, 22), 11, Theme.WithAlpha(Accent, 40), 1);
                    Icons.Draw(g, TitleIcon, new RectangleF(x + 3, 14, 16, 16), Accent, 1.7f);
                    x += 32;
                }
                using (var f = F(11f, true))
                    Theme.DrawText(g, TitleText, f, Theme.Text, new Point((int)x, 13));
                if (!string.IsNullOrEmpty(RightText))
                    using (var f = F(9f))
                    {
                        var sz = Theme.Measure(RightText, f);
                        Theme.DrawText(g, RightText, f, Theme.TextMuted, new Point(Width - 16 - sz.Width, 16));
                    }
                using (var pen = new Pen(Theme.BorderSoft, 1f))
                    g.DrawLine(pen, 12, 42, Width - 12, 42);
            }
        }
    }

    /// <summary>Compact line chart with glow, used for live data and the communication trace.</summary>
    public sealed class Sparkline : BaseControl
    {
        private double[] _values = new double[0];
        public Color LineColor { get; set; } = Theme.Red;
        public bool ShowGrid { get; set; } = true;
        public bool Fill { get; set; } = true;
        public double? FixedMin { get; set; }
        public double? FixedMax { get; set; }

        public Sparkline()
        {
            BackColor = Theme.Card;
        }

        public void SetValues(IEnumerable<double> values)
        {
            _values = values?.ToArray() ?? new double[0];
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Theme.Panel(g, r, 8, Theme.Card, Theme.BorderSoft);
            var inner = new RectangleF(10, 8, Width - 20, Height - 16);
            if (ShowGrid)
            {
                using (var pen = new Pen(Theme.WithAlpha(Theme.TextDim, 70), 1f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(pen, inner.Left, inner.Top + inner.Height * 0.5f, inner.Right, inner.Top + inner.Height * 0.5f);
                }
                using (var pen = new Pen(Theme.WithAlpha(Theme.TextDim, 90), 1f))
                    g.DrawLine(pen, inner.Left, inner.Bottom, inner.Right, inner.Bottom);
            }
            if (_values.Length < 2) return;
            double min = FixedMin ?? _values.Min();
            double max = FixedMax ?? _values.Max();
            if (max - min < 1e-9) { max = min + 1; min -= 1; }
            double pad = (max - min) * 0.1;
            if (!FixedMin.HasValue) min -= pad;
            if (!FixedMax.HasValue) max += pad;
            var pts = new PointF[_values.Length];
            for (int i = 0; i < _values.Length; i++)
            {
                float x = inner.Left + inner.Width * i / (_values.Length - 1);
                float y = inner.Bottom - (float)((_values[i] - min) / (max - min)) * inner.Height;
                pts[i] = new PointF(x, y);
            }
            if (Fill)
            {
                var poly = new PointF[pts.Length + 2];
                poly[0] = new PointF(pts[0].X, inner.Bottom);
                Array.Copy(pts, 0, poly, 1, pts.Length);
                poly[poly.Length - 1] = new PointF(pts[pts.Length - 1].X, inner.Bottom);
                using (var b = new LinearGradientBrush(new RectangleF(inner.X, inner.Y - 1, inner.Width, inner.Height + 2), Theme.WithAlpha(LineColor, 70), Theme.WithAlpha(LineColor, 0), LinearGradientMode.Vertical))
                    g.FillPolygon(b, poly);
            }
            using (var pen = new Pen(Theme.WithAlpha(LineColor, 60), 4f) { LineJoin = LineJoin.Round })
                g.DrawLines(pen, pts);
            using (var pen = new Pen(LineColor, 1.6f) { LineJoin = LineJoin.Round })
                g.DrawLines(pen, pts);
            var last = pts[pts.Length - 1];
            Theme.GlowDot(g, last, 2.5f, LineColor, 3);
        }
    }

    public sealed class KeyValueRow
    {
        public string Label;
        public string Value;
        public Color? ValueColor;
        public Color? Dot;

        public KeyValueRow(string label, string value, Color? valueColor = null, Color? dot = null)
        {
            Label = label; Value = value; ValueColor = valueColor; Dot = dot;
        }
    }

    /// <summary>Two-column label/value list.</summary>
    public sealed class KeyValueList : BaseControl
    {
        private readonly List<KeyValueRow> _rows = new List<KeyValueRow>();
        public int RowHeight { get; set; } = 26;
        public bool Separators { get; set; } = true;
        public float FontSize { get; set; } = 9f;
        public bool ValueBold { get; set; }

        public void SetRows(IEnumerable<KeyValueRow> rows)
        {
            _rows.Clear();
            if (rows != null) _rows.AddRange(rows);
            Invalidate();
        }

        public int PreferredHeight => _rows.Count * RowHeight;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            using (var fl = F(FontSize))
            using (var fv = F(FontSize, ValueBold))
            {
                int y = 0;
                foreach (var row in _rows)
                {
                    if (y + RowHeight > Height + RowHeight) break;
                    int x = 0;
                    if (row.Dot.HasValue)
                    {
                        Theme.GlowDot(g, new PointF(6, y + RowHeight / 2f), 3.5f, row.Dot.Value, 2);
                        x = 16;
                    }
                    Theme.DrawText(g, row.Label, fl, Theme.TextMuted, new Rectangle(x, y, Width - x, RowHeight), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    Theme.DrawText(g, row.Value ?? "", fv, row.ValueColor ?? Theme.Text, new Rectangle(0, y, Width, RowHeight), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                    if (Separators)
                        using (var pen = new Pen(Theme.BorderSoft, 1f))
                            g.DrawLine(pen, 0, y + RowHeight - 1, Width, y + RowHeight - 1);
                    y += RowHeight;
                }
            }
        }
    }

    /// <summary>Scrollable list with owner-drawn rows.</summary>
    public sealed class ItemList : BaseControl
    {
        private readonly List<object> _items = new List<object>();
        private int _scroll, _hover = -1, _selected = -1;

        public int ItemHeight { get; set; } = 56;
        public Action<Graphics, Rectangle, object, bool, bool> DrawItem { get; set; }
        public string EmptyTextKey { get; set; }
        public event Action SelectionChanged;
        public event Action<object> ItemActivated;

        public ItemList()
        {
            BackColor = Theme.Surface;
        }

        public IReadOnlyList<object> Items => _items;
        public object SelectedItem => _selected >= 0 && _selected < _items.Count ? _items[_selected] : null;

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
                SelectionChanged?.Invoke();
            }
        }

        public void SetItems(IEnumerable<object> items, bool keepSelection = false)
        {
            var sel = SelectedItem;
            _items.Clear();
            if (items != null) _items.AddRange(items);
            _scroll = Math.Max(0, Math.Min(_scroll, MaxScroll));
            int idx = keepSelection && sel != null ? _items.IndexOf(sel) : -1;
            if (idx < 0 && _items.Count > 0 && keepSelection) idx = 0;
            _selected = idx;
            Invalidate();
            SelectionChanged?.Invoke();
        }

        private int MaxScroll => Math.Max(0, _items.Count * ItemHeight - Height);

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _scroll = Math.Max(0, Math.Min(MaxScroll, _scroll - Math.Sign(e.Delta) * ItemHeight));
            Invalidate();
            base.OnMouseWheel(e);
        }

        private int HitTest(Point p)
        {
            int idx = (p.Y + _scroll) / ItemHeight;
            return idx >= 0 && idx < _items.Count ? idx : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != _hover) { _hover = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            int h = HitTest(e.Location);
            if (h >= 0) SelectedIndex = h;
            base.OnMouseDown(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h >= 0) ItemActivated?.Invoke(_items[h]);
            base.OnMouseDoubleClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            if (_items.Count == 0)
            {
                using (var f = F(10f))
                    Theme.DrawText(g, EmptyTextKey != null ? Loc.T(EmptyTextKey) : "", f, Theme.TextMuted, ClientRectangle, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }
            int first = _scroll / ItemHeight;
            for (int i = first; i < _items.Count; i++)
            {
                int y = i * ItemHeight - _scroll;
                if (y > Height) break;
                var r = new Rectangle(0, y, Width - (MaxScroll > 0 ? 8 : 0), ItemHeight);
                bool sel = i == _selected, hov = i == _hover;
                if (sel) Theme.FillRounded(g, new RectangleF(r.X + 4, r.Y + 3, r.Width - 8, r.Height - 6), 8, Theme.WithAlpha(Theme.Red, 40));
                else if (hov) Theme.FillRounded(g, new RectangleF(r.X + 4, r.Y + 3, r.Width - 8, r.Height - 6), 8, Theme.WithAlpha(Theme.Cyan, 18));
                if (sel) Theme.DrawRounded(g, new RectangleF(r.X + 4.5f, r.Y + 3.5f, r.Width - 9, r.Height - 7), 8, Theme.WithAlpha(Theme.Red, 160));
                DrawItem?.Invoke(g, Rectangle.Inflate(r, -14, -6), _items[i], sel, hov);
                using (var pen = new Pen(Theme.BorderSoft, 1f))
                    g.DrawLine(pen, 12, r.Bottom - 1, r.Right - 12, r.Bottom - 1);
            }
            if (MaxScroll > 0)
            {
                float trackH = Height - 8;
                float thumbH = Math.Max(24, trackH * Height / (_items.Count * ItemHeight));
                float thumbY = 4 + (trackH - thumbH) * _scroll / MaxScroll;
                Theme.FillRounded(g, new RectangleF(Width - 6, thumbY, 3, thumbH), 1.5f, Theme.WithAlpha(Theme.TextMuted, 120));
            }
        }
    }

    /// <summary>iOS-style on/off switch.</summary>
    public sealed class ToggleSwitch : BaseControl
    {
        private bool _checked;
        public event Action CheckedChanged;

        public ToggleSwitch()
        {
            Size = new Size(44, 24);
            Cursor = Cursors.Hand;
        }

        public bool Checked
        {
            get { return _checked; }
            set { if (_checked != value) { _checked = value; Invalidate(); CheckedChanged?.Invoke(); } }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Checked = !Checked;
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Theme.FillRounded(g, r, Height / 2f, _checked ? Theme.Red : Theme.SurfaceAlt);
            Theme.DrawRounded(g, r, Height / 2f, _checked ? Theme.RedBright : Theme.Border);
            float d = Height - 6;
            float x = _checked ? Width - d - 3 : 3;
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, x, 3, d, d);
        }
    }

    /// <summary>Simple text label painted with the theme font (supports localization keys).</summary>
    public sealed class ThemeLabel : BaseControl
    {
        public string TextKey { get; set; }
        public float FontSize { get; set; } = 9.5f;
        public bool Bold { get; set; }
        public Color Color { get; set; } = Theme.Text;
        public ContentAlignment Align { get; set; } = ContentAlignment.MiddleLeft;
        public bool Wrap { get; set; }

        public ThemeLabel()
        {
            Size = new Size(120, 24);
        }

        protected override void OnLocalize()
        {
            if (TextKey != null) Text = Loc.T(TextKey);
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            var flags = TextFormatFlags.EndEllipsis;
            if (Wrap) flags = TextFormatFlags.WordBreak;
            switch (Align)
            {
                case ContentAlignment.MiddleCenter: flags |= TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter; break;
                case ContentAlignment.MiddleRight: flags |= TextFormatFlags.Right | TextFormatFlags.VerticalCenter; break;
                case ContentAlignment.TopLeft: flags |= TextFormatFlags.Left | TextFormatFlags.Top; break;
                default: flags |= TextFormatFlags.Left | TextFormatFlags.VerticalCenter; break;
            }
            using (var f = F(FontSize, Bold))
                Theme.DrawText(g, Text, f, Color, ClientRectangle, flags);
        }
    }

    /// <summary>Factory helpers for the few standard WinForms inputs we use (styled dark).</summary>
    public static class Dark
    {
        public static TextBox TextBox(int width = 200)
        {
            return new TextBox
            {
                BackColor = Theme.SurfaceAlt,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.Font(10f),
                Width = width,
                Height = 28
            };
        }

        public static ComboBox Combo(int width = 200)
        {
            var cb = new ComboBox
            {
                BackColor = Theme.SurfaceAlt,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = Theme.Font(10f),
                Width = width,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 20
            };
            cb.DrawItem += (s, e) =>
            {
                var c = (ComboBox)s;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                bool editBox = (e.State & DrawItemState.ComboBoxEdit) != 0;
                using (var b = new SolidBrush(selected && !editBox ? Theme.WithAlpha(Theme.Red, 90) : Theme.SurfaceAlt))
                    e.Graphics.FillRectangle(b, e.Bounds);
                if (e.Index >= 0 && e.Index < c.Items.Count)
                {
                    var text = c.Items[e.Index]?.ToString() ?? "";
                    TextRenderer.DrawText(e.Graphics, text, c.Font, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height), c.Enabled ? Theme.Text : Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                }
            };
            return cb;
        }

        public static NumericUpDown Numeric(int min, int max, int value, int width = 120)
        {
            return new NumericUpDown
            {
                BackColor = Theme.SurfaceAlt,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.Font(10f),
                Minimum = min,
                Maximum = max,
                Value = Math.Max(min, Math.Min(max, value)),
                Width = width
            };
        }
    }
}
