using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace MusicBeePlugin
{
    /// <summary>
    /// Provides loading and saving of <see cref="PluginSettings"/> to the
    /// persistent storage directory supplied by MusicBee.
    /// </summary>
    public class PluginSettingsManager
    {
        private readonly string _settingsPath;

        /// <summary>
        /// Gets the settings being managed.
        /// </summary>
        public PluginSettings Settings { get; private set; }

        /// <summary>
        /// Initialize a new manager storing settings in the given directory.
        /// </summary>
        /// <param name="storagePath">Directory returned by MusicBee.</param>
        public PluginSettingsManager(string storagePath)
        {
            if (string.IsNullOrEmpty(storagePath))
                throw new ArgumentException("storagePath is required", nameof(storagePath));

            _settingsPath = Path.Combine(storagePath, "settings.json");
            Settings = Load();
        }

        /// <summary>
        /// Load settings from disk or return defaults if none exist.
        /// </summary>
        private PluginSettings Load()
        {
            if (File.Exists(_settingsPath))
            {
                using (var stream = File.OpenRead(_settingsPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(PluginSettings));
                    return (PluginSettings)serializer.ReadObject(stream);
                }
            }
            return new PluginSettings();
        }

        /// <summary>
        /// Persist the current settings to disk.
        /// </summary>
        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath));
            using (var stream = File.Create(_settingsPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(PluginSettings));
                serializer.WriteObject(stream, Settings);
            }
        }
    }
}
