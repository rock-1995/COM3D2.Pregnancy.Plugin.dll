using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Yotogis;

namespace COM3D2.Pregnancy.Plugin
{
    [HarmonyPatch(typeof(GameMain), "OnEndDay")]
    class Patch_OnEndDay
    {
        static void Postfix() => PregnancyManager.AdvanceDay();
    }

    [HarmonyPatch(typeof(YotogiCommandFactory), "SetCommandCallback")]
    class Patch_SetCommandCallback
    {
        static readonly Dictionary<YotogiCommandFactory.CommandCallback, YotogiCommandFactory.CommandCallback> _wrapped
            = new Dictionary<YotogiCommandFactory.CommandCallback, YotogiCommandFactory.CommandCallback>();
        static readonly HashSet<YotogiCommandFactory.CommandCallback> _wrappers
            = new HashSet<YotogiCommandFactory.CommandCallback>();
        static bool _bVaginalInsert = false;

        static void Prefix(ref YotogiCommandFactory.CommandCallback __0)
        {
            if (__0 == null) return;
            if (_wrappers.Contains(__0)) return;

            YotogiCommandFactory.CommandCallback wrapped;
            if (!_wrapped.TryGetValue(__0, out wrapped))
            {
                YotogiCommandFactory.CommandCallback original = __0;
                wrapped = delegate(Skill.Data.Command.Data commandData)
                {
                    original(commandData);
                    HandleCommand(commandData);
                };
                _wrapped[__0] = wrapped;
                _wrappers.Add(wrapped);
            }

            __0 = wrapped;
        }

        static void HandleCommand(Skill.Data.Command.Data commandData)
        {
            try
            {
                if (commandData == null || commandData.basic == null) return;

                string cmdType = commandData.basic.command_type.ToString();
                string name = commandData.basic.name ?? "";
                string group = commandData.basic.group_name ?? "";

                bool isAnal = group.Contains("\u30A2\u30CA\u30EB");

                if (cmdType == "\u633F\u5165" && !isAnal)
                    _bVaginalInsert = true;
                else if (cmdType == "\u6B62\u3081\u308B" || cmdType == "\u5358\u767A")
                    _bVaginalInsert = false;

                if (cmdType == "\u7D76\u9802"
                    && (name.Contains("\u4E2D\u51FA\u3057") || name.Contains("\u6CE8\u304E\u8FBC\u3080"))
                    && _bVaginalInsert)
                {
                    Log("Vaginal creampie detected: " + group + " / " + name);
                    CheckConception();
                }
            }
            catch (System.Exception e) { Log("Command callback hook error: " + e.Message); }
        }

        static void CheckConception()
        {
            var cm = GameMain.Instance?.CharacterMgr;
            if (object.ReferenceEquals(cm, null)) return;

            FertilityCycleMode mode = PregnancyManager.GetCycleMode();
            bool cyclic = PregnancyManager.IsCyclicMode(mode);
            int cnt = cm.GetMaidCount();
            for (int i = 0; i < cnt; i++)
            {
                Maid m = cm.GetMaid(i);
                if (object.ReferenceEquals(m, null)) continue;
                if (PregnancyManager.GetPregnant(m)) continue;

                if (cyclic)
                {
                    PregnancyManager.EnsureCycleProgress(m);
                    PregnancyManager.SetFertilityCoefficient(m, 1f);
                    Log("Creampie stored fertility coefficient: "
                        + m.status.lastName + " " + m.status.firstName);
                    continue;
                }

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
