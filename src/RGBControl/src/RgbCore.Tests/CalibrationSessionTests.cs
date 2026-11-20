using System.Linq;
using RgbCore;
using Xunit;

namespace RgbCore.Tests
{
    public class CalibrationSessionTests
    {
        private const uint G = 0x00FF00, R = 0xFF0000;

        [Fact]
        public void StartsFromSavedCounts()
        {
            var s = new CalibrationSession(new[] { 0, 0, 8 });
            Assert.Equal(0, s.Connector);
            Assert.Equal(8, s.Count); // un conector a 0 empieza en 8
            Assert.True(s.Next());
            Assert.True(s.Next());
            Assert.Equal(2, s.Connector);
            Assert.Equal(8, s.Count);
        }

        [Fact]
        public void ClampsBetween1And120()
        {
            var s = new CalibrationSession(new[] { 10, 10, 10 });
            for (int i = 0; i < 200; i++)
                s.Increase();
            Assert.Equal(CalibrationSession.MaxCount, s.Count);
            Assert.Equal(120, s.Count);
            for (int i = 0; i < 200; i++)
                s.Decrease();
            Assert.Equal(1, s.Count);
        }

        [Fact]
        public void EmptyThenUncheck_RestoresLastCount()
        {
            var s = new CalibrationSession(new[] { 12, 8, 8 });
            s.SetEmpty(true);
            Assert.True(s.IsEmpty);
            Assert.Equal(0, s.Count);
            s.SetEmpty(false);
            Assert.False(s.IsEmpty);
            Assert.Equal(12, s.Count);
        }

        [Fact]
        public void Frame_LastLedRed_RestOff()
        {
            var s = new CalibrationSession(new[] { 3, 8, 8 });
            Assert.Equal(new[] { G, G, R, 0u, 0u, 0u, 0u, 0u }, s.Frame(0, 8));
            Assert.True(s.Frame(1, 8).All(c => c == 0));
            Assert.True(s.Frame(2, 8).All(c => c == 0));
        }

        [Fact]
        public void Frame_EmptyConnector_AllOff()
        {
            var s = new CalibrationSession(new[] { 3, 8, 8 });
            s.SetEmpty(true);
            Assert.True(s.Frame(0, 8).All(c => c == 0));
        }

        [Fact]
        public void Result_ReflectsEmptyConnectors()
        {
            var s = new CalibrationSession(new[] { 8, 8, 8 });
            s.SetEmpty(true);
            s.Next();
            for (int i = 0; i < 2; i++)
                s.Increase();
            s.Next();
            Assert.Equal(new[] { 0, 10, 8 }, s.Result());
        }

        [Fact]
        public void NavigationBounds()
        {
            var s = new CalibrationSession(new[] { 8, 8, 8 });
            Assert.False(s.Previous());
            Assert.False(s.IsLast);
            s.Next();
            Assert.False(s.IsLast);
            s.Next();
            Assert.True(s.IsLast);
            Assert.False(s.Next());
            Assert.True(s.Previous());
            Assert.Equal(1, s.Connector);
        }
    }
}
