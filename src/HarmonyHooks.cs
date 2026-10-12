using HarmonyLib;

namespace COM3D2.Pregnancy.Plugin
{
    [HarmonyPatch(typeof(GameMain), "OnEndDay")]
    class Patch_OnEndDay
    {
        static void Postfix() => PregnancyManager.AdvanceDay();
    }

    [HarmonyPatch(typeof(TMorph), "FixBlendValues")]
    class Patch_TMorphFixBlendValues
    {
        static void Postfix(TMorph __instance)
        {
            try { BellyMorphController.NotifyFixBlendValues(__instance); }
            catch (System.Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("Pregnancy")
                    .LogInfo("[Pregnancy] FixBlendValues hook: " + e.Message);
            }
        }
    }

}
