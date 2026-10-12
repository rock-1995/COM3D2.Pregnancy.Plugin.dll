using COM3D2.Pregnancy.Plugin;
using UnityEngine;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch(Type type, string method) : Attribute { }
}
namespace BepInEx.Logging
{
    public class ManualLogSource { public void LogInfo(string s) { } public void LogWarning(string s) { } }
    public static class Logger { public static ManualLogSource CreateLogSource(string s) => new(); }
}
namespace UnityEngine
{
    public class Object { public static void Destroy(Object obj) { ((Texture2D)obj).Destroyed = true; } }
    public enum TextureFormat { RGBA32 }
    public enum TextureWrapMode { Clamp }
    public class Texture2D(int w, int h, TextureFormat f, bool mip) : Object
    {
        public TextureWrapMode wrapMode;
        public bool Destroyed;
        public MaidParts.PartsColor Color;
    }
}
namespace COM3D2.Pregnancy.Plugin
{
    public class Config<T>(T value) { public T Value = value; }
    public class PregnancyPlugin
    {
        public static Config<int> CfgBodyChangeBirths = new(5), CfgBodyChangeLower = new(0), CfgBodyChangeUpper = new(100);
    }
    public static class PregnancyManager { public static int GetBirthCount(Maid maid) => maid.Births; }
}
public enum MPN { Hara, west, chikubi }
public class MaidProp
{
    public int idx, type, value, temp_value, min = 0, max = 100;
    public string name;
    public bool boTempDut, boDut, boTempExecuted;
}
public class Maid
{
    public int Births;
    public TBody body0;
    public MaidParts Parts = new();
    public Dictionary<MPN, MaidProp> Props = new();
    public Maid(int hara, int waist)
    {
        body0 = new TBody { maid = this };
        Props[MPN.Hara] = new() { idx = 0, name = "Hara", type = 1, value = hara };
        Props[MPN.west] = new() { idx = 1, name = "west", type = 2, value = waist };
        Parts.Color = new() { m_nMainBrightness = 303, m_nMainChroma = 198 };
    }
    public MaidProp GetProp(MPN id) => Props.TryGetValue(id, out var prop) ? prop : null;
    public void Uninit() { }
    // These are game deserializer boundaries. Real deserializers and supplied cards
    // are exercised by BodyProfileTests against the installed game DLL itself.
    public static List<MaidProp> DeserializePropPre(BinaryReader r)
    {
        bool reference = Path.GetFileName(((FileStream)r.BaseStream).Name) == "reference.preset";
        return new() {
            new() { idx=0, name="Hara", type=1, value=reference ? 48 : 13 },
            new() { idx=1, name="west", type=2, value=reference ? 59 : 35 },
            new() { idx=2, name="chikubi", type=3, value=reference ? 1 : 0 }
        };
    }
    public static MaidParts.PartsColor[] DeserializeMultiColorPre(BinaryReader r)
    {
        bool reference = Path.GetFileName(((FileStream)r.BaseStream).Name) == "reference.preset";
        return new[] { new MaidParts.PartsColor { m_nMainBrightness = reference ? 163 : 303, m_nMainChroma = reference ? 94 : 198 } };
    }
    public string SaveShape() => string.Join("|", Props.Values.Select(p => $"{p.name},{p.value},{p.temp_value},{p.boDut},{p.boTempDut},{p.boTempExecuted},{p.min},{p.max}"))
        + string.Join(",", BodyChangeProfile.ColorValues(Parts.Color));
}
public class TBody
{
    public Maid maid;
    public Dictionary<string, float> Values = new(StringComparer.OrdinalIgnoreCase);
    public int Updates;
    public Texture2D LastColor;
    public void VertexMorph_FromProcItem(string tag, float f) { BodyChanges.Transform(this, tag, ref f); Values[tag] = f; Updates++; }
    public void BoneMorph_FromProcItem(string tag, float f) { BodyChanges.Transform(this, tag, ref f); Values[tag] = f; Updates++; }
    public void ItemScaleReset() { }
    public void UpdateInfinityColor(MaidParts.PARTS_COLOR kind) => LastColor = maid.Parts.GetPartsColorTableTex(kind);
}
public class MaidParts
{
    public enum PARTS_COLOR { NIPPLE }
    public struct PartsColor
    {
        public bool m_bUse;
        public int m_nMainHue, m_nMainChroma, m_nMainBrightness, m_nMainContrast, m_nShadowRate, m_nShadowHue, m_nShadowChroma, m_nShadowBrightness, m_nShadowContrast;
    }
    public PartsColor Color;
    public Texture2D Original = new(256, 1, TextureFormat.RGBA32, false);
    public PartsColor GetPartsColor(PARTS_COLOR kind) => Color;
    public Texture2D GetPartsColorTableTex(PARTS_COLOR kind)
    {
        var result = Original; BodyChanges.ColorTexture(this, kind, ref result); return result;
    }
}
public static class UTY
{
    public static void UpdateColorTableTexture(MaidParts.PartsColor c, ref Texture2D t) => t.Color = c;
}
