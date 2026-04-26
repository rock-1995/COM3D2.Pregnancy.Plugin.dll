using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    [HarmonyPatch(typeof(GameMain), "OnEndDay")]
    class Patch_OnEndDay
    {
        static void Postfix() => PregnancyManager.AdvanceDay();
    }

    [HarmonyPatch(typeof(YotogiPlayManager), "OnClickCommand")]
    class Patch_OnClickCommand
    {
        static bool _bVaginalInsert = false;

        static void Postfix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length == 0) return;
                var data = __args[0];
                if (object.ReferenceEquals(data, null)) return;

                var basicField = data.GetType().GetField("basic",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (object.ReferenceEquals(basicField, null)) return;
                var basic = basicField.GetValue(data);
                if (object.ReferenceEquals(basic, null)) return;

                var cmdTypeField = basic.GetType().GetField("command_type",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var nameField    = basic.GetType().GetField("name",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var groupField   = basic.GetType().GetField("group_name",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (object.ReferenceEquals(cmdTypeField, null)) return;

                string cmdType = cmdTypeField.GetValue(basic).ToString();
                string name    = object.ReferenceEquals(nameField,  null) ? "" :
                                 nameField.GetValue(basic)  as string ?? "";
                string group   = object.ReferenceEquals(groupField, null) ? "" :
                                 groupField.GetValue(basic) as string ?? "";

                bool isAnal = group.Contains("アナル");

                if (cmdType == "挿入" && !isAnal)
                    _bVaginalInsert = true;
                else if (cmdType == "止める" || cmdType == "単発")
                    _bVaginalInsert = false;

                if (cmdType == "絶頂"
                    && (name.Contains("中出し") || name.Contains("注ぎ込む"))
                    && _bVaginalInsert)
                {
                    Log("Vaginal creampie detected: " + group + " / " + name);
                    CheckConception();
                }
            }
            catch (System.Exception e) { Log("OnClickCommand hook error: " + e.Message); }
        }

        static void CheckConception()
        {
            var cm = GameMain.Instance?.CharacterMgr;
            if (object.ReferenceEquals(cm, null)) return;

            int cnt = cm.GetMaidCount();
            for (int i = 0; i < cnt; i++)
            {
                Maid m = cm.GetMaid(i);
                if (object.ReferenceEquals(m, null)) continue;
                if (PregnancyManager.GetPregnant(m)) continue;

                float rate = PregnancyPlugin.CfgFertilityRate.Value;
                if (Random.value < rate)
                {
                    PregnancyManager.SetPregnant(m, true);
                    Log("Conception! " + m.status.lastName + " " + m.status.firstName);
                }
                else
                {
                    Log("Creampie, no conception (rate=" + rate + ")");
                }
            }
        }

        static void Log(string msg)
            => BepInEx.Logging.Logger.CreateLogSource("Pregnancy")
                .LogInfo("[Pregnancy] " + msg);
    }

    // ── 衣装ロード時に孕み腹を自動適用 ───────────────────────────────
    // sharedMesh の差し替えは行わず in-place で頂点を書き換えるため、
    // COM3D2 側の mesh オブジェクト参照は一切変化しない。
    // → body 可視性管理・脱衣時の挙動はすべて COM3D2 が正常に処理する。
    [HarmonyPatch(typeof(ImportCM), "LoadSkinMesh_R")]
    class Patch_LoadSkinMesh_R
    {
        static void Postfix(GameObject __result, TBodySkin __3)
        {
            if (__result == null || __3 == null) return;
            try
            {
                Maid maid = FindMaidForBodySkin(__3);
                if (maid == null) return;
                if (!BellyMorphController.IsActive(maid)) return;

                SkinnedMeshRenderer smr =
                    __result.GetComponentInChildren<SkinnedMeshRenderer>(false);
                if (smr == null) return;

                BellyMorphController.ApplyFromPatch(maid, smr, __result.name);
            }
            catch (System.Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("Pregnancy")
                    .LogWarning("[Pregnancy] LoadSkinMesh_R patch error: " + e.Message);
            }
        }

        public static Maid FindMaidForBodySkin(TBodySkin bodySkin)
        {
            var cm = GameMain.Instance?.CharacterMgr;
            if (cm == null) return null;
            int cnt = cm.GetStockMaidCount();
            for (int i = 0; i < cnt; i++)
            {
                Maid m = cm.GetStockMaid(i);
                if (m?.body0?.goSlot == null) continue;
                for (int j = 0; j < m.body0.goSlot.Count; j++)
                    if (m.body0.goSlot[j] == bodySkin) return m;
            }
            return null;
        }
    }
}
