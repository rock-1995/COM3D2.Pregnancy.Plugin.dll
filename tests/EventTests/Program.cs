using COM3D2.Pregnancy.Plugin;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rng = UnityEngine.Random;

class Program
{
    static int checks;
    static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception("FAILED: " + message);
    }
    static void Invoke(Type type, string method, params object[] args)
        => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    static KagTagSupport Effect(bool fast = false, string bone = "_IK_vagina", string resource = "Particle/pSeieki_naka", int maid = 0)
    {
        KagTagSupport tag = fast ? new ScriptManagerFast.KagTagSupportFast() : new KagTagSupport();
        tag.Values = new() { ["maid"] = maid.ToString(), ["bone"] = bone, ["src"] = resource };
        return tag;
    }
    static BaseKagManager Manager(string guid)
    {
        var manager = new BaseKagManager(); manager.Maids[0] = new Maid(guid); return manager;
    }
    static void Emit(BaseKagManager manager, KagTagSupport tag, int line)
    {
        manager.kag.Line = line;
        // Exercise the actual production postfix, not a second detector implementation.
        Invoke(typeof(Patch_ConceptionEffect), "Postfix", manager, tag);
    }
    static void Main(string[] args)
    {
        PregnancyPlugin.CfgFertilityRate.Value = 0f;
        var story = Manager("story-target");
        story.Maids[1] = new Maid("bystander");
        var tag = Effect();
        int before = Rng.Calls;
        foreach (int line in new[] { 10, 13, 16 }) Emit(story, tag, line);
        Check(Rng.Calls == before + 1, "three particle pulses produce one roll");
        Emit(story, tag, 16);
        Check(Rng.Calls == before + 1, "same callback position is not a second roll");
        Invoke(typeof(Patch_ConceptionLabel), "Prefix", story.kag);
        Emit(story, tag, 10);
        Check(Rng.Calls == before + 2, "re-entered label produces a fresh roll");
        for (int i = 1; i < 9; i++) Emit(story, tag, 10 + i * 3);
        Check(Rng.Calls == before + 2, "nine pulses remain one event without a time limit");
        Invoke(typeof(Patch_ConceptionScenario), "Prefix", story.kag);
        Emit(story, tag, 10);
        Check(Rng.Calls == before + 3, "script reload allows a new event");

        before = Rng.Calls;
        foreach (string bone in new[] { "_IK_anal", "Bip01 Head", "chinko1", "_IK_vagina_extra", "" })
            Emit(story, Effect(bone: bone), 1);
        foreach (string resource in new[] { "Particle/pSeieki", "Particle/pSeieki2", "Seieki/spe001", "Particle/pSeieki_naka_extra", "" })
            Emit(story, Effect(resource: resource), 1);
        var manTag = Effect(); manTag.Values.Remove("maid"); manTag.Values["man"] = "0";
        Emit(story, manTag, 1);
        Emit(story, new KagTagSupport(), 1);
        Emit(story, null, 1);
        Emit(story, Effect(maid: 9), 1);
        story.Maids[2] = new Maid("male") { boMAN = true };
        Emit(story, Effect(maid: 2), 1);
        Check(Rng.Calls == before, "anal/oral/external/texture/malformed/missing/male are excluded");

        PregnancyPlugin.CfgFertilityRate.Value = 1f;
        Emit(story, Effect(maid: 1), 1);
        Check(PregnancyManager.GetPregnant(story.Maids[1]), "explicit maid index receives the event");
        Check(!PregnancyManager.GetPregnant(story.Maids[0]), "other on-screen maid is unchanged");
        before = Rng.Calls;
        Invoke(typeof(Patch_ConceptionLabel), "Prefix", story.kag);
        Emit(story, Effect(maid: 1), 1);
        Check(Rng.Calls == before, "already pregnant maid consumes no new roll");
        PregnancyPlugin.CfgFertilityRate.Value = .3f;
        Rng.Next = .3f;
        var boundary = Manager("rate-boundary");
        Emit(boundary, tag, 1);
        Check(!PregnancyManager.GetPregnant(boundary.Maids[0]), "probability boundary retains strict less-than comparison");
        Rng.Next = .2f;
        Invoke(typeof(Patch_ConceptionLabel), "Prefix", boundary.kag);
        Emit(boundary, tag, 1);
        Check(PregnancyManager.GetPregnant(boundary.Maids[0]), "next event rolls again after a failed conception");
        Rng.Next = .5f;
        PregnancyPlugin.CfgFertilityRate.Value = 1f;
        var mapped = Manager("mapped-main-maid");
        Emit(mapped, tag, 1);
        Check(PregnancyManager.GetPregnant(mapped.Maids[0]), "manager's actual maid mapping is used");

        foreach (var mode in new[] { FertilityCycleMode.SevenDay, FertilityCycleMode.TwentyEightDay })
        {
            PregnancyPlugin.CfgCycleMode.Value = mode;
            var interaction = Manager("private-" + mode);
            var target = interaction.Maids[0];
            interaction.Maids[1] = new Maid("private-bystander-" + mode);
            // Stay inside the day: existing storage rounds progress to three decimals.
            PregnancyManager.SetCycleProgress(target, mode == FertilityCycleMode.SevenDay ? 2.5f / 7f : 13.5f / 28f);
            before = Rng.Calls;
            Emit(interaction, tag, 100);
            Emit(interaction, tag, 103);
            Check(PregnancyManager.GetFertilityCoefficient(target) == 1f, mode + " stores coefficient");
            Check(PregnancyManager.GetFertilityCoefficient(interaction.Maids[1]) == 0f, mode + " leaves bystander untouched");
            Check(!PregnancyManager.GetPregnant(target) && Rng.Calls == before, mode + " waits for day end");
            GameMain.Instance.CharacterMgr.Maids.Clear();
            GameMain.Instance.CharacterMgr.Maids.Add(target);
            PregnancyManager.AdvanceDay();
            Check(PregnancyManager.GetPregnant(target), mode + " uses existing day-end algorithm");
        }

        PregnancyPlugin.CfgCycleMode.Value = FertilityCycleMode.Simple;
        PregnancyPlugin.CfgFertilityRate.Value = 0;
        var fastManager = Manager("fast-target");
        var parser = new ScriptManagerFast.KagParserFast();
        parser.Label("*first", 0); parser.Label("*second", 20);
        var fast = new ScriptManagerFast.BaseKagManagerFast(fastManager, parser);
        var fastTag = Effect(fast: true);
        object[] enterArgs = { fast, null };
        Invoke(typeof(Patch_ConceptionFastContext), "Prefix", enterArgs);
        before = Rng.Calls;
        foreach (int index in new[] { 2, 5, 8 }) { fast.Index(index); Emit(fastManager, fastTag, 0); }
        Check(Rng.Calls == before + 1, "fast engine pulses deduplicate with parser context");
        fast.Index(21); Emit(fastManager, fastTag, 0);
        Check(Rng.Calls == before + 2, "natural fast label transition starts a new event");
        Invoke(typeof(Patch_ConceptionFastJump), "Postfix", fast, true);
        Emit(fastManager, fastTag, 0);
        Check(Rng.Calls == before + 3, "fast jump re-enters one-pulse block");
        Invoke(typeof(Patch_ConceptionFastJump), "Postfix", fast, false);
        Emit(fastManager, fastTag, 0);
        Check(Rng.Calls == before + 3, "failed fast jump does not reset deduplication");
        Invoke(typeof(Patch_ConceptionFastReset), "Prefix", fast);
        Emit(fastManager, fastTag, 0);
        Check(Rng.Calls == before + 4, "fast reset starts new event");
        Invoke(typeof(Patch_ConceptionFastLoad), "Postfix", fast, true);
        Emit(fastManager, fastTag, 0);
        Check(Rng.Calls == before + 5, "fast load starts new event");
        var nested = new ScriptManagerFast.BaseKagManagerFast(Manager("nested"), parser);
        object[] nestedArgs = { nested, null };
        Invoke(typeof(Patch_ConceptionFastContext), "Prefix", nestedArgs);
        Invoke(typeof(Patch_ConceptionFastContext), "Finalizer", nestedArgs[1]);
        Check(ReferenceEquals(ConceptionEffects.CurrentFast, fast), "nested fast scope restores outer context");
        Invoke(typeof(Patch_ConceptionFastContext), "Finalizer", enterArgs[1]);
        Check(ConceptionEffects.CurrentFast == null, "finalizer clears fast context including exceptional exits");
        before = Rng.Calls;
        Emit(fastManager, fastTag, 0);
        Check(Rng.Calls == before, "missing fast context cannot fall back to a wrong script");
        story.ThrowOnResolve = true;
        Emit(story, tag, 1);
        Check(BepInEx.Logging.Logger.Warnings.Count == 2, "resolver errors are contained and logged");

        foreach (string path in args) ReplaySurvey(path);
        var mother = new Maid("birth-test");
        var otherMother = new Maid("other-birth-test");
        GameMain.Instance.CharacterMgr.Maids.Clear();
        GameMain.Instance.CharacterMgr.Maids.Add(mother);
        GameMain.Instance.CharacterMgr.Maids.Add(otherMother);
        PregnancyPlugin.CfgCycleMode.Value = FertilityCycleMode.Simple;
        Check(PregnancyManager.GetBirthCount(mother) == 0, "old save defaults to zero births");
        for (int birth = 1; birth <= 7; birth++)
        {
            PregnancyManager.SetPregnant(mother, true);
            PregnancyManager.SetProgress(mother, .999f);
            PregnancyManager.AdvanceDay();
            Check(PregnancyManager.GetBirthCount(mother) == birth && !PregnancyManager.GetPregnant(mother), "birth increment " + birth);
            PregnancyManager.AdvanceDay();
            Check(PregnancyManager.GetBirthCount(mother) == birth, "no duplicate birth " + birth);
        }
        Check(PregnancyManager.GetBirthCount(otherMother) == 0, "birth history isolated by character");
        PregnancyManager.SetPregnant(otherMother, true);
        PregnancyManager.SetPregnant(otherMother, false);
        Check(PregnancyManager.GetBirthCount(otherMother) == 0, "pregnant checkbox is not a birth");
        ExSaveDataBridge.Set(mother, "birthCount", int.MaxValue.ToString());
        PregnancyManager.SetPregnant(mother, true); PregnancyManager.SetProgress(mother, .999f);
        PregnancyManager.AdvanceDay();
        Check(PregnancyManager.GetBirthCount(mother) == int.MaxValue, "birth count cannot wrap negative");
        Console.WriteLine("PASS: " + checks + " event/target/probability/cycle/context/birth checks.");
    }

    // Optional replay of a read-only local script survey; game scripts are not distributed.
    static void ReplaySurvey(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        int accepted = 0, groups = 0, rejected = 0;
        var pattern = new Regex("(?:^|\\s)(\\w+)=(?:\"([^\"]*)\"|(\\S+))");
        foreach (var file in doc.RootElement.EnumerateArray())
        {
            var gate = new EffectEventGate(); var owner = new object();
            var seen = new HashSet<string>();
            foreach (var row in file.GetProperty("rows").EnumerateArray())
            {
                if (row[0].GetString() != "effect") continue;
                var attrs = pattern.Matches(row[3].GetString()).ToDictionary(m => m.Groups[1].Value.ToLowerInvariant(), m => m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value);
                attrs.TryGetValue("src", out var resource); attrs.TryGetValue("bone", out var bone);
                if (!EffectEventGate.IsConceptionEffect(resource, bone)) { rejected++; continue; }
                string maid = attrs["maid"], label = row[2].GetString();
                bool first = seen.Add(label + ":" + maid);
                bool roll = gate.TryAccept(owner, maid, file.GetProperty("file").GetString(), label, row[1].GetInt32());
                Check(roll == first, "local corpus pulse group " + file.GetProperty("file").GetString());
                accepted++; if (roll) groups++;
            }
        }
        Console.WriteLine($"Corpus {Path.GetFileName(path)}: {accepted} matching pulses -> {groups} events; {rejected} other effects excluded.");
    }
}
