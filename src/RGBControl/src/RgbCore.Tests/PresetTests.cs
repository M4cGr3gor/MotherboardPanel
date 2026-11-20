using System.Collections.Generic;
using System.Linq;
using RgbCore;
using Xunit;

namespace RgbCore.Tests
{
    public class PresetTests
    {
        [Fact]
        public void ColorValues_ClampsAndRepairs()
        {
            var one = new Preset { Colors = new List<string> { "#FF0000" } };
            uint[] v = one.ColorValues();
            Assert.Equal(2, v.Length);
            Assert.Equal(0xFF0000u, v[0]);

            var nine = new Preset { Colors = Enumerable.Repeat("#00FF00", 9).ToList() };
            Assert.Equal(8, nine.ColorValues().Length);

            var bad = new Preset { Colors = new List<string> { "#ZZZ", "#0000FF" } };
            Assert.Equal(0xFFFFFFu, bad.ColorValues()[0]);
            Assert.Equal(0x0000FFu, bad.ColorValues()[1]);
        }

        [Fact]
        public void NewEffects_AreAdvanced()
        {
            for (int e = 0; e <= 6; e++)
                Assert.False(Preset.IsAdvanced((PresetEffect)e));
            for (int e = 7; e <= 21; e++)
                Assert.True(Preset.IsAdvanced((PresetEffect)e));
            Assert.Equal(8, (int)PresetEffect.GradientRotate);
            Assert.Equal(13, (int)PresetEffect.Twinkle);
            // Los decorativos: mismos números que el enum Effect del ayudante en C++.
            Assert.Equal(14, (int)PresetEffect.Fire);
            Assert.Equal(15, (int)PresetEffect.Candle);
            Assert.Equal(16, (int)PresetEffect.Heartbeat);
            Assert.Equal(17, (int)PresetEffect.Aurora);
            Assert.Equal(18, (int)PresetEffect.Scanner);
            Assert.Equal(19, (int)PresetEffect.Chase);
            Assert.Equal(20, (int)PresetEffect.DoubleComet);
            Assert.Equal(21, (int)PresetEffect.Storm);
        }

        [Fact]
        public void DecorativeEffects_HaveSpanishNames_AndExpectedSettings()
        {
            Assert.Equal("Fuego", Preset.EffectName(PresetEffect.Fire));
            Assert.Equal("Tormenta", Preset.EffectName(PresetEffect.Storm));
            Assert.True(Preset.UsesColor(PresetEffect.Candle));
            Assert.True(Preset.UsesColor(PresetEffect.Heartbeat));
            Assert.True(Preset.UsesColorList(PresetEffect.Aurora));
            Assert.True(Preset.UsesColorList(PresetEffect.DoubleComet));
            Assert.True(Preset.UsesTrail(PresetEffect.Scanner));
            Assert.True(Preset.UsesDensity(PresetEffect.Storm));
            Assert.False(Preset.UsesSpeed(PresetEffect.Storm));
            Assert.False(Preset.UsesColor(PresetEffect.Fire));
            Assert.False(Preset.UsesColorList(PresetEffect.Fire));
        }
    }
}
