using System;
using System.Drawing;
using System.Windows.Forms;

namespace RgbApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Una sola ventana: si ya hay una abierta, se trae al frente y esta copia termina.
            bool created;
            using (new System.Threading.Mutex(true, "RGBControl.Window", out created))
            {
                if (!created)
                {
                    NativeMethods.AllowSetForegroundWindow(-1); // ASFW_ANY
                    NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, MainForm.ShowMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }

    internal static class AppIcon
    {
        /// <summary>El icono incrustado en el .exe (app.ico).</summary>
        public static Icon Create()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return SystemIcons.Application;
            }
        }
    }
}
