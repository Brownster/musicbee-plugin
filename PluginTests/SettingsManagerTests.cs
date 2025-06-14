using System;
using System.IO;
using MusicBeePlugin;
using Xunit;

namespace PluginTests
{
    public class SettingsManagerTests
    {
        [Fact]
        public void SavesAndLoadsSettings()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var manager = new PluginSettingsManager(tempDir);
                manager.Settings.EndpointUrl = "http://example.com";
                manager.Save();

                var manager2 = new PluginSettingsManager(tempDir);
                Assert.Equal("http://example.com", manager2.Settings.EndpointUrl);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void DefaultsUsedWhenNoFileExists()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var manager = new PluginSettingsManager(tempDir);
                Assert.Equal("http://localhost:8000", manager.Settings.EndpointUrl);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
