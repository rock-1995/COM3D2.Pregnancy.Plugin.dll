using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    // All expensive work belongs to a native customization transaction. Visibility
    // changes do not start a transaction; native ResetBlendValues reuses our output.
    internal static class NativeAppearanceRuntime
    {
        sealed class Build
        {
            internal int Depth;
            internal bool Sequential, Prepared, Dirty, Completing, BodyReplaced;
        }

        static readonly Dictionary<Maid, Build> Builds = new Dictionary<Maid, Build>();
        static readonly FieldInfo Phase = AccessTools.Field(typeof(Maid), "AllProcProp2Fase");
        static int saveLoadDepth;
        internal static bool LoadingSave => saveLoadDepth > 0;

        static bool Valid(Maid maid) => maid != null && !maid.boMAN && maid.body0 != null;
        static Build For(Maid maid)
        {
            Build build;
            if (!Builds.TryGetValue(maid, out build)) Builds.Add(maid, build = new Build());
            return build;
        }

        static void Prepare(Maid maid, Build build)
        {
            if (build.Prepared || LoadingSave) return;
            build.Prepared = true;
            var appearance = PregnancyAppearance.Get(maid);
            if (BodyChanges.PrepareNative(maid, appearance.Body)) build.Dirty = true;
        }

        internal static void Begin(Maid maid, bool sequential)
        {
            if (!Valid(maid)) return;
            var build = For(maid);
            if (sequential) build.Sequential = true;
            else build.Depth++;
            Prepare(maid, build);
        }

        internal static void End(Maid maid)
        {
            if (!Valid(maid)) return;
            var build = For(maid);
            if (build.Depth > 0) build.Depth--;
            if (build.Depth == 0 && !build.Sequential) Complete(maid, build);
        }

        internal static int ReadPhase(Maid maid) => Phase == null ? -1 : (int)Phase.GetValue(maid);

        internal static void AfterSequenceStep(Maid maid, int phase)
        {
            if (!Valid(maid)) return;
            var build = For(maid);
            // Phase 2 commits native sliders; phase 3 finishes logical visibility.
            // Submit with that final slot mask while still inside the build.
            if (phase == 3) Complete(maid, build);
            if (phase == 5) { build.Sequential = false; build.Prepared = false; }
        }

        internal static void PreparePart(Maid maid)
        {
            if (!Valid(maid)) return;
            Prepare(maid, For(maid));
        }

        internal static void PartLoaded(TBodySkin skin)
        {
            var maid = skin == null || skin.body == null ? null : skin.body.maid;
            if (!Valid(maid) || !BellyMorphController.IsNativeMorphRelevant(skin.morph)) return;
            var build = For(maid);
            Prepare(maid, build);
            build.Dirty = true;
            if ((int)skin.SlotId == 0) build.BodyReplaced = true;
        }

        internal static void MorphChanged(TMorph morph)
        {
            if (!BellyMorphController.IsNativeMorphRelevant(morph)) return;
            var maid = morph.bodyskin.body.maid;
            if (!Valid(maid)) return;
            var build = For(maid);
            Prepare(maid, build);
            build.Dirty = true;
        }

        internal static void CompletePending(Maid maid)
        {
            Build build;
            if (!Valid(maid) || !Builds.TryGetValue(maid, out build) || build.Depth != 0 || build.Sequential) return;
            if (!build.Dirty && !build.Prepared) return;
            Complete(maid, build);
        }

        static void Complete(Maid maid, Build build)
        {
            if (LoadingSave || build.Completing || !maid.body0.isLoadedBody) return;
            build.Completing = true;
            try
            {
                Prepare(maid, build);
                BodyChanges.CompleteNative(maid, build.BodyReplaced);
                if (build.Dirty)
                {
                    var appearance = PregnancyAppearance.Get(maid);
                    BellyMorphController.ApplyNativeAppearance(maid, appearance.Belly);
                    build.Dirty = false;
                    build.BodyReplaced = false;
                }
            }
            catch (Exception e)
            {
                PregnancyPlugin.Instance.LoggerForNative.LogError("[NativeAppearance] " + e);
                // Keep the request for the next real native change, not a retry on
                // every frame after a permanently invalid model.
                build.Dirty = false;
            }
            finally { build.Completing = false; build.Prepared = false; }
        }

        internal static void RequestApply(Maid maid)
        {
            if (!Valid(maid)) return;
            var build = For(maid);
            if (!build.Completing) build.Prepared = false;
            build.Dirty = true;
            Prepare(maid, build);
            // Maker/F8 changes coalesce until this actor's native completion
            // boundary, just like native slider changes. No whole-scene scan.
        }

        internal static void Forget(Maid maid)
        {
            if (ReferenceEquals(maid, null)) return;
            Builds.Remove(maid);
            BellyMorphController.ForgetNativeAppearance(maid);
            BodyChanges.Forget(maid);
        }

        internal static void AbortBuild(Maid maid)
        {
            // Native code already failed. Retire only our transaction bookkeeping;
            // touching model resources here could hide the original exception.
            if (!ReferenceEquals(maid, null)) Builds.Remove(maid);
        }

        internal static void BodyUnloading(Maid maid)
        {
            if (ReferenceEquals(maid, null)) return;
            Build build;
            if (Builds.TryGetValue(maid, out build) && (build.Depth > 0 || build.Sequential))
            {
                // LoadBody_R unloads the previous body inside the surrounding
                // property transaction. Preserve that transaction's boundary.
                BellyMorphController.ForgetNativeAppearance(maid);
                BodyChanges.Forget(maid);
                build.Prepared = false;
                build.BodyReplaced = true;
                build.Dirty = true;
                return;
            }
            Forget(maid);
        }

        internal static void BeginSaveLoad()
        {
            saveLoadDepth++;
            if (saveLoadDepth != 1) return;
            PregnancyAppearance.ClearPreviews();
            foreach (var maid in new List<Maid>(Builds.Keys)) Forget(maid);
        }

        internal static void EndSaveLoad()
        {
            if (saveLoadDepth > 0) saveLoadDepth--;
            if (LoadingSave) return;
            PregnancyAppearance.ClearPreviews();
            // ExSaveData's patched Deserialize callback has already installed the
            // selected slot here. Any models created during deserialization now
            // consume that slot's appearance instead of the previous slot's values.
            RefreshLoadedAppearances();
        }

        internal static void RefreshLoadedAppearances()
        {
            var manager = GameMain.Instance == null ? null : GameMain.Instance.CharacterMgr;
            if (manager == null) return;
            for (int i = 0; i < manager.GetMaidCount(); i++)
            {
                var maid = manager.GetMaid(i);
                if (Valid(maid)) RequestApply(maid);
            }
        }

        internal static void AbortSaveLoad()
        {
            if (saveLoadDepth > 0) saveLoadDepth--;
            if (LoadingSave) return;
            PregnancyAppearance.ClearPreviews();
            foreach (var maid in new List<Maid>(Builds.Keys)) Forget(maid);
        }
    }

    [HarmonyPatch(typeof(Maid), "AllProcProp")]
    static class Patch_NativeAppearance_AllProps
    {
        static void Prefix(Maid __instance) => NativeAppearanceRuntime.Begin(__instance, false);
        static void Postfix(Maid __instance) => NativeAppearanceRuntime.End(__instance);
        static Exception Finalizer(Maid __instance, Exception __exception)
        {
            if (__exception != null) NativeAppearanceRuntime.AbortBuild(__instance);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Maid), "AllProcPropSeqStart")]
    static class Patch_NativeAppearance_SequenceStart
    {
        static void Prefix(Maid __instance) => NativeAppearanceRuntime.Begin(__instance, true);
        static Exception Finalizer(Maid __instance, Exception __exception)
        {
            if (__exception != null) NativeAppearanceRuntime.AbortBuild(__instance);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Maid), "AllProcPropSeq")]
    static class Patch_NativeAppearance_SequenceStep
    {
        static void Prefix(Maid __instance, out int __state) => __state = NativeAppearanceRuntime.ReadPhase(__instance);
        static void Postfix(Maid __instance, int __state) => NativeAppearanceRuntime.AfterSequenceStep(__instance, __state);
        static Exception Finalizer(Maid __instance, Exception __exception)
        {
            if (__exception != null) NativeAppearanceRuntime.AbortBuild(__instance);
            return __exception;
        }
    }

    [HarmonyPatch]
    static class Patch_NativeAppearance_PartLoad
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(TBodySkin)))
                if (method.Name == "Load" && method.GetParameters().Length == 11) yield return method;
        }
        static void Prefix(TBodySkin __instance) => NativeAppearanceRuntime.PreparePart(__instance.body == null ? null : __instance.body.maid);
        static void Postfix(TBodySkin __instance) => NativeAppearanceRuntime.PartLoaded(__instance);
    }

    [HarmonyPatch(typeof(TBody), "LateUpdate")]
    static class Patch_NativeAppearance_AfterBody
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(TBody __instance) => NativeAppearanceRuntime.CompletePending(__instance.maid);
    }

    [HarmonyPatch(typeof(TBody), "UnInit")]
    static class Patch_NativeAppearance_Unload
    {
        static void Prefix(TBody __instance) => NativeAppearanceRuntime.BodyUnloading(__instance.maid);
    }

    [HarmonyPatch(typeof(GameMain), "Deserialize")]
    static class Patch_NativeAppearance_LoadSave
    {
        static void Prefix() => NativeAppearanceRuntime.BeginSaveLoad();
        [HarmonyPriority(Priority.Last)]
        static void Postfix() => NativeAppearanceRuntime.EndSaveLoad();
        static Exception Finalizer(Exception __exception)
        {
            if (__exception != null) NativeAppearanceRuntime.AbortSaveLoad();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(GameMain), "Serialize")]
    static class Patch_NativeAppearance_Save
    {
        static void Prefix() => PregnancyAppearance.EnsureCommittedForSave();
    }
}
