using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace RgbCore
{
    /// <summary>Todos los presets (los que trae el programa y los del usuario), guardados en presets.json en la carpeta de datos del usuario.</summary>
    public sealed class PresetStore
    {
        [DataContract]
        private sealed class FileData
        {
            [DataMember(Order = 0)] public string LastApplied { get; set; }
            [DataMember(Order = 1)] public List<Preset> Presets { get; set; }
            [DataMember(Order = 2, EmitDefaultValue = false)] public string LastChipPreset { get; set; }
            [DataMember(Order = 3, EmitDefaultValue = false)] public int[] LedCounts { get; set; }
            [DataMember(Order = 4, EmitDefaultValue = false)] public uint[] ByteOrders { get; set; }
            [DataMember(Order = 5, EmitDefaultValue = false)] public bool? LedCountsCalibrated { get; set; }
        }

        /// <summary>LEDs por conector (D_LED1, D_LED2, D_LED3) hasta que el usuario los calibra.</summary>
        public static int[] DefaultLedCounts() => new[] { 32, 32, 32 };

        public string FilePath { get; }
        public List<Preset> Presets { get; private set; } = Preset.Defaults();
        public string LastApplied { get; set; }
        /// <summary>Último preset normal (del chip) aplicado: el que se restaura al cerrar el ayudante.</summary>
        public string LastChipPreset { get; set; }
        public int[] LedCounts { get; set; } = DefaultLedCounts();
        /// <summary>Orden de bytes de color de cada conector, leído del chip una vez (null = aún no leído).</summary>
        public uint[] ByteOrders { get; set; }
        /// <summary>True cuando LedCounts sale de "Calibrar LEDs…" y no del valor por defecto.</summary>
        public bool LedCountsCalibrated { get; set; }

        public PresetStore(string filePath)
        {
            FilePath = filePath;
        }

        public static PresetStore Default() =>
            new PresetStore(Path.Combine(DataFolder.Prepare(AppDomain.CurrentDomain.BaseDirectory), "presets.json"));

        public void Load()
        {
            // Sin archivo (primera vez o carpeta nueva): se crea con los presets y ajustes por defecto.
            if (!File.Exists(FilePath))
            {
                Save();
                return;
            }

            var ser = new DataContractJsonSerializer(typeof(FileData));
            FileData data;
            using (var fs = File.OpenRead(FilePath))
                data = (FileData)ser.ReadObject(fs);

            LastApplied = data.LastApplied;
            LastChipPreset = data.LastChipPreset;
            bool hasCounts = data.LedCounts != null && data.LedCounts.Length == 3;
            LedCounts = hasCounts ? data.LedCounts : DefaultLedCounts();
            // Archivos anteriores a la calibración: si ya guardaban LEDs, se tomaron en este equipo.
            LedCountsCalibrated = data.LedCountsCalibrated ?? hasCounts;
            ByteOrders = data.ByteOrders != null && data.ByteOrders.Length == 3 ? data.ByteOrders : null;
            var loaded = data.Presets ?? new List<Preset>();

            // Los presets del programa van primero y en su orden oficial, conservando los ajustes guardados;
            // los que falten (versión anterior) se añaden. Después, los del usuario en su orden.
            var defaults = Preset.Defaults()
                .Select(d => loaded.FirstOrDefault(p => p.IsDefault && p.Name == d.Name) ?? d)
                .ToList();
            Presets = defaults.Concat(loaded.Where(p => !p.IsDefault)).ToList();
        }

        public void Save()
        {
            var data = new FileData { LastApplied = LastApplied, Presets = Presets, LastChipPreset = LastChipPreset, LedCounts = LedCounts, ByteOrders = ByteOrders, LedCountsCalibrated = LedCountsCalibrated };
            var ser = new DataContractJsonSerializer(typeof(FileData));
            using (var ms = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, false, true, "  "))
                    ser.WriteObject(writer, data);
                File.WriteAllBytes(FilePath, ms.ToArray());
            }
        }
    }
}
