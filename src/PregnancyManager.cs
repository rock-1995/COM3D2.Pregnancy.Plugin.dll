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
                    Settings = JsonUtility.FromJson<PregSettings>(json) ?? new PregSettings();
                    // JsonUtility may zero missing fields in old files. Never reinterpret old shape knobs.
                    if (!json.Contains("growthModelVersion") || Settings.growth == null)
                        Settings.growth = new Growth.VtxSettings();
                    Settings.growth.CompleteGrowthDefaults(json);
                    Settings.growthModelVersion = 1;
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
            BellyMorphController.Shape = (Settings.growth ?? new Growth.VtxSettings()).Copy();
        }

        public static void CaptureCurrentBellySettings()
        {
            Settings.growthModelVersion = 1;
            Settings.growth = BellyMorphController.Shape.Copy();
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
