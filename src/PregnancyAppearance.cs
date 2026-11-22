using System;
using System.Collections.Generic;
using System.Globalization;

namespace COM3D2.Pregnancy.Plugin
{
    internal readonly struct AppearanceValues
    {
        internal readonly float Belly;
        internal readonly float Body;

        internal AppearanceValues(float belly, float body)
        {
            Belly = Unit(belly);
            Body = Unit(body);
        }

        internal static float Unit(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return Math.Max(0f, Math.Min(1f, value));
        }
    }

    // The installed ExSaveData owns the current slot and keys each entry by
    // maid.status.guid. Its Deserialize callback replaces the complete store.
    // Read that store directly: no GUID-only cache can leak into another slot.
    // These keys are deliberately not registered with ExternalPreset.
    internal static class PregnancyAppearance
    {
        internal const string BellyKey = "appearanceBellyProgress";
        internal const string BodyKey = "appearanceBodyProgress";

        private static readonly Dictionary<Maid, AppearanceValues> Previews =
            new Dictionary<Maid, AppearanceValues>();

        internal static AppearanceValues Get(Maid maid)
        {
            if (maid == null) return new AppearanceValues();
            AppearanceValues preview;
            return Previews.TryGetValue(maid, out preview) ? preview : ReadCommitted(maid);
        }

        internal static AppearanceValues ReadCommitted(Maid maid)
        {
            if (maid == null) return new AppearanceValues();
            float belly, body;
            bool haveBelly = TryRead(ExSaveDataBridge.Get(maid, BellyKey), out belly);
            bool haveBody = TryRead(ExSaveDataBridge.Get(maid, BodyKey), out body);
            if (!haveBelly || !haveBody)
            {
                var initial = FromSimulation(maid);
                // Migrate each missing field independently. Explicit zero is a
                // real saved appearance, not a request to use pregnancy state.
                if (!haveBelly) { belly = initial.Belly; Write(maid, BellyKey, belly); }
                if (!haveBody) { body = initial.Body; Write(maid, BodyKey, body); }
            }
            return new AppearanceValues(belly, body);
        }

        internal static void Set(Maid maid, float belly, float body)
        {
            if (maid == null) return;
            var values = new AppearanceValues(belly, body);
            Write(maid, BellyKey, values.Belly);
            Write(maid, BodyKey, values.Body);
        }

        internal static void RefreshFromSimulation(Maid maid)
        {
            if (maid == null) return;
            var values = FromSimulation(maid);
            Set(maid, values.Belly, values.Body);
        }

        internal static void EnsureCommittedForSave()
        {
            var game = GameMain.Instance;
            var manager = game == null ? null : game.CharacterMgr;
            if (manager == null) return;
            for (int i = 0; i < manager.GetStockMaidCount(); i++)
            {
                var maid = manager.GetStockMaid(i);
                if (maid != null) ReadCommitted(maid);
            }
        }

        private static AppearanceValues FromSimulation(Maid maid)
        {
            float belly = PregnancyManager.GetPregnant(maid) ? PregnancyManager.GetProgress(maid) : 0f;
            int births = PregnancyManager.GetBirthCount(maid);
            int target = PregnancyPlugin.CfgBodyChangeBirths.Value;
            float body = (float)BodyChangeMath.Factor(births, belly, target);
            return new AppearanceValues(belly, body);
        }

        internal static void BeginPreview(Maid maid)
        {
            if (maid != null && !Previews.ContainsKey(maid)) Previews.Add(maid, ReadCommitted(maid));
        }

        internal static void SetPreview(Maid maid, float belly, float body)
        {
            if (maid == null) return;
            Previews[maid] = new AppearanceValues(belly, body);
        }

        internal static void CommitPreview(Maid maid)
        {
            if (maid == null) return;
            AppearanceValues values;
            if (!Previews.TryGetValue(maid, out values)) return;
            Set(maid, values.Belly, values.Body);
            Previews.Remove(maid);
        }

        internal static void CancelPreview(Maid maid)
        {
            if (maid != null) Previews.Remove(maid);
        }

        // Called at game-slot load boundaries. Saving does not serialize the
        // preview dictionary, so an autosave retains the committed appearance.
        internal static void ClearPreviews() { Previews.Clear(); }

        private static bool TryRead(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void Write(Maid maid, string key, float value)
        {
            ExSaveDataBridge.Set(maid, key, value.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
