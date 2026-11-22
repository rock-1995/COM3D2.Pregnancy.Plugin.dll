using HarmonyLib;

namespace COM3D2.Pregnancy.Plugin
{
    [HarmonyPatch(typeof(GameMain), "OnEndDay")]
    class Patch_OnEndDay
    {
        static void Postfix()
        {
            PregnancyManager.AdvanceDay();
            NativeAppearanceRuntime.RefreshLoadedAppearances();
        }
    }

    [HarmonyPatch(typeof(TMorph), "FixBlendValues")]
    class Patch_TMorphFixBlendValues
    {
        static void Prefix(TMorph __instance, out bool __state)
        {
            __state = BellyMorphController.NativeMorphWillChange(__instance);
        }
        static void Postfix(TMorph __instance, bool __state)
        {
            if (__state) NativeAppearanceRuntime.MorphChanged(__instance);
        }
    }

}
