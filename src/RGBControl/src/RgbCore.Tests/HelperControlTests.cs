using System;
using System.Threading;
using Microsoft.Win32;
using RgbCore;
using Xunit;

namespace RgbCore.Tests
{
    public class HelperControlTests : IDisposable
    {
        private const string Root = @"Software\RGBControlTests";
        private readonly string suffix = "-test-" + Guid.NewGuid().ToString("N");
        private readonly HelperControl control;

        public HelperControlTests()
        {
            control = new HelperControl(Root + @"\Run", Root + @"\Settings", suffix);
        }

        public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(Root, false);

        [Fact]
        public void Preference_DefaultsToTrue_WhenMissing() => Assert.True(control.AutostartPreference);

        [Fact]
        public void Preference_RoundTrips()
        {
            control.AutostartPreference = false;
            Assert.False(new HelperControl(Root + @"\Run", Root + @"\Settings", suffix).AutostartPreference);
            control.AutostartPreference = true;
            Assert.True(control.AutostartPreference);
        }

        [Fact]
        public void Register_WritesQuotedPath()
        {
            control.RegisterAutostart(@"C:\x\RGB Helper.exe");
            Assert.Equal("\"C:\\x\\RGB Helper.exe\"", control.RegisteredCommand);
            control.RegisterAutostart(@"D:\y\RGB Helper.exe");
            Assert.Equal("\"D:\\y\\RGB Helper.exe\"", control.RegisteredCommand);
        }

        [Fact]
        public void Unregister_IsIdempotent()
        {
            control.UnregisterAutostart();
            control.RegisterAutostart(@"C:\x\RGB Helper.exe");
            control.UnregisterAutostart();
            control.UnregisterAutostart();
            Assert.Null(control.RegisteredCommand);
        }

        [Fact]
        public void SignalReload_SetsExistingEvent()
        {
            using (var ev = new EventWaitHandle(false, EventResetMode.AutoReset, "RGBControl.Reload" + suffix))
            {
                Assert.False(ev.WaitOne(0));
                control.SignalReload();
                Assert.True(ev.WaitOne(0));
            }
        }

        [Fact]
        public void Signals_WithoutHelper_DoNothing()
        {
            control.SignalReload();
            control.SignalExit();
        }

        [Fact]
        public void IsHelperRunning_ReflectsMutex()
        {
            Assert.False(control.IsHelperRunning());
            using (new Mutex(true, "RGBControl.Helper" + suffix))
                Assert.True(control.IsHelperRunning());
        }
    }
}
