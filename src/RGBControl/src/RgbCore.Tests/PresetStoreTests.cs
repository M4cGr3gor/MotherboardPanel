using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RgbCore;
using Xunit;

namespace RgbCore.Tests
{
    public class PresetStoreTests : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "rgb-presets-" + Guid.NewGuid().ToString("N") + ".json");

        public void Dispose()
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        [Fact]
        public void Load_CreatesDefaultFile_WhenMissing()
        {
            Assert.False(File.Exists(path));

            var store = new PresetStore(path);
            store.Load();

            Assert.True(File.Exists(path));
            var reloaded = new PresetStore(path);
            reloaded.Load();
            Assert.Equal(Preset.Defaults().Select(p => p.Name), reloaded.Presets.Select(p => p.Name));
            Assert.Equal(PresetStore.DefaultLedCounts(), reloaded.LedCounts);
        }

        [Fact]
        public void NewFile_IsNotCalibrated()
        {
            var store = new PresetStore(path);
            store.Load();
            Assert.False(store.LedCountsCalibrated);
            Assert.Equal(new[] { 32, 32, 32 }, store.LedCounts);

            var reloaded = new PresetStore(path);
            reloaded.Load();
            Assert.False(reloaded.LedCountsCalibrated);
            Assert.Equal(new[] { 32, 32, 32 }, reloaded.LedCounts);
        }

        [Fact]
        public void OldFileWithLedCounts_CountsAsCalibrated()
        {
            File.WriteAllText(path, "{\"LastApplied\":null,\"Presets\":[],\"LedCounts\":[0,0,8]}");
            var store = new PresetStore(path);
            store.Load();
            Assert.True(store.LedCountsCalibrated);
            Assert.Equal(new[] { 0, 0, 8 }, store.LedCounts);
        }

        [Fact]
        public void OldFileWithoutLedCounts_IsNotCalibrated()
        {
            File.WriteAllText(path, "{\"LastApplied\":null,\"Presets\":[]}");
            var store = new PresetStore(path);
            store.Load();
            Assert.False(store.LedCountsCalibrated);
        }

        [Fact]
        public void Calibrated_RoundTrips()
        {
            var store = new PresetStore(path);
            store.LedCounts = new[] { 0, 0, 8 };
            store.LedCountsCalibrated = true;
            store.Save();

            var loaded = new PresetStore(path);
            loaded.Load();
            Assert.True(loaded.LedCountsCalibrated);
            Assert.Equal(new[] { 0, 0, 8 }, loaded.LedCounts);
        }

        [Fact]
        public void LoadsCurrentFormatFile()
        {
            File.WriteAllText(path,
                "{\"LastApplied\":\"Noche\",\"Presets\":[{\"Name\":\"Noche\",\"Effect\":1,\"Color\":\"#6040FF\",\"Brightness\":40,\"Speed\":5,\"ColorCount\":7,\"IsDefault\":false,\"Icon\":\"moon\"}]}");

            var store = new PresetStore(path);
            store.Load();

            Assert.Equal(7, store.Presets.Count);
            Assert.True(store.Presets.Take(6).All(p => p.IsDefault));
            Assert.Equal("Degradado giratorio", store.Presets[4].Name);
            Assert.Equal("Apagado", store.Presets[5].Name);
            Assert.Equal("Noche", store.Presets[6].Name);
            Assert.Equal("moon", store.Presets[6].Icon);
            Assert.Equal(3, store.LedCounts.Length);
            Assert.Equal("Noche", store.LastApplied);
        }

        [Fact]
        public void RoundTripsNewFields()
        {
            var store = new PresetStore(path);
            store.Presets.Add(new Preset
            {
                Name = "Mío",
                Effect = PresetEffect.Comet,
                Colors = new List<string> { "#112233", "#445566", "#778899" },
                Clockwise = false,
                Trail = 8,
            });
            store.LastChipPreset = "Respiración";
            store.LedCounts = new[] { 12, 20, 0 };
            store.ByteOrders = new uint[] { 0x00010002, 0x00000102, 0x00010002 };
            store.Save();

            var loaded = new PresetStore(path);
            loaded.Load();
            var p = loaded.Presets.Single(x => x.Name == "Mío");
            Assert.Equal(new[] { "#112233", "#445566", "#778899" }, p.Colors);
            Assert.False(p.Clockwise);
            Assert.Equal(8, p.Trail);
            Assert.Equal(PresetEffect.Comet, p.Effect);
            Assert.Equal("Respiración", loaded.LastChipPreset);
            Assert.Equal(new[] { 12, 20, 0 }, loaded.LedCounts);
            Assert.Equal(new uint[] { 0x00010002, 0x00000102, 0x00010002 }, loaded.ByteOrders);
        }

        [Fact]
        public void NewDefault_GoesAfterExistingDefaults()
        {
            // Archivo de la versión anterior: los 5 predefinidos guardados y un preset del usuario.
            var old = new PresetStore(path);
            old.Presets.RemoveAll(p => p.Name == "Degradado giratorio");
            old.Presets.Add(new Preset { Name = "Mío", Effect = PresetEffect.Static });
            old.Save();

            var store = new PresetStore(path);
            store.Load();
            Assert.Equal(
                new[] { "Onda arcoíris", "Ciclo de colores", "Respiración", "Color fijo", "Degradado giratorio", "Apagado", "Mío" },
                store.Presets.Select(p => p.Name));
        }

        [Fact]
        public void Defaults_AreReorderedToCanonicalOrder_KeepingTheirSettings()
        {
            // Archivo guardado con "Apagado" delante de "Degradado giratorio" y un ajuste cambiado.
            var old = new PresetStore(path);
            var off = old.Presets.Single(p => p.Name == "Apagado");
            old.Presets.Remove(off);
            old.Presets.Insert(4, off);
            old.Presets.Single(p => p.Name == "Respiración").Speed = 3;
            old.Save();

            var store = new PresetStore(path);
            store.Load();
            Assert.Equal("Apagado", store.Presets[5].Name);
            Assert.Equal(3, store.Presets.Single(p => p.Name == "Respiración").Speed);
        }

        [Fact]
        public void ByteOrders_NullWhenNeverRead()
        {
            File.WriteAllText(path, "{\"LastApplied\":null,\"Presets\":[]}");
            var store = new PresetStore(path);
            store.Load();
            Assert.Null(store.ByteOrders);
        }
    }
}
