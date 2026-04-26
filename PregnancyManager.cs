using System;
using System.Globalization;
using System.IO;
using BepInEx;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public static class PregnancyManager
    {
        static string SettingsPath => Path.Combine(Paths.PluginPath, "pregnancy_settings.json");
        public static PregSettings Settings { get; private set; } = new PregSettings();

        public static void Initialize()
        {
            if (File.Exists(SettingsPath))
                try { Settings = JsonUtility.FromJson<PregSettings>(
                          File.ReadAllText(SettingsPath)) ?? new PregSettings(); }
                catch { }
        }

        public static void SaveSettings()
        {
            try { File.WriteAllText(SettingsPath, JsonUtility.ToJson(Settings, true)); }
            catch { }
        }

        // ── ExSaveData 経由データ ──────────────────────────────────

        public static bool GetPregnant(Maid maid)
            => ExSaveDataBridge.Get(maid, "isPregnant", "false") == "true";

        public static void SetPregnant(Maid maid, bool value)
            => ExSaveDataBridge.Set(maid, "isPregnant", value ? "true" : "false");

        public static float GetProgress(Maid maid)
        {
            float.TryParse(ExSaveDataBridge.Get(maid, "progress", "0"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out float p);
            return Mathf.Clamp01(p);
        }

        public static void SetProgress(Maid maid, float value)
            => ExSaveDataBridge.Set(maid, "progress",
                value.ToString("F3", CultureInfo.InvariantCulture));

        // ── 日付進行 ──────────────────────────────────────────────

        public static void AdvanceDay()
        {
            int weeks = PregnancyPlugin.CfgPregnancyWeeks.Value;
            if (weeks <= 0) weeks = 40;
            float inc = 1f / (weeks * 7f);

            var cm = GameMain.Instance?.CharacterMgr;
            if (cm == null) return;

            int cnt = cm.GetStockMaidCount();
            for (int i = 0; i < cnt; i++)
            {
                Maid m = cm.GetStockMaid(i);
                if (m == null) continue;
                if (!GetPregnant(m)) continue;
                SetProgress(m, Mathf.Clamp01(GetProgress(m) + inc));
            }
        }
    }
}
