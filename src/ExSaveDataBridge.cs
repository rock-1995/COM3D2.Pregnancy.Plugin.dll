using System;
using System.Reflection;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public static class ExSaveDataBridge
    {
        static Type       _type;
        static MethodInfo _get;
        static MethodInfo _set;
        static bool       _initialized;
        static bool       _available;
        static readonly BepInEx.Logging.ManualLogSource _log =
            BepInEx.Logging.Logger.CreateLogSource("Pregnancy");

        static bool Init()
        {
            if (_initialized) return _available;
            _initialized = true;

            // 確認済みの正しいクラス名を先頭に
            string[] candidates = {
                "CM3D2.ExternalSaveData.Managed.ExSaveData",   // ← 確認済み
                "CM3D2.ExSaveData.Managed.ExSaveData",
                "COM3D2.ExSaveData.Managed.ExSaveData",
                "CM3D2.ExSaveData.ExSaveData",
                "COM3D2.ExSaveData.ExSaveData",
                "ExSaveData",
            };

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                foreach (var name in candidates)
                {
                    try
                    {
                        var t = asm.GetType(name);
                        if (!object.ReferenceEquals(t, null)) { _type = t; break; }
                    }
                    catch { }
                }

            if (object.ReferenceEquals(_type, null))
            {
                Log("ExSaveData class not found. Check log above for 'Found candidate' lines.");
                _available = false;
                return false;
            }

            Log("Using ExSaveData type: " + _type.FullName);

            _get = _type.GetMethod("Get",
                new[] { typeof(Maid), typeof(string), typeof(string), typeof(string) });
            _set = _type.GetMethod("Set",
                new[] { typeof(Maid), typeof(string), typeof(string), typeof(string) });

            _available = !object.ReferenceEquals(_get, null)
                      && !object.ReferenceEquals(_set, null);

            Log(_available
                ? "ExSaveData bridge OK."
                : "ExSaveData found but Get/Set method signatures mismatch.");
            return _available;
        }

        public static string Get(Maid maid, string key, string def = "")
        {
            try
            {
                if (!Init()) return def;
                return (string)_get.Invoke(null,
                    new object[] { maid, PregnancyPlugin.PluginGuid, key, def });
            }
            catch { return def; }
        }

        public static void Set(Maid maid, string key, string value)
        {
            try
            {
                if (!Init()) return;
                _set.Invoke(null,
                    new object[] { maid, PregnancyPlugin.PluginGuid, key, value });
            }
            catch { }
        }

        static void Log(string msg)
            => _log.LogInfo("[Pregnancy] " + msg);
    }
}
