global using Random = UnityEngine.Random;
using System.Reflection;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    class HarmonyPatch(Type type, string name) : Attribute
    { public Type TargetType = type; public string TargetName = name; }
    static class AccessTools
    {
        public static FieldInfo Field(Type type, string name)
            => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    }
}
namespace BepInEx
{
    static class Paths { public static string PluginPath => "unused"; }
}
namespace BepInEx.Logging
{
    class ManualLogSource
    {
        public void LogInfo(object message) { }
        public void LogWarning(object message) => Logger.Warnings.Add(message.ToString());
    }
    static class Logger
    {
        public static readonly List<string> Warnings = new();
        public static ManualLogSource CreateLogSource(string name) => new();
    }
}
namespace UnityEngine
{
    static class Mathf
    {
        public static float Clamp01(float x) => Math.Clamp(x, 0, 1);
        public static int Clamp(int x, int min, int max) => Math.Clamp(x, min, max);
        public static int FloorToInt(float x) => (int)MathF.Floor(x);
    }
    static class Random
    {
        public static int Calls;
        public static float Next = .5f;
        public static float value { get { Calls++; return Next; } }
    }
    static class JsonUtility
    {
        public static T FromJson<T>(string text) => throw new NotSupportedException();
        public static string ToJson(object value, bool pretty) => throw new NotSupportedException();
    }
}
public class Maid
{
    public bool boMAN;
    public Status status = new();
    public Maid(string guid) { status.guid = guid; }
    public class Status { public string guid; }
}
class CharacterMgr
{
    public readonly List<Maid> Maids = new();
    public int GetStockMaidCount() => Maids.Count;
    public Maid GetStockMaid(int index) => Maids[index];
}
class GameMain
{
    public static GameMain Instance = new();
    public CharacterMgr CharacterMgr = new();
}
class KagScript
{
    public string File = "sample.ks", Label = "*finish";
    public int Line = 10;
    public string GetCurrentFileName() => File;
    public string GetCurrentLabel() => Label;
    public int GetCurrentLine() => Line;
}
class TagValue(string text)
{
    public string AsString() => text;
    public int AsInteger() => int.Parse(text);
}
class KagTagSupport
{
    public Dictionary<string, string> Values = new();
    public bool IsValid(string key) => Values.ContainsKey(key);
    public TagValue GetTagProperty(string key) => new(Values[key]);
}
class BaseKagManager
{
    public KagScript kag = new();
    public Dictionary<int, Maid> Maids = new();
    public bool ThrowOnResolve;
    public virtual Maid GetMaidAndMan(KagTagSupport tag)
    {
        if (ThrowOnResolve) throw new InvalidOperationException("test resolver failure");
        Maids.TryGetValue(tag.GetTagProperty("maid").AsInteger(), out var maid);
        return maid;
    }
}
class ScriptManagerFast
{
    public class KagTagSupportFast : KagTagSupport { }
    public class KagParserFast
    {
        public class KLabel { public string m_strLabel; public int m_nLine; }
        public string m_strFileName = "fast.ks";
        private List<KLabel> m_listLabel = new();
        public void Label(string name, int index) => m_listLabel.Add(new KLabel { m_strLabel = name, m_nLine = index });
    }
    public class BaseKagManagerFast
    {
        protected BaseKagManager m_BaseKagMgr;
        protected KagParserFast m_KagParser;
        protected int m_nNowIndex;
        public BaseKagManagerFast(BaseKagManager manager, KagParserFast parser)
        { m_BaseKagMgr = manager; m_KagParser = parser; }
        public void Index(int index) => m_nNowIndex = index;
    }
}
namespace COM3D2.Pregnancy.Plugin.Growth
{
    public class VtxSettings
    {
        public void CompleteGrowthDefaults(string json) { }
        public VtxSettings Copy() => new();
    }
}
namespace COM3D2.Pregnancy.Plugin
{
    public class PregSettings { public Growth.VtxSettings growth = new(); public int growthModelVersion; }
    static class BellyMorphController { public static Growth.VtxSettings Shape = new(); }
    class Config<T>(T value) { public T Value = value; }
    static class PregnancyPlugin
    {
        public static Config<float> CfgFertilityRate = new(.3f);
        public static Config<int> CfgPregnancyWeeks = new(40);
        public static Config<FertilityCycleMode> CfgCycleMode = new(FertilityCycleMode.Simple);
    }
    static class ExSaveDataBridge
    {
        private static readonly Dictionary<(Maid,string),string> Values = new();
        public static string Get(Maid maid, string key, string def = "")
            => Values.TryGetValue((maid,key), out var value) ? value : def;
        public static void Set(Maid maid, string key, string value) => Values[(maid,key)] = value;
    }
}
