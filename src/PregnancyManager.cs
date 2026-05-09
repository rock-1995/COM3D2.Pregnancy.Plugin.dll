using System.Globalization;
using System.IO;
using BepInEx;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public static class PregnancyManager
    {
        static string SettingsPath => Path.Combine(Paths.PluginPath, "pregnancy_settings.json");
        static readonly float[] FullCycleEggCoefficients = {
            0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f, 0f,
            1f, 0.5f,
            0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f, 0f
        };

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
            BellyMorphController.InflationStretchZ = Settings.bellyInflationStretchZ;
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
            BellyMorphController.InnerThighGuardStrength = Settings.bellyInnerThighGuardStrength;
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
            Settings.bellyInflationStretchZ = BellyMorphController.InflationStretchZ;
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
            Settings.bellyInnerThighGuardStrength = BellyMorphController.InnerThighGuardStrength;
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
        {
            ExSaveDataBridge.Set(maid, "isPregnant", value ? "true" : "false");
            if (value && IsCyclicMode(GetCycleMode()))
                SetCycleProgress(maid, GetPostOvulationSecondDayProgress(GetCycleMode()));
        }

        public static float GetProgress(Maid maid)
        {
            float.TryParse(ExSaveDataBridge.Get(maid, "progress", "0"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out float p);
            return Mathf.Clamp01(p);
        }

        public static void SetProgress(Maid maid, float value)
            => ExSaveDataBridge.Set(maid, "progress",
                value.ToString("F3", CultureInfo.InvariantCulture));

        public static FertilityCycleMode GetCycleMode()
        {
            return PregnancyPlugin.CfgCycleMode != null
                ? PregnancyPlugin.CfgCycleMode.Value
                : FertilityCycleMode.Simple;
        }

        public static bool IsCyclicMode(FertilityCycleMode mode)
            => mode == FertilityCycleMode.SevenDay
            || mode == FertilityCycleMode.TwentyEightDay;

        public static int GetCycleLength(FertilityCycleMode mode)
        {
            if (mode == FertilityCycleMode.SevenDay) return 7;
            if (mode == FertilityCycleMode.TwentyEightDay) return 28;
            return 0;
        }

        public static int GetCycleDay(float cycleProgress, int cycleLength)
        {
            if (cycleLength <= 0) return 0;
            int day = Mathf.FloorToInt(Mathf.Clamp01(cycleProgress) * cycleLength) + 1;
            return Mathf.Clamp(day, 1, cycleLength);
        }

        public static float EnsureCycleProgress(Maid maid)
        {
            string raw = ExSaveDataBridge.Get(maid, "cycleProgress", "");
            if (TryParseFloat(raw, out float p)) return Mathf.Clamp01(p);

            p = Random.value;
            SetCycleProgress(maid, p);
            return p;
        }

        public static void SetCycleProgress(Maid maid, float value)
            => ExSaveDataBridge.Set(maid, "cycleProgress",
                Mathf.Clamp01(value).ToString("F3", CultureInfo.InvariantCulture));

        public static float GetFertilityCoefficient(Maid maid)
        {
            string raw = ExSaveDataBridge.Get(maid, "fertilityCoefficient", "0");
            return TryParseFloat(raw, out float p) ? Mathf.Clamp01(p) : 0f;
        }

        public static void SetFertilityCoefficient(Maid maid, float value)
            => ExSaveDataBridge.Set(maid, "fertilityCoefficient",
                Mathf.Clamp01(value).ToString("F3", CultureInfo.InvariantCulture));

        public static void AdvanceDay()
        {
            int weeks = PregnancyPlugin.CfgPregnancyWeeks.Value;
            if (weeks <= 0) weeks = 40;
            float inc = 1f / (weeks * 7f);
            FertilityCycleMode mode = GetCycleMode();
            bool cyclic = IsCyclicMode(mode);

            var cm = GameMain.Instance?.CharacterMgr;
            if (cm == null) return;

            int cnt = cm.GetStockMaidCount();
            for (int i = 0; i < cnt; i++)
            {
                Maid m = cm.GetStockMaid(i);
                if (m == null) continue;

                if (cyclic)
                {
                    EnsureCycleProgress(m);
                    TryConceiveAtDayEnd(m, mode);
                }

                bool pregnant = GetPregnant(m);
                if (pregnant)
                {
                    float newProgress = GetProgress(m) + inc;
                    if (newProgress >= 1f)
                    {
                        SetProgress(m, newProgress - 1f);
                        SetPregnant(m, false);
                    }
                    else
                    {
                        SetProgress(m, newProgress);
                    }
                }

                if (cyclic)
                {
                    DecayFertilityCoefficient(m, mode);
                    if (!GetPregnant(m))
                        AdvanceCycleProgress(m, mode);
                }
            }
        }

        static void TryConceiveAtDayEnd(Maid maid, FertilityCycleMode mode)
        {
            if (GetPregnant(maid)) return;

            float fertilityCoefficient = GetFertilityCoefficient(maid);
            if (fertilityCoefficient <= 0f) return;

            float eggCoefficient = GetEggCoefficient(mode, EnsureCycleProgress(maid));
            float rate = fertilityCoefficient * PregnancyPlugin.CfgFertilityRate.Value * eggCoefficient;
            if (rate > 0f && Random.value < rate)
                SetPregnant(maid, true);
        }

        static void DecayFertilityCoefficient(Maid maid, FertilityCycleMode mode)
        {
            float coefficient = GetFertilityCoefficient(maid);
            if (coefficient <= 0f) return;

            coefficient *= mode == FertilityCycleMode.SevenDay ? 0.5f : 0.75f;
            if (coefficient < 0.1f) coefficient = 0f;
            SetFertilityCoefficient(maid, coefficient);
        }

        static void AdvanceCycleProgress(Maid maid, FertilityCycleMode mode)
        {
            int cycleLength = GetCycleLength(mode);
            if (cycleLength <= 0) return;

            float next = EnsureCycleProgress(maid) + 1f / cycleLength;
            if (next >= 1f) next = 0f;
            SetCycleProgress(maid, next);
        }

        static float GetEggCoefficient(FertilityCycleMode mode, float cycleProgress)
        {
            if (mode == FertilityCycleMode.SevenDay)
                return GetCycleDay(cycleProgress, 7) == 3 ? 1f : 0f;

            if (mode == FertilityCycleMode.TwentyEightDay)
            {
                int day = GetCycleDay(cycleProgress, 28);
                if (day < 1 || day > FullCycleEggCoefficients.Length) return 0f;
                return FullCycleEggCoefficients[day - 1];
            }

            return 0f;
        }

        static float GetPostOvulationSecondDayProgress(FertilityCycleMode mode)
        {
            int cycleLength = GetCycleLength(mode);
            if (cycleLength <= 0) return 0f;

            int lastOvulationDay = 0;
            if (mode == FertilityCycleMode.SevenDay)
            {
                lastOvulationDay = 3;
            }
            else if (mode == FertilityCycleMode.TwentyEightDay)
            {
                for (int i = 0; i < FullCycleEggCoefficients.Length; i++)
                    if (FullCycleEggCoefficients[i] > 0f)
                        lastOvulationDay = i + 1;
            }

            if (lastOvulationDay <= 0) return 0f;
            int targetDay = lastOvulationDay + 2;
            while (targetDay > cycleLength) targetDay -= cycleLength;
            return (targetDay - 1f) / cycleLength;
        }

        internal static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }
    }
}
