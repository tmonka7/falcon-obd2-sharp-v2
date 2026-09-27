using System;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Controls;
using RedlineDiagnostics.Localization;

namespace RedlineDiagnostics.Forms.Pages
{
    /// <summary>A full-content page hosted by the main form. Pages are 1206 x 712 at the design resolution.</summary>
    public abstract class BasePage : BaseControl
    {
        public const int PageWidth = 1206;
        public const int PageHeight = 712;

        public abstract string TitleKey { get; }

        /// <summary>Second line under the title in the top bar; default is the vehicle line.</summary>
        public virtual string Subtitle
        {
            get
            {
                var st = AppState.Instance;
                var parts = new System.Collections.Generic.List<string>();
                parts.Add(st.VehicleTitle);
                if (!string.IsNullOrEmpty(st.VehicleEngine)) parts.Add(st.VehicleEngine);
                var vin = st.CurrentVin;
                if (!string.IsNullOrEmpty(vin)) parts.Add("VIN: " + vin);
                return string.Join("    |    ", parts);
            }
        }

        public event Action<int> NavigateRequested;
        public event Action TitleChanged;

        protected BasePage()
        {
            Size = new System.Drawing.Size(PageWidth, PageHeight);
            BackColor = Theme.Background;
        }

        protected void Navigate(int index) => NavigateRequested?.Invoke(index);
        protected void RaiseTitleChanged() => TitleChanged?.Invoke();

        public virtual void OnShown() { }
        public virtual void OnHidden() { }

        protected static DialogResult Confirm(string text)
        {
            return MessageBox.Show(text, Loc.T("common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }

        protected static void Info(string text)
        {
            MessageBox.Show(text, Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected static void Error(string text)
        {
            MessageBox.Show(text, Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
