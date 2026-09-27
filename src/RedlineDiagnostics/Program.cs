using System;
using System.Threading;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Forms;

namespace RedlineDiagnostics
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => ShowFatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowFatal(e.ExceptionObject as Exception);

            AppState.Instance.Initialize();
            var args = Environment.GetCommandLineArgs();
            string autoTestDir = null;
            for (int i = 1; i < args.Length - 1; i++)
                if (string.Equals(args[i], "--autotest", StringComparison.OrdinalIgnoreCase)) autoTestDir = args[i + 1];
            Application.Run(new MainForm { AutoTestDir = autoTestDir });
        }

        private static void ShowFatal(Exception ex)
        {
            if (ex == null) return;
            try
            {
                MessageBox.Show(ex.ToString(), "Redline Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
