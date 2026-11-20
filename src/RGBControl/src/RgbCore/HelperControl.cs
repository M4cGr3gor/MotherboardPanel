using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;

namespace RgbCore
{
    /// <summary>
    /// Lo que la ventana necesita del ayudante (RGB Helper.exe): preferencia e inicio con Windows,
    /// saber si está funcionando, avisarle y arrancarlo. Los nombres coinciden con los del ayudante en C++.
    /// </summary>
    public sealed class HelperControl
    {
        public const string HelperExeName = "RGB Helper.exe";
        private const string RunValueName = "RGBControlHelper";
        private const string PreferenceValueName = "HelperAutostart";

        private readonly string runKeyPath;
        private readonly string settingsKeyPath;
        private readonly string nameSuffix;

        /// <param name="nameSuffix">Sufijo de los objetos de Windows; solo para pruebas.</param>
        public HelperControl(
            string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run",
            string settingsKeyPath = @"Software\RGBControl",
            string nameSuffix = "")
        {
            this.runKeyPath = runKeyPath;
            this.settingsKeyPath = settingsKeyPath;
            this.nameSuffix = nameSuffix;
        }

        /// <summary>"Iniciar el ayudante con Windows". Si nunca se ha guardado, está activada.</summary>
        public bool AutostartPreference
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(settingsKeyPath))
                    return !(key?.GetValue(PreferenceValueName) is int v) || v != 0;
            }
            set
            {
                using (var key = Registry.CurrentUser.CreateSubKey(settingsKeyPath))
                    key.SetValue(PreferenceValueName, value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        public void RegisterAutostart(string helperExePath)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(runKeyPath))
                key.SetValue(RunValueName, "\"" + helperExePath + "\"", RegistryValueKind.String);
        }

        public void UnregisterAutostart()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true))
                key?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }

        /// <summary>Comando registrado en el inicio de Windows, o null si no lo está.</summary>
        public string RegisteredCommand
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(runKeyPath))
                    return key?.GetValue(RunValueName) as string;
            }
        }

        public bool IsHelperRunning()
        {
            Mutex m;
            if (!Mutex.TryOpenExisting("RGBControl.Helper" + nameSuffix, out m))
                return false;
            m.Dispose();
            return true;
        }

        public void SignalReload() => Signal("RGBControl.Reload");

        public void SignalExit() => Signal("RGBControl.Exit");

        /// <summary>Si el ayudante funciona, le pide recargar helper.cfg; si no, lo arranca.</summary>
        public void StartOrReload(string helperExePath)
        {
            if (IsHelperRunning())
                SignalReload();
            else
                Process.Start(new ProcessStartInfo(helperExePath) { UseShellExecute = false });
        }

        private void Signal(string name)
        {
            EventWaitHandle ev;
            if (!EventWaitHandle.TryOpenExisting(name + nameSuffix, out ev))
                return;
            using (ev)
                ev.Set();
        }
    }
}
