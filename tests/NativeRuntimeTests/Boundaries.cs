using System.Reflection;
using COM3D2.Pregnancy.Plugin;

namespace UnityEngine { }
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch() { }
        public HarmonyPatch(Type type, string name) { }
    }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPriority(int value) : Attribute { }
    public static class Priority { public const int Last = 0; }
    public static class AccessTools
    {
        public static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        public static List<MethodInfo> GetDeclaredMethods(Type type) => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).ToList();
    }
}

// These are lifecycle boundaries, not an implementation of the appearance state machine.
// The tests run the production patch entry points around native event sequences.
public sealed class Maid
{
    public bool boMAN;
    public TBody body0;
    private int AllProcProp2Fase;
    public Maid() { body0 = new TBody { maid = this }; }
    public void SetPhase(int phase) { AllProcProp2Fase = phase; }
}
public sealed class TBody
{
    public Maid maid;
    public bool isLoadedBody = true;
}
public sealed class TBodySkin
{
    public TBody body;
    public int SlotId;
    public TMorph morph;
    public TBodySkin(Maid maid, int slot)
    { body = maid.body0; SlotId = slot; morph = new TMorph { bodyskin = this }; }
}
public sealed class TMorph { public TBodySkin bodyskin; public bool Relevant = true; }
public sealed class CharacterMgr
{
    public readonly List<Maid> Maids = new();
    public int GetMaidCount() => Maids.Count;
    public Maid GetMaid(int i) => Maids[i];
}
public sealed class GameMain
{
    public static GameMain Instance = new();
    public CharacterMgr CharacterMgr = new();
}

namespace COM3D2.Pregnancy.Plugin
{
    internal readonly record struct AppearanceValues(float Belly, float Body);
    internal static class PregnancyAppearance
    {
        internal static readonly Dictionary<Maid, AppearanceValues> Saved = new(), Previews = new();
        internal static readonly List<(Maid Maid, AppearanceValues Value)> Reads = new();
        internal static int Clears;
        internal static int SaveCommits;
        internal static AppearanceValues Get(Maid maid)
        {
            var result = Previews.TryGetValue(maid, out var preview) ? preview : Saved.GetValueOrDefault(maid);
            Reads.Add((maid, result));
            return result;
        }
        internal static void ClearPreviews() { Previews.Clear(); Clears++; }
        internal static void EnsureCommittedForSave() { SaveCommits++; }
    }
    internal static class BodyChanges
    {
        internal static readonly Dictionary<Maid, float> Applied = new();
        internal static readonly List<(Maid Maid, float Factor)> Prepares = new();
        internal static readonly List<(Maid Maid, bool Rebuilt)> Completes = new();
        internal static Action<Maid> DuringComplete;
        internal static bool PrepareNative(Maid maid, float factor)
        {
            Prepares.Add((maid, factor));
            bool changed = !Applied.TryGetValue(maid, out var prior) ? factor != 0 : prior != factor;
            Applied[maid] = factor;
            return changed;
        }
        internal static void CompleteNative(Maid maid, bool rebuilt)
        { Completes.Add((maid, rebuilt)); DuringComplete?.Invoke(maid); }
        internal static void Forget(Maid maid) { Applied.Remove(maid); }
    }
    public static class BellyMorphController
    {
        internal static readonly List<(Maid Maid, float Belly)> Bakes = new();
        internal static readonly List<Maid> Forgotten = new();
        internal static Action<Maid> DuringBake;
        internal static bool IsNativeMorphRelevant(TMorph morph) => morph != null && morph.Relevant && morph.bodyskin != null;
        internal static void ApplyNativeAppearance(Maid maid, float progress)
        { Bakes.Add((maid, progress)); DuringBake?.Invoke(maid); }
        internal static void ForgetNativeAppearance(Maid maid) { Forgotten.Add(maid); }
    }
    public sealed class PregnancyPlugin
    {
        public static PregnancyPlugin Instance = new();
        public readonly Log LoggerForNative = new();
        public sealed class Log { public void LogError(object message) { } }
    }
}

static class Hooks
{
    internal static object Call(string patch, string method, params object[] args)
    {
        var type = typeof(NativeAppearanceRuntime).Assembly.GetType("COM3D2.Pregnancy.Plugin." + patch, true);
        var target = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (target == null) throw new MissingMethodException(patch, method);
        return target.Invoke(null, args);
    }
    internal static void StartSequence(Maid maid) => Call("Patch_NativeAppearance_SequenceStart", "Prefix", maid);
    internal static void Step(Maid maid, int phase, Action work = null)
    {
        maid.SetPhase(phase);
        var arguments = new object[] { maid, 0 };
        Call("Patch_NativeAppearance_SequenceStep", "Prefix", arguments);
        work?.Invoke();
        maid.SetPhase(phase + 1);
        Call("Patch_NativeAppearance_SequenceStep", "Postfix", maid, arguments[1]);
    }
    internal static void BeginAll(Maid maid) => Call("Patch_NativeAppearance_AllProps", "Prefix", maid);
    internal static void EndAll(Maid maid) => Call("Patch_NativeAppearance_AllProps", "Postfix", maid);
    internal static TBodySkin LoadPart(Maid maid, int slot)
    {
        var skin = new TBodySkin(maid, slot);
        Call("Patch_NativeAppearance_PartLoad", "Prefix", skin);
        // Native TBodySkin.Load applies restored blend weights before returning.
        NativeAppearanceRuntime.MorphChanged(skin.morph);
        Call("Patch_NativeAppearance_PartLoad", "Postfix", skin);
        return skin;
    }
    internal static void Frame(Maid maid) => Call("Patch_NativeAppearance_AfterBody", "Postfix", maid.body0);
    internal static void Unload(Maid maid) => Call("Patch_NativeAppearance_Unload", "Prefix", maid.body0);
    internal static void BeginLoad() => Call("Patch_NativeAppearance_LoadSave", "Prefix");
    internal static void EndLoad() => Call("Patch_NativeAppearance_LoadSave", "Postfix");
    internal static Exception FailLoad(Exception error) => (Exception)Call("Patch_NativeAppearance_LoadSave", "Finalizer", error);
}
