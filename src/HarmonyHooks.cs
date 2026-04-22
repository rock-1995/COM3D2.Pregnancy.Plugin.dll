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
        // 膣挿入中フラグ（アナル除外）
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

                // 挿入状態を追跡（膣のみ。アナルは膣フラグに影響しない）
                if (cmdType == "挿入" && !isAnal)
                    _bVaginalInsert = true;
                else if (cmdType == "止める" || cmdType == "単発")
                    _bVaginalInsert = false;

                // 膣内中出し判定
                // 「中出し」コマンド実行時に膣挿入フラグが立っていれば判定
                // アナルのみの場合はフラグが立たないので自然に除外
                // MMF（膣+アナル同時）の場合は膣フラグが立つので対象に含まれる
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
}
