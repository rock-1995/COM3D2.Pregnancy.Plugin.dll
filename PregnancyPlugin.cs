using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class PregnancyPlugin : BaseUnityPlugin
    {
        public const string PluginGuid    = "com.pregnancymod.com3d2";
        public const string PluginName    = "COM3D2 Pregnancy";
        public const string PluginVersion = "0.2.0";

        // F1 に表示される設定
        internal static ConfigEntry<KeyCode> CfgToggleKey;
        internal static ConfigEntry<int>     CfgPregnancyWeeks;
        internal static ConfigEntry<float>   CfgFertilityRate;

        void Awake()
        {
            CfgToggleKey = Config.Bind(
                "General", "Toggle UI Key", KeyCode.F8,
                "Hotkey to open/close the Pregnancy UI window.");

            CfgPregnancyWeeks = Config.Bind(
                "General", "Pregnancy Weeks", 40,
                "Duration of pregnancy in weeks.");

            CfgFertilityRate = Config.Bind(
                "General", "Fertility Rate", 0.3f,
                "Base probability of conception per creampie (0.00 - 1.00).");

            try
            {
                new Harmony(PluginGuid).PatchAll();
                Logger.LogInfo("Harmony patches applied.");
            }
            catch (System.Exception e) { Logger.LogWarning("Harmony patch failed: " + e.Message); }

            try { PregnancyManager.Initialize(); }
            catch (System.Exception e) { Logger.LogWarning("Manager init failed: " + e.Message); }

            gameObject.AddComponent<PregnancyUI>();
            gameObject.AddComponent<SceneAutoApply>();
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }
    }
}
