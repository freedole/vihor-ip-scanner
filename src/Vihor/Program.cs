using System;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace Vihor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Single instance per user session
            using var mutex = new Mutex(true, @"Local\VihorIPScanner", out bool first);
            if (!first)
            {
                Native.BringExistingToFront("Vihor IP Scanner");
                return;
            }

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 64;
            // The scanner runs hundreds of short-lived probes in parallel; avoid thread-pool ramp-up delays
            ThreadPool.GetMinThreads(out int w, out int io);
            ThreadPool.SetMinThreads(Math.Max(w, 200), Math.Max(io, 200));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => ShowError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowError(e.ExceptionObject as Exception);

            Settings.Load();
            Lang.Load(Settings.Language);

            Application.Run(new MainForm());
        }

        private static void ShowError(Exception ex)
        {
            if (ex == null) return;
            MessageBox.Show(ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
