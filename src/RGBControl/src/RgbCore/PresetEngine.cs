using System;

namespace RgbCore
{
    /// <summary>Traduce un preset a órdenes del chip, lo aplica y lo graba en su memoria.</summary>
    public static class PresetEngine
    {
        public static void Apply(Fusion2Controller c, Preset p, bool saveToFlash = true)
        {
            c.SetLampArrayMode(false);
            c.Reset();
            c.SetBeatMode(false);
            c.SetArgbBuiltinEffects(true);

            // Todas las zonas a negro antes de poner el efecto, para no dejar restos del anterior.
            c.SetEffect(-1, new EffectParams { Type = EffectType.Static, Color0 = 0 });
            c.Apply();

            if (p.Effect == PresetEffect.Rainbow)
            {
                // El arcoíris de fábrica del chip: se activa con una onda en la zona 2.
                c.SetEffect(2, new EffectParams { Type = EffectType.Wave4, Period0 = 200, Param0 = 7 });
                c.Apply();
            }
            else if (p.Effect != PresetEffect.Off)
            {
                c.SetEffect(-1, ToParams(p));
                c.Apply();
            }

            if (saveToFlash)
                c.SaveToFlash();
        }

        /// <summary>
        /// Parámetros de cada efecto. Los tiempos siguen los rangos del software de Gigabyte
        /// (documentados por OpenRGB); s va de 0 (más rápido) a 9 (más lento).
        /// </summary>
        public static EffectParams ToParams(Preset p)
        {
            int s = 10 - Clamp(p.Speed, 1, 10);
            var e = new EffectParams
            {
                MaxBrightness = (byte)Math.Round(Clamp(p.Brightness, 0, 100) * 2.55),
                Color0 = p.ColorValue,
            };

            switch (p.Effect)
            {
                case PresetEffect.Static:
                    e.Type = EffectType.Static;
                    break;
                case PresetEffect.Breathing:
                    e.Type = EffectType.Pulse;
                    e.Period0 = (ushort)(s <= 6 ? 400 + s * 100 : 1000 + (s - 6) * 200);
                    e.Period1 = e.Period0;
                    e.Period2 = 200;
                    break;
                case PresetEffect.Flash:
                    e.Type = EffectType.Flash;
                    e.Period0 = 100;
                    e.Period1 = 100;
                    e.Period2 = (ushort)(s * 200 + 700);
                    break;
                case PresetEffect.DoubleFlash:
                    e.Type = EffectType.Flash;
                    e.Period0 = 100;
                    e.Period1 = 100;
                    e.Period2 = (ushort)(s * 200 + 700);
                    e.Param1 = 1;
                    e.Param2 = 2;
                    break;
                case PresetEffect.ColorCycle:
                    e.Type = EffectType.ColorCycle;
                    e.Color0 = 0;
                    e.Period0 = (ushort)(s * 100 + 300 + (s > 8 ? 1300 * (s - 8) : 0));
                    e.Period1 = (ushort)(e.Period0 - 200);
                    e.Param0 = (byte)Clamp(p.ColorCount, 2, 7);
                    break;
                default:
                    e.Type = EffectType.Static;
                    e.Color0 = 0;
                    break;
            }
            return e;
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
