using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace RgbCore
{
    /// <summary>
    /// Escribe helper.cfg: el preset avanzado ya resuelto y los paquetes del chip precalculados
    /// que lee el ayudante en C++ (RGB Helper.exe). Formato: una línea clave=valor por dato.
    /// </summary>
    public static class HelperConfig
    {
        public const string FileName = "helper.cfg";

        /// <param name="advanced">Preset avanzado a reproducir.</param>
        /// <param name="ledCounts">LEDs de D_LED1, D_LED2 y D_LED3.</param>
        /// <param name="byteOrders">Orden de bytes de color de cada conector.</param>
        /// <param name="lastChip">Último preset normal, que se restaura al cerrar el ayudante (null = ninguno).</param>
        public static string Build(Preset advanced, int[] ledCounts, uint[] byteOrders, Preset lastChip)
        {
            var sb = new StringBuilder();
            void Line(string key, object value) => sb.Append(key).Append('=').Append(value).Append('\n');

            // Los efectos de un solo color (Cometa, Vela, Latido, Escáner, Tormenta) envían ese color.
            uint[] colors = Preset.UsesColor(advanced.Effect) && !Preset.UsesColorList(advanced.Effect)
                ? new[] { advanced.ColorValue, advanced.ColorValue }
                : advanced.ColorValues();

            Line("version", 1);
            Line("name", (advanced.Name ?? "").Replace('\n', ' ').Replace('\r', ' '));
            Line("effect", (int)advanced.Effect);
            Line("colors", string.Join(",", colors.Select(Hex6)));
            Line("background", Hex6(advanced.BackgroundValue));
            Line("brightness", advanced.Brightness);
            Line("speed", advanced.Speed);
            Line("clockwise", advanced.Clockwise ? 1 : 0);
            Line("trail", advanced.Trail);
            Line("density", advanced.Density);
            Line("leds", string.Join(",", ledCounts));
            Line("byteorders", string.Join(",", byteOrders.Select(o => o.ToString("X8"))));

            var enter = new List<byte[]>();
            using (var rec = Fusion2Controller.CreateRecorder(enter))
                rec.EnterDirectMode(ledCounts);
            foreach (byte[] p in enter)
                Line("enter", ToHex(p));

            if (lastChip != null)
            {
                var restore = new List<byte[]>();
                using (var rec = Fusion2Controller.CreateRecorder(restore))
                    PresetEngine.Apply(rec, lastChip, saveToFlash: false);
                foreach (byte[] p in restore)
                    Line("restore", ToHex(p));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Escribe a un temporal y lo renombra, para que el ayudante nunca lea un archivo a medias.
        /// Si el ayudante lo está leyendo justo en ese momento, reintenta durante un máximo de 2 s.
        /// </summary>
        public static void Write(string path, string content)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(path))
                        File.Replace(tmp, path, null);
                    else
                        File.Move(tmp, path);
                    return;
                }
                catch (IOException) when (attempt < 40)
                {
                    Thread.Sleep(50);
                }
                catch (System.UnauthorizedAccessException) when (attempt < 40)
                {
                    Thread.Sleep(50);
                }
            }
        }

        private static string Hex6(uint v) => (v & 0xFFFFFF).ToString("X6");

        private static string ToHex(byte[] p)
        {
            var sb = new StringBuilder(p.Length * 2);
            foreach (byte b in p)
                sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
    }
}
