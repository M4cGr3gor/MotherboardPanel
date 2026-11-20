using System.Collections.Generic;
using System.Linq;
using RgbCore;
using Xunit;

namespace RgbCore.Tests
{
    public class HelperConfigTests
    {
        private static readonly uint[] Orders = { 0x00010002, 0x00010002, 0x00010002 };

        private static Preset Rotating() => Preset.Defaults().Single(p => p.Name == "Degradado giratorio");

        private static string[] Lines(string text, string key) =>
            text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.StartsWith(key + "=")).Select(l => l.Substring(key.Length + 1)).ToArray();

        [Fact]
        public void LedCountDivision_Ranges()
        {
            Assert.Equal(0, Fusion2Controller.LedCountDivision(0));
            Assert.Equal(0, Fusion2Controller.LedCountDivision(32));
            Assert.Equal(1, Fusion2Controller.LedCountDivision(33));
            Assert.Equal(1, Fusion2Controller.LedCountDivision(64));
            Assert.Equal(2, Fusion2Controller.LedCountDivision(65));
        }

        [Fact]
        public void EnterDirectMode_RecordsExpectedPackets()
        {
            var sink = new List<byte[]>();
            var rec = Fusion2Controller.CreateRecorder(sink);
            rec.EnterDirectMode(new[] { 12, 40, 0 });

            // LampArray off, reinicio de los 11 registros de efectos + aplicar (sin él, tras encender o
            // reanudar el chip sigue con su efecto guardado e ignora el modo directo), beat off,
            // efectos de serie off y tamaño de tira.
            Assert.Equal(16, sink.Count);
            Assert.All(sink, p => Assert.Equal(64, p.Length));
            Assert.Equal(new byte[] { 0xCC, 0x48, 0x00 }, sink[0].Take(3));
            Assert.Equal(new byte[] { 0xCC, 0x20 }, sink[1].Take(2));
            Assert.Equal(new byte[] { 0xCC, 0x92 }, sink[11].Take(2));
            Assert.Equal(new byte[] { 0xCC, 0x28, 0xFF, 0x07 }, sink[12].Take(4));
            Assert.Equal(new byte[] { 0xCC, 0x31, 0x00 }, sink[13].Take(3));
            Assert.Equal(new byte[] { 0xCC, 0x32, 0x7B }, sink[14].Take(3));
            Assert.Equal(new byte[] { 0xCC, 0x34, 0x10, 0x00, 0x00 }, sink[15].Take(5));
        }

        [Fact]
        public void Build_ContainsResolvedValues()
        {
            string cfg = HelperConfig.Build(Rotating(), new[] { 12, 40, 0 }, Orders, null);

            Assert.Equal(new[] { "1" }, Lines(cfg, "version"));
            Assert.Equal(new[] { "8" }, Lines(cfg, "effect"));
            Assert.Equal(new[] { "8000FF,0040FF,00E0FF" }, Lines(cfg, "colors"));
            Assert.Equal(new[] { "12,40,0" }, Lines(cfg, "leds"));
            Assert.Equal(new[] { "1" }, Lines(cfg, "clockwise"));
            Assert.Equal(new[] { "00010002,00010002,00010002" }, Lines(cfg, "byteorders"));
            string[] enter = Lines(cfg, "enter");
            Assert.Equal(16, enter.Length);
            Assert.All(enter, e => Assert.Equal(128, e.Length));
            Assert.StartsWith("CC48", enter[0]);
            Assert.Empty(Lines(cfg, "restore"));
        }

        [Fact]
        public void Build_WithLastChip_HasRestore()
        {
            var fixedColor = Preset.Defaults().Single(p => p.Name == "Color fijo");
            string cfg = HelperConfig.Build(Rotating(), new[] { 12, 40, 0 }, Orders, fixedColor);

            string[] restore = Lines(cfg, "restore");
            Assert.NotEmpty(restore);
            Assert.All(restore, r => Assert.Equal(128, r.Length));
            Assert.StartsWith("CC28", restore[restore.Length - 1]);
            Assert.DoesNotContain(restore, r => r.StartsWith("CC5E")); // nunca graba la flash
        }

        [Fact]
        public void Write_WaitsWhileFileIsBriefly_Locked()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rgb-cfg-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, HelperConfig.FileName);
            try
            {
                System.IO.File.WriteAllText(path, "viejo");
                // Simula al ayudante leyendo el archivo (sin permitir borrarlo/renombrarlo) durante 300 ms.
                var reader = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
                var release = System.Threading.Tasks.Task.Delay(300).ContinueWith(_ => reader.Dispose());

                HelperConfig.Write(path, "nuevo");

                release.Wait();
                Assert.Equal("nuevo", System.IO.File.ReadAllText(path));
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Build_SingleColorEffects_SendPresetColor()
        {
            foreach (var fx in new[] { PresetEffect.Candle, PresetEffect.Heartbeat, PresetEffect.Scanner, PresetEffect.Storm })
            {
                var p = new Preset { Name = "X", Effect = fx, Color = "#FF8020", Colors = new List<string> { "#000001", "#000002" } };
                string cfg = HelperConfig.Build(p, new[] { 0, 0, 8 }, Orders, null);
                Assert.Equal(new[] { "FF8020,FF8020" }, Lines(cfg, "colors"));
                Assert.Equal(new[] { ((int)fx).ToString() }, Lines(cfg, "effect"));
            }
        }

        [Fact]
        public void Build_Comet_UsesPresetColorAsHead()
        {
            var comet = new Preset { Name = "C", Effect = PresetEffect.Comet, Color = "#FF8000", Background = "#000020" };
            string cfg = HelperConfig.Build(comet, new[] { 12, 12, 0 }, Orders, null);
            Assert.Equal(new[] { "FF8000,FF8000" }, Lines(cfg, "colors"));
            Assert.Equal(new[] { "000020" }, Lines(cfg, "background"));
        }
    }
}
