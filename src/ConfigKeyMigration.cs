using System.IO;
using BepInEx.Configuration;

namespace COM3D2.Pregnancy.Plugin
{
    internal static class ConfigKeyMigration
    {
        internal static ConfigEntry<int> BindInt(ConfigFile config, string section, string key,
            string legacyKey, int defaultValue, string description)
        {
            ConfigEntry<int> existing;
            bool hasEnglishValue = config.TryGetEntry(section, key, out existing)
                || HasSavedKey(config.ConfigFilePath, section, key);
            bool autoSave = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                // Bind and remove the old key through the public API so orphaned
                // entries are consumed too. The new entry keeps its real default.
                var legacy = config.Bind(section, legacyKey, defaultValue);
                int previous = legacy.Value;
                config.Remove(legacy.Definition);
                var current = config.Bind(section, key, defaultValue, description);
                if (!hasEnglishValue) current.Value = previous;
                return current;
            }
            finally
            {
                config.SaveOnConfigSet = autoSave;
                if (autoSave) config.Save();
            }
        }

        static bool HasSavedKey(string path, string section, string key)
        {
            if (!File.Exists(path)) return false;
            string currentSection = "";
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("#") || line.Length == 0) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                else if (currentSection == section)
                {
                    int equals = line.IndexOf('=');
                    if (equals >= 0 && line.Substring(0, equals).Trim() == key) return true;
                }
            }
            return false;
        }
    }
}
