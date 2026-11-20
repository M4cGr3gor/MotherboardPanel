using Microsoft.Win32;

namespace RgbCore
{
    /// <summary>
    /// Interruptor "Usar la iluminación dinámica en mis dispositivos" de Configuración de Windows.
    /// Configuración lo guarda en HKCU\Software\Microsoft\Lighting\AmbientLightingEnabled.
    /// </summary>
    public static class WindowsDynamicLighting
    {
        private const string KeyPath = @"Software\Microsoft\Lighting";
        private const string ValueName = "AmbientLightingEnabled";

        public static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
            {
                object v = key?.GetValue(ValueName);
                return v is int i && i != 0;
            }
        }

        public static void Disable()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
                key.SetValue(ValueName, 0, RegistryValueKind.DWord);
        }
    }
}
