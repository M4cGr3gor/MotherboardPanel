using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;

namespace RgbCore
{
    /// <summary>
    /// Efectos que ofrece el programa (todos los reproduce el chip por sí solo).
    /// Se guardan en presets.json por su número: no cambiar el orden.
    /// </summary>
    public enum PresetEffect
    {
        Rainbow,
        Static,
        Breathing,
        Flash,
        DoubleFlash,
        ColorCycle,
        Off,

        // Efectos avanzados: los genera el ayudante (RGB Helper.exe) LED a LED.
        GradientStatic,
        GradientRotate,
        CustomCycle,
        RainbowAdjustable,
        Comet,
        BreathingMulti,
        Twinkle,

        // Efectos decorativos (también los genera el ayudante).
        Fire,
        Candle,
        Heartbeat,
        Aurora,
        Scanner,
        Chase,
        DoubleComet,
        Storm,
    }

    [DataContract]
    public sealed class Preset
    {
        [DataMember(Order = 0)] public string Name { get; set; }
        [DataMember(Order = 1)] public PresetEffect Effect { get; set; }
        /// <summary>Color en formato #RRGGBB.</summary>
        [DataMember(Order = 2)] public string Color { get; set; } = "#0050FF";
        /// <summary>Brillo 0-100 %.</summary>
        [DataMember(Order = 3)] public int Brightness { get; set; } = 100;
        /// <summary>Velocidad 1 (lenta) - 10 (rápida).</summary>
        [DataMember(Order = 4)] public int Speed { get; set; } = 5;
        /// <summary>Colores de la paleta del chip que recorre el ciclo (2-7).</summary>
        [DataMember(Order = 5)] public int ColorCount { get; set; } = 7;
        /// <summary>Preset que trae el programa: se pueden cambiar sus ajustes, pero no su nombre, su efecto ni borrarlo.</summary>
        [DataMember(Order = 6)] public bool IsDefault { get; set; }
        /// <summary>Icono elegido en la lista (clave de PresetIcons); vacío = el del efecto.</summary>
        [DataMember(Order = 7, EmitDefaultValue = false)] public string Icon { get; set; }
        /// <summary>Colores #RRGGBB de los efectos avanzados con lista de colores (2-8).</summary>
        [DataMember(Order = 8, EmitDefaultValue = false)] public List<string> Colors { get; set; }
        /// <summary>Sentido de giro de los efectos que se mueven por el anillo.</summary>
        [DataMember(Order = 9)] public bool Clockwise { get; set; } = true;
        /// <summary>Color de fondo (#RRGGBB) de Cometa y Destellos.</summary>
        [DataMember(Order = 10)] public string Background { get; set; } = "#000000";
        /// <summary>Longitud de la estela del Cometa, 1-10 LEDs.</summary>
        [DataMember(Order = 11)] public int Trail { get; set; } = 5;
        /// <summary>Densidad de Destellos, 1-10.</summary>
        [DataMember(Order = 12)] public int Density { get; set; } = 5;

        public const int MinColors = 2;
        public const int MaxColors = 8;

        // DataContractJsonSerializer no ejecuta los inicializadores al leer: se restauran los
        // valores por defecto de los campos que falten en archivos antiguos.
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            Clockwise = true;
            Background = "#000000";
            Trail = 5;
            Density = 5;
        }

        public Preset Clone()
        {
            var p = (Preset)MemberwiseClone();
            p.Colors = Colors == null ? null : new List<string>(Colors);
            return p;
        }

        /// <summary>Lista de colores lista para usar: 2-8 entradas; un valor inválido pasa a blanco.</summary>
        public uint[] ColorValues()
        {
            var result = new List<uint>();
            foreach (string c in Colors ?? new List<string>())
            {
                if (result.Count == MaxColors)
                    break;
                result.Add(ParseHex(c));
            }
            while (result.Count < MinColors)
                result.Add(ColorValue);
            return result.ToArray();
        }

        public uint BackgroundValue => ParseHex(Background);

