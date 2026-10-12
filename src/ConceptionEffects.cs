using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace COM3D2.Pregnancy.Plugin
{
    internal static class ConceptionEffects
    {
        internal static readonly EffectEventGate Gate = new EffectEventGate();
        [ThreadStatic] internal static ScriptManagerFast.BaseKagManagerFast CurrentFast;

        static readonly FieldInfo FastManager = AccessTools.Field(typeof(ScriptManagerFast.BaseKagManagerFast), "m_BaseKagMgr");
        static readonly FieldInfo FastParser = AccessTools.Field(typeof(ScriptManagerFast.BaseKagManagerFast), "m_KagParser");
        static readonly FieldInfo FastIndex = AccessTools.Field(typeof(ScriptManagerFast.BaseKagManagerFast), "m_nNowIndex");
        static readonly FieldInfo FastLabels = AccessTools.Field(typeof(ScriptManagerFast.KagParserFast), "m_listLabel");
        static readonly BepInEx.Logging.ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("Pregnancy.Events");

        internal static void Handle(BaseKagManager manager, KagTagSupport tag)
        {
            try
            {
                if (manager == null || tag == null || !tag.IsValid("maid")
                    || !tag.IsValid("src") || !tag.IsValid("bone")) return;
                if (!EffectEventGate.IsConceptionEffect(tag.GetTagProperty("src").AsString(),
                    tag.GetTagProperty("bone").AsString())) return;

                // Use the game's virtual mapping: MotionKagManager can map maid=0 to a
                // different active maid. Never fall back to every character on screen.
                Maid maid = manager.GetMaidAndMan(tag);
                if (maid == null || maid.boMAN || maid.status == null || string.IsNullOrEmpty(maid.status.guid)) return;

                object owner;
                string file, label;
                int index;
                if (tag is ScriptManagerFast.KagTagSupportFast)
                {
                    if (CurrentFast == null || FastManager == null || FastParser == null
                        || FastIndex == null || FastLabels == null
                        || !ReferenceEquals(FastManager.GetValue(CurrentFast), manager))
                    {
                        Log.LogWarning("Conception effect skipped: fast-script execution context unavailable.");
                        return;
                    }
                    var parser = (ScriptManagerFast.KagParserFast)FastParser.GetValue(CurrentFast);
                    if (parser == null) return;
                    owner = CurrentFast;
                    file = parser.m_strFileName;
                    index = (int)FastIndex.GetValue(CurrentFast);
                    label = "";
                    int nearest = -1;
                    var labels = (List<ScriptManagerFast.KagParserFast.KLabel>)FastLabels.GetValue(parser);
                    foreach (var candidate in labels)
                    {
                        if (candidate.m_nLine <= index && candidate.m_nLine >= nearest)
                        {
                            nearest = candidate.m_nLine;
                            label = candidate.m_strLabel;
                        }
                    }
                }
                else
                {
                    if (manager.kag == null) return;
                    owner = manager.kag;
                    file = manager.kag.GetCurrentFileName();
                    label = manager.kag.GetCurrentLabel();
                    index = manager.kag.GetCurrentLine();
                }

                if (!Gate.TryAccept(owner, maid.status.guid, file, label, index)) return;
                string source = file + " " + label + " @" + index;
                Log.LogInfo("Conception effect: maid=" + maid.status.guid + " source=" + source);
                PregnancyManager.RegisterConceptionEvent(maid);
            }
            catch (Exception e)
            {
                // Detection must never interrupt the game's script execution.
                Log.LogWarning("Conception effect hook failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(BaseKagManager), "TagAddPrefabChara")]
    internal static class Patch_ConceptionEffect
    {
        static void Postfix(BaseKagManager __instance, KagTagSupport __0)
            => ConceptionEffects.Handle(__instance, __0);
    }

    // Reset at actual script entries, including re-entering a one-pulse label.
    [HarmonyPatch(typeof(KagScript), "CallOnLabelEvent")]
    internal static class Patch_ConceptionLabel
    {
        static void Prefix(KagScript __instance) => ConceptionEffects.Gate.Reset(__instance);
    }

    [HarmonyPatch(typeof(KagScript), "LoadScenario")]
    internal static class Patch_ConceptionScenario
    {
        static void Prefix(KagScript __instance) => ConceptionEffects.Gate.Reset(__instance);
    }

    [HarmonyPatch(typeof(ScriptManagerFast.BaseKagManagerFast), "Reset")]
    internal static class Patch_ConceptionFastReset
    {
        static void Prefix(ScriptManagerFast.BaseKagManagerFast __instance)
            => ConceptionEffects.Gate.Reset(__instance);
    }

    [HarmonyPatch(typeof(ScriptManagerFast.BaseKagManagerFast), "LoadScript")]
    internal static class Patch_ConceptionFastLoad
    {
        static void Postfix(ScriptManagerFast.BaseKagManagerFast __instance, bool __result)
        {
            if (__result) ConceptionEffects.Gate.Reset(__instance);
        }
    }

    [HarmonyPatch(typeof(ScriptManagerFast.BaseKagManagerFast), "Jump")]
    internal static class Patch_ConceptionFastJump
    {
        static void Postfix(ScriptManagerFast.BaseKagManagerFast __instance, bool __result)
        {
            if (__result) ConceptionEffects.Gate.Reset(__instance);
        }
    }

    [HarmonyPatch(typeof(ScriptManagerFast.BaseKagManagerFast), "Update")]
    internal static class Patch_ConceptionFastContext
    {
        static void Prefix(ScriptManagerFast.BaseKagManagerFast __instance,
            out ScriptManagerFast.BaseKagManagerFast __state)
        {
            __state = ConceptionEffects.CurrentFast;
            ConceptionEffects.CurrentFast = __instance;
        }

        static void Finalizer(ScriptManagerFast.BaseKagManagerFast __state)
        {
            ConceptionEffects.CurrentFast = __state;
        }
    }
}
