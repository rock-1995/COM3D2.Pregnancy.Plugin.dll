using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class PregnancyPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.pregnancymod.com3d2";
        public const string PluginName = "COM3D2 Pregnancy";
        public const string PluginVersion = "0.2.3";

        internal static PregnancyPlugin Instance;
        internal static ConfigEntry<KeyCode> CfgToggleKey;
        internal static ConfigEntry<KeyCode> CfgMaidListKey;
        internal static ConfigEntry<int> CfgPregnancyWeeks;
        internal static ConfigEntry<float> CfgFertilityRate;
        internal static ConfigEntry<FertilityCycleMode> CfgCycleMode;
        internal static ConfigEntry<MorphTriggerMode> CfgMorphTriggerMode;
        internal static ConfigEntry<bool> CfgMorphSpyLogging;
        internal static ConfigEntry<bool> CfgDebugMeshLogging;
        internal static ConfigEntry<int> CfgBodyChangeBirths;
        internal static ConfigEntry<int> CfgBodyChangeLower;
        internal static ConfigEntry<int> CfgBodyChangeUpper;
        Harmony _harmony;
        int _aysHookAttempts;
        bool _aysHookReady;

        void Awake()
        {
            Instance = this;
            CfgToggleKey = Config.Bind(
                "General", "Toggle UI Key", KeyCode.F8,
                "Hotkey to open/close the Pregnancy UI window.");
            CfgMaidListKey = Config.Bind(
                "General", "Maid List UI Key", KeyCode.F9,
                "Hotkey to open/close the Maid Status list window.");

            CfgPregnancyWeeks = Config.Bind(
                "General", "Pregnancy Weeks", 40,
                "Duration of pregnancy in weeks.");

            CfgFertilityRate = Config.Bind(
                "General", "Fertility Rate", 0.3f,
                "Base probability of conception per creampie (0.00 - 1.00).");

            CfgCycleMode = Config.Bind(
                "General", "Fertility Cycle Mode", FertilityCycleMode.Simple,
                "Simple uses the fixed fertility rate on creampie. SevenDay and TwentyEightDay use cycle timing at day end.");

            CfgMorphTriggerMode = Config.Bind(
                "General", "Morph Trigger Mode", MorphTriggerMode.VisibilityChange,
                "When belly morphs are refreshed: ManualOnly or VisibilityChange.");

            CfgMorphSpyLogging = Config.Bind(
                "Debug", "Morph Spy Logging", true,
                "Log TMorph runtime-base bake events used for AddYotogiSlider compatibility.");

            CfgDebugMeshLogging = Config.Bind(
                "Debug", "Mesh Logging", false,
                "Log mesh load spy and belly morph diagnostics.");

            CfgBodyChangeBirths = ConfigKeyMigration.BindInt(Config,
                "Body Changes", "Births for Full Change", "\u8fbe\u5230\u5b8c\u6574\u53d8\u5316\u7684\u751f\u4ea7\u6b21\u6570 x", 5,
                "Number of births needed for the full body/color change. Before that: (pregnancy progress + births) / x. At or above x: full change. Integer; default 5. Zero or negative x uses the full change.");
            CfgBodyChangeLower = ConfigKeyMigration.BindInt(Config,
                "Body Changes", "Body Value Minimum", "\u4f53\u578b\u6570\u503c\u4e0b\u9650", 0,
                "Minimum final body-slider value, in the game's slider units. Integer input; negative values and values above 100 are allowed. Does not limit colors or change saved character data. Reversed bounds are sorted for calculation.");
            CfgBodyChangeUpper = ConfigKeyMigration.BindInt(Config,
                "Body Changes", "Body Value Maximum", "\u4f53\u578b\u6570\u503c\u4e0a\u9650", 100,
                "Maximum final body-slider value, in the game's slider units. Integer input; negative values and values above 100 are allowed. New settings take effect on the next deformation.");

            try
            {
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll();
                BellyMorphController.PatchAysHooks(_harmony);
                Logger.LogInfo("Harmony patches applied. Conception uses vaginal script effects (story/private/yotogi).");
            }
            catch (System.Exception e)
            {
                Logger.LogWarning("Harmony patch failed: " + e.Message);
            }

            try
            {
                PregnancyManager.Initialize();
            }
            catch (System.Exception e)
            {
                Logger.LogWarning("Manager init failed: " + e.Message);
            }

            gameObject.AddComponent<PregnancyUI>();
            gameObject.AddComponent<MaidListUI>();
            gameObject.AddComponent<SceneAutoApply>();
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }

        void Start()
        {
            _aysHookAttempts = 0;
            _aysHookReady = BellyMorphController.PatchAysHooks(_harmony);
        }

        void Update()
        {
            if (_aysHookReady || _aysHookAttempts >= 600)
                return;

            _aysHookAttempts++;
            _aysHookReady = BellyMorphController.PatchAysHooks(_harmony);
        }

    }
}
