using System;
using System.IO;
using RgbCore;
using Xunit;

namespace RgbCore.Tests
{
    public class DataFolderTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "rgb-datos-" + Guid.NewGuid().ToString("N"));
        private string Legacy => Path.Combine(root, "programa");
        private string Data => Path.Combine(root, "datos");

        public DataFolderTests() => Directory.CreateDirectory(Legacy);

        public void Dispose()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Fact]
        public void Prepare_CreatesEmptyFolder_WhenNothingToCopy()
        {
            Assert.Equal(Data, DataFolder.Prepare(Legacy, Data));
            Assert.True(Directory.Exists(Data));
            Assert.Empty(Directory.GetFiles(Data));
        }

        [Fact]
        public void Prepare_CopiesFilesFromProgramFolder()
        {
            File.WriteAllText(Path.Combine(Legacy, "presets.json"), "antiguo");
            File.WriteAllText(Path.Combine(Legacy, "helper.cfg"), "version=1");

            DataFolder.Prepare(Legacy, Data);

            Assert.Equal("antiguo", File.ReadAllText(Path.Combine(Data, "presets.json")));
            Assert.Equal("version=1", File.ReadAllText(Path.Combine(Data, "helper.cfg")));
            Assert.True(File.Exists(Path.Combine(Legacy, "presets.json"))); // se copia, no se mueve
        }

        [Fact]
        public void Prepare_KeepsExistingUserData()
        {
            Directory.CreateDirectory(Data);
            File.WriteAllText(Path.Combine(Data, "presets.json"), "actual");
            File.WriteAllText(Path.Combine(Legacy, "presets.json"), "antiguo");

            DataFolder.Prepare(Legacy, Data);

            Assert.Equal("actual", File.ReadAllText(Path.Combine(Data, "presets.json")));
        }
    }
}
