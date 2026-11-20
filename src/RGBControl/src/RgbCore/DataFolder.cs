using System;
using System.IO;

namespace RgbCore
{
    /// <summary>
    /// Carpeta de datos del usuario (%LOCALAPPDATA%\RGB Control) con presets.json y helper.cfg.
    /// El programa puede estar en Archivos de programa, donde un usuario normal no puede escribir.
    /// </summary>
    public static class DataFolder
    {
        public static string Path =>
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RGB Control");

        /// <summary>Crea la carpeta de datos y copia los archivos que versiones anteriores guardaban junto al programa.</summary>
        public static string Prepare(string legacyDir) => Prepare(legacyDir, Path);

        public static string Prepare(string legacyDir, string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            foreach (string name in new[] { "presets.json", HelperConfig.FileName })
            {
                string from = System.IO.Path.Combine(legacyDir, name);
                string to = System.IO.Path.Combine(dataDir, name);
                if (!File.Exists(to) && File.Exists(from))
                    File.Copy(from, to);
            }
            return dataDir;
        }
    }
}
