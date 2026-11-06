using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Shiguang
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Runs from a %TEMP% copy of the app after the main window has closed.
            if (args != null && args.Length >= 4 && args[0] == "--patch")
            {
                ConfigStore.RunPatcher(args[1], args[2], args[3]);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            float dpi = 1f;
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    dpi = g.DpiX / 96f;
            }
            catch { }
            if (dpi < 0.7f || dpi > 4f) dpi = 1f;

            var wa = Screen.PrimaryScreen.WorkingArea;
            float fit = Math.Min((wa.Width - 40f) / 1100f, (wa.Height - 40f) / 720f);
            float scale = Math.Min(dpi, fit);
            if (scale < 0.72f) scale = 0.72f;
            if (scale > 2.5f) scale = 2.5f;
            Ui.Init(scale);

            string exe = Application.ExecutablePath;
            bool createdNew;
            var mutex = new Mutex(true, "Local\\DJS_SINGLE_" + ConfigStore.HashPath(exe), out createdNew);
            if (!createdNew)
            {
                ActivateExisting();
                try { mutex.Dispose(); } catch { }
                return;
            }

            try
            {
                var store = ConfigStore.Open(exe);
                Application.Run(new MainForm(store));
            }
            catch (Exception ex)
            {
                try
                {
                    System.IO.Directory.CreateDirectory(ConfigStore.TempFolder);
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(ConfigStore.TempFolder, "crash.log"),
                        DateTime.Now + "\r\n" + ex);
                }
                catch { }
                MessageBox.Show("程序遇到问题：\r\n\r\n" + ex.Message, "时光倒数",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                GC.KeepAlive(mutex);
                try { mutex.ReleaseMutex(); } catch { }
                try { mutex.Dispose(); } catch { }
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        static void ActivateExisting()
        {
            try
            {
                var h = FindWindow(null, "时光倒数");
                if (h != IntPtr.Zero)
                {
                    ShowWindow(h, 9);
                    SetForegroundWindow(h);
                }
            }
            catch { }
        }
    }
}
