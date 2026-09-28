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

            var args = Environment.GetCommandLineArgs();
            string autoTestDir = null, renderDir = null, splashDir = null;
            bool quick = false, startup = false;
            for (int i = 1; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--autotest", StringComparison.OrdinalIgnoreCase)) autoTestDir = args[i + 1];
                if (string.Equals(args[i], "--autotest-home", StringComparison.OrdinalIgnoreCase)) { autoTestDir = args[i + 1]; quick = true; }
                if (string.Equals(args[i], "--render-cars", StringComparison.OrdinalIgnoreCase)) renderDir = args[i + 1];
                if (string.Equals(args[i], "--autotest-splash", StringComparison.OrdinalIgnoreCase)) splashDir = args[i + 1];
                if (string.Equals(args[i], "--autotest-startup", StringComparison.OrdinalIgnoreCase)) { splashDir = args[i + 1]; startup = true; }
            }

            if (autoTestDir == null && renderDir == null)
            {
                // Normal start: the splash screen performs the loading, then hands over to the main window.
                AppState.Instance.LoadSettings();
                var ctx = new ApplicationContext();
                var splash = new SplashForm { AutoTestDir = splashDir, AutoTestContinue = startup };
                MainForm main = null;
                splash.Finished += () =>
                {
                    // --autotest-startup: continue off-screen into the quick Home capture to check the hand-over.
                    main = startup ? new MainForm { AutoTestDir = splashDir, AutoTestQuick = true } : new MainForm();
                    main.FormClosed += (s, e) => ctx.ExitThread();
                    main.Show();
                    main.Activate();
                    splash.Close();
                };
                splash.FormClosed += (s, e) => { if (main == null) ctx.ExitThread(); };
                splash.Show();
                Application.Run(ctx);
                return;
            }

            AppState.Instance.Initialize();
            if (renderDir != null)
            {
                // Debug aid: writes the cached car "product shots" to PNG files and exits.
                System.IO.Directory.CreateDirectory(renderDir);
                AppState.Instance.LoadMesh(AppState.Instance.Settings.ModelPath);
                var mesh = AppState.Instance.VehicleMesh.Detailed ?? AppState.Instance.VehicleMesh;
                var sizes = new System.Collections.Generic.Dictionary<Controls.CarShot, System.Drawing.Size>
                {
                    { Controls.CarShot.Hero, new System.Drawing.Size(330, 200) },
                    { Controls.CarShot.Rear, new System.Drawing.Size(300, 150) },
                    { Controls.CarShot.Thumb, new System.Drawing.Size(76, 42) },
                    { Controls.CarShot.Nav, new System.Drawing.Size(150, 76) },
                };
                if (Environment.GetEnvironmentVariable("REDLINE_RENDER_BIG") == "1")
                {
                    Controls.CarImages.LightsEnabled = Environment.GetEnvironmentVariable("REDLINE_RENDER_LIGHTS") == "1";
                    foreach (var k in new System.Collections.Generic.List<Controls.CarShot>(sizes.Keys))
                        sizes[k] = new System.Drawing.Size(sizes[k].Width * 3, sizes[k].Height * 3);
                }
                foreach (var kv in sizes)
                    using (var bmp = Controls.CarImages.Render(mesh, kv.Key, kv.Value))
                    using (var bg = new System.Drawing.Bitmap(bmp.Width, bmp.Height))
                    using (var g = System.Drawing.Graphics.FromImage(bg))
                    {
                        g.Clear(System.Drawing.Color.Black);
                        g.DrawImage(bmp, 0, 0);
                        bg.Save(System.IO.Path.Combine(renderDir, kv.Key + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                    }
                return;
            }
            Application.Run(new MainForm { AutoTestDir = autoTestDir, AutoTestQuick = quick });
        }

        private static void ShowFatal(Exception ex)
        {
            if (ex == null) return;
            try
            {
                // Keep a record even when the dialog is dismissed (or no one is watching an automated run).
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RedlineDiagnostics");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"), DateTime.Now.ToString("s") + "  " + ex + Environment.NewLine + Environment.NewLine);
            }
            catch { }
            try
            {
                MessageBox.Show(ex.ToString(), "Redline Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
