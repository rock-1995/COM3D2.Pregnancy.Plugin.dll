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
            {
                try
                {
                    string json = File.ReadAllText(SettingsPath);
                    bool hasOuterClothPregnancyScale = json.Contains("bellyOuterClothPregnancyScale");
                    bool hasOuterClothLayerGuard = json.Contains("bellyOuterClothLayerGuard");
                    Settings = JsonUtility.FromJson<PregSettings>(
                        json) ?? new PregSettings();
                    if (!hasOuterClothPregnancyScale)
                        Settings.bellyOuterClothPregnancyScale = 1.0f;
                    if (!hasOuterClothLayerGuard)
                        Settings.bellyOuterClothLayerGuard = 0.0f;
                }
                catch { }
            }

            ApplyGlobalBellySettings();
        }

        public static void SaveSettings()
        {
            try { File.WriteAllText(SettingsPath, JsonUtility.ToJson(Settings, true)); }
            catch { }
        }

        public static void ApplyGlobalBellySettings()
        {
            BellyMorphController.InflationMultiplier = Settings.bellyInflationMultiplier;
            BellyMorphController.InflationMoveY = Settings.bellyInflationMoveY;
            BellyMorphController.InflationMoveZ = Settings.bellyInflationMoveZ;
            BellyMorphController.InflationStretchX = Settings.bellyInflationStretchX;
            BellyMorphController.InflationStretchY = Settings.bellyInflationStretchY;
            BellyMorphController.InflationShiftY = Settings.bellyInflationShiftY;
            BellyMorphController.InflationShiftZ = Settings.bellyInflationShiftZ;
            BellyMorphController.InflationTaperY = Settings.bellyInflationTaperY;
            BellyMorphController.InflationTaperZ = Settings.bellyInflationTaperZ;
            BellyMorphController.InflationRoundness = Settings.bellyInflationRoundness;
            BellyMorphController.InflationDrop = Settings.bellyInflationDrop;
            BellyMorphController.InflationFatFold = Settings.bellyInflationFatFold;
            BellyMorphController.InflationFatFoldHeight = Settings.bellyInflationFatFoldHeight;
            BellyMorphController.InflationFatFoldGap = Settings.bellyInflationFatFoldGap;
            BellyMorphController.RegionRadiusSide = Settings.bellyRegionRadiusSide;
            BellyMorphController.RegionRadiusFront = Settings.bellyRegionRadiusFront;
            BellyMorphController.RegionRadiusBack = Settings.bellyRegionRadiusBack;
            BellyMorphController.RegionRadiusUp = Settings.bellyRegionRadiusUp;
            BellyMorphController.RegionRadiusDown = Settings.bellyRegionRadiusDown;
            BellyMorphController.ThighGuardSpeed = Settings.bellyThighGuardSpeed;
            BellyMorphController.TopEdgeTaper = Settings.bellyTopEdgeTaper;
            BellyMorphController.BottomEdgeTaper = Settings.bellyBottomEdgeTaper;
            BellyMorphController.SideSmoothWidth = Settings.bellySideSmoothWidth;
            BellyMorphController.SideSmoothStrength = Settings.bellySideSmoothStrength;
            BellyMorphController.BreastGuardStrength = Settings.bellyBreastGuardStrength;
            BellyMorphController.OuterClothPregnancyScale = Settings.bellyOuterClothPregnancyScale;
            BellyMorphController.OuterClothSkirtDrape = Settings.bellyOuterClothSkirtDrape;
            BellyMorphController.OuterClothLayerGuard = Settings.bellyOuterClothLayerGuard;
            BellyMorphController.InnerClothOffset = Settings.bellyInnerClothOffset;
            BellyMorphController.OuterClothOffset = Settings.bellyOuterClothOffset;
            BellyMorphController.ClothThicknessPreserve = Settings.bellyClothThicknessPreserve;
            BellyMorphController.ClothOffsetSideRatio = Settings.bellyClothOffsetSideRatio;
            BellyMorphController.ClothBackOffsetBoost = Settings.bellyClothBackOffsetBoost;
            BellyMorphController.ClothDepthStretch = Settings.bellyClothDepthStretch;
        }

        public static void CaptureCurrentBellySettings()
        {
            Settings.bellyInflationMultiplier = BellyMorphController.InflationMultiplier;
            Settings.bellyInflationMoveY = BellyMorphController.InflationMoveY;
            Settings.bellyInflationMoveZ = BellyMorphController.InflationMoveZ;
            Settings.bellyInflationStretchX = BellyMorphController.InflationStretchX;
            Settings.bellyInflationStretchY = BellyMorphController.InflationStretchY;
            Settings.bellyInflationShiftY = BellyMorphController.InflationShiftY;
            Settings.bellyInflationShiftZ = BellyMorphController.InflationShiftZ;
            Settings.bellyInflationTaperY = BellyMorphController.InflationTaperY;
            Settings.bellyInflationTaperZ = BellyMorphController.InflationTaperZ;
            Settings.bellyInflationRoundness = BellyMorphController.InflationRoundness;
            Settings.bellyInflationDrop = BellyMorphController.InflationDrop;
            Settings.bellyInflationFatFold = BellyMorphController.InflationFatFold;
            Settings.bellyInflationFatFoldHeight = BellyMorphController.InflationFatFoldHeight;
            Settings.bellyInflationFatFoldGap = BellyMorphController.InflationFatFoldGap;
            Settings.bellyRegionRadiusSide = BellyMorphController.RegionRadiusSide;
            Settings.bellyRegionRadiusFront = BellyMorphController.RegionRadiusFront;
            Settings.bellyRegionRadiusBack = BellyMorphController.RegionRadiusBack;
            Settings.bellyRegionRadiusUp = BellyMorphController.RegionRadiusUp;
            Settings.bellyRegionRadiusDown = BellyMorphController.RegionRadiusDown;
            Settings.bellyThighGuardSpeed = BellyMorphController.ThighGuardSpeed;
            Settings.bellyTopEdgeTaper = BellyMorphController.TopEdgeTaper;
            Settings.bellyBottomEdgeTaper = BellyMorphController.BottomEdgeTaper;
            Settings.bellySideSmoothWidth = BellyMorphController.SideSmoothWidth;
            Settings.bellySideSmoothStrength = BellyMorphController.SideSmoothStrength;
            Settings.bellyBreastGuardStrength = BellyMorphController.BreastGuardStrength;
            Settings.bellyOuterClothPregnancyScale = BellyMorphController.OuterClothPregnancyScale;
            Settings.bellyOuterClothSkirtDrape = BellyMorphController.OuterClothSkirtDrape;
            Settings.bellyOuterClothLayerGuard = BellyMorphController.OuterClothLayerGuard;
            Settings.bellyInnerClothOffset = BellyMorphController.InnerClothOffset;
            Settings.bellyOuterClothOffset = BellyMorphController.OuterClothOffset;
            Settings.bellyClothThicknessPreserve = BellyMorphController.ClothThicknessPreserve;
            Settings.bellyClothOffsetSideRatio = BellyMorphController.ClothOffsetSideRatio;
            Settings.bellyClothBackOffsetBoost = BellyMorphController.ClothBackOffsetBoost;
            Settings.bellyClothDepthStretch = BellyMorphController.ClothDepthStretch;
            SaveSettings();
        }

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