        private static uint ParseHex(string s)
        {
            uint v;
            s = (s ?? "").Trim().TrimStart('#');
            return s.Length == 6 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v) ? v : 0xFFFFFF;
        }

        public static bool IsAdvanced(PresetEffect e) => e >= PresetEffect.GradientStatic && e <= PresetEffect.Storm;

        public static bool UsesColorList(PresetEffect e) =>
            e == PresetEffect.GradientStatic || e == PresetEffect.GradientRotate || e == PresetEffect.CustomCycle ||
            e == PresetEffect.BreathingMulti || e == PresetEffect.Twinkle || e == PresetEffect.Aurora ||
            e == PresetEffect.Chase || e == PresetEffect.DoubleComet;

        public static bool UsesBackground(PresetEffect e) =>
            e == PresetEffect.Comet || e == PresetEffect.Twinkle || e == PresetEffect.Scanner || e == PresetEffect.Chase ||
            e == PresetEffect.DoubleComet || e == PresetEffect.Storm;

        public static bool UsesDirection(PresetEffect e) =>
            e == PresetEffect.GradientRotate || e == PresetEffect.RainbowAdjustable || e == PresetEffect.Comet ||
            e == PresetEffect.Chase || e == PresetEffect.DoubleComet;

        public static bool UsesTrail(PresetEffect e) =>
            e == PresetEffect.Comet || e == PresetEffect.Scanner || e == PresetEffect.DoubleComet;

        public static bool UsesDensity(PresetEffect e) => e == PresetEffect.Twinkle || e == PresetEffect.Storm;

        public uint ColorValue
        {
            get
            {
                uint v;
                string s = (Color ?? "").TrimStart('#');
                return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v) ? v & 0xFFFFFF : 0xFFFFFF;
            }
            set { Color = "#" + (value & 0xFFFFFF).ToString("X6"); }
        }

        public static bool UsesColor(PresetEffect e) =>
            e == PresetEffect.Static || e == PresetEffect.Breathing || e == PresetEffect.Flash || e == PresetEffect.DoubleFlash ||
            e == PresetEffect.Comet || e == PresetEffect.Candle || e == PresetEffect.Heartbeat || e == PresetEffect.Scanner ||
            e == PresetEffect.Storm;

        public static bool UsesBrightness(PresetEffect e) => e != PresetEffect.Rainbow && e != PresetEffect.Off;

        public static bool UsesSpeed(PresetEffect e) =>
            e == PresetEffect.Breathing || e == PresetEffect.Flash || e == PresetEffect.DoubleFlash || e == PresetEffect.ColorCycle ||
            (IsAdvanced(e) && e != PresetEffect.GradientStatic && e != PresetEffect.Storm);

        public static string EffectName(PresetEffect e)
        {
            switch (e)
            {
                case PresetEffect.Rainbow: return "Arcoíris en movimiento";
                case PresetEffect.Static: return "Color fijo";
                case PresetEffect.Breathing: return "Respiración";
                case PresetEffect.Flash: return "Parpadeo";
                case PresetEffect.DoubleFlash: return "Doble parpadeo";
                case PresetEffect.ColorCycle: return "Ciclo de colores";
                case PresetEffect.Off: return "Apagado";
                case PresetEffect.GradientStatic: return "Degradado fijo";
                case PresetEffect.GradientRotate: return "Degradado giratorio";
                case PresetEffect.CustomCycle: return "Ciclo de colores propio";
                case PresetEffect.RainbowAdjustable: return "Arcoíris ajustable";
                case PresetEffect.Comet: return "Cometa";
                case PresetEffect.BreathingMulti: return "Respiración multicolor";
                case PresetEffect.Twinkle: return "Destellos";
                case PresetEffect.Fire: return "Fuego";
                case PresetEffect.Candle: return "Vela";
                case PresetEffect.Heartbeat: return "Latido";
                case PresetEffect.Aurora: return "Aurora";
                case PresetEffect.Scanner: return "Escáner";
                case PresetEffect.Chase: return "Persecución";
                case PresetEffect.DoubleComet: return "Doble cometa";
                case PresetEffect.Storm: return "Tormenta";
                default: return e.ToString();
            }
        }

        /// <summary>Presets del programa, en el orden en que se muestran ("Apagado" siempre el último).</summary>
        public static List<Preset> Defaults()
        {
            return new List<Preset>
            {
                new Preset { Name = "Onda arcoíris", Effect = PresetEffect.Rainbow, IsDefault = true },
                new Preset { Name = "Ciclo de colores", Effect = PresetEffect.ColorCycle, Speed = 5, ColorCount = 7, IsDefault = true },
                new Preset { Name = "Respiración", Effect = PresetEffect.Breathing, Color = "#0050FF", Speed = 5, IsDefault = true },
                new Preset { Name = "Color fijo", Effect = PresetEffect.Static, Color = "#FFFFFF", IsDefault = true },
                new Preset
                {
                    Name = "Degradado giratorio", Effect = PresetEffect.GradientRotate, IsDefault = true,
                    Colors = new List<string> { "#8000FF", "#0040FF", "#00E0FF" },
                },
                new Preset { Name = "Apagado", Effect = PresetEffect.Off, IsDefault = true },
            };
        }
    }
}
