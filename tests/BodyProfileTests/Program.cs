using COM3D2.Pregnancy.Plugin;
using System.Reflection;

int checks = 0;
void Check(bool condition, string description)
{
    checks++;
    if (!condition) throw new Exception(description);
}
var profile = BodyChangeProfile.Load(args[0], args[1]);
var expected = new Dictionary<string, long> {
    ["MuneL"] = 25, ["MuneTare"] = 29, ["RegFat"] = 26, ["ArmL"] = 3,
    ["Hara"] = 35, ["RegMeet"] = 13, ["koshi"] = 44, ["kata"] = 1,
    ["west"] = 24, ["MuneUpDown"] = -7, ["MuneYawaraka"] = 5
};
Check(profile.Sliders.Count == 11, "exactly 11 slider changes");
foreach (var pair in expected) Check(profile.Sliders[pair.Key].Delta == pair.Value, pair.Key);
Check(!profile.Sliders.ContainsKey("chikubi") && !profile.Sliders.ContainsKey("chikubicolor"), "menu selections excluded");
Check(profile.Colors.Count == 3, "three color groups");
Check(profile.Colors[MaidParts.PARTS_COLOR.NIPPLE].SequenceEqual(new long[] { 5, -104, -140, 0, 0, 0, 163, 194, 0 }), "native nipple channel deltas");
Check(profile.Colors[MaidParts.PARTS_COLOR.EYE_L].SequenceEqual(new long[] { 0, 0, -2, 0, 0, -1, -7, 0, 0 }), "eye color deltas");
var identical = BodyChangeProfile.Load(args[0], args[0]);
Check(identical.Sliders.Count == 0 && identical.Colors.Count == 0, "identical presets produce no changes");
var reverse = BodyChangeProfile.Load(args[1], args[0]);
Check(reverse.Sliders["Hara"].Delta == -35, "negative custom delta");
Check(BodyChangeMath.Factor(0, 0f, 5) == 0, "initial no change");
Check(BodyChangeMath.Factor(0, .5f, 5) == .1, "first pregnancy halfway");
Check(BodyChangeMath.Factor(2, .5f, 5) == .5, "pregnancy plus birth history");
Check(BodyChangeMath.Factor(4, 1f, 5) == 1, "fifth full pregnancy");
Check(BodyChangeMath.Factor(5, .5f, 5) == 1 && BodyChangeMath.Factor(999, 1f, 5) == 1, "birth threshold saturation");
Check(BodyChangeMath.Factor(0, 0f, 0) == 1 && BodyChangeMath.Factor(0, .5f, -2) == 1, "zero and negative threshold branch");
Check(BodyChangeMath.Factor(3, 1f, 5) == BodyChangeMath.Factor(4, 0f, 5), "birth transition continuity");
Check(BodyChangeMath.Add(90, 25, 1, 0, 100) == 100, "upper limit");
Check(BodyChangeMath.Add(2, -7, 1, 0, 100) == 0, "lower limit");
Check(BodyChangeMath.Add(-40, -7, 1, -200, 500) == -47, "negative value supported");
Check(BodyChangeMath.Add(180, 25, 1, -200, 500) == 205, "over 100 supported");
Check(BodyChangeMath.Add(180, 25, 1, 500, -200) == 205, "reversed entered bounds");
var color = new MaidParts.PartsColor { m_nMainHue = 5, m_nMainChroma = 198, m_nMainBrightness = 303, m_nMainContrast = 100 };
var updated = BodyChangeProfile.AddColor(color, profile.Colors[MaidParts.PARTS_COLOR.NIPPLE], .5);
Check(updated.m_nMainBrightness == 233 && updated.m_nMainChroma == 146, "color interpolation retains native units >100");
Check(color.m_nMainBrightness == 303, "source color unchanged");
// Check the exact local game APIs used by the new Harmony hooks.
Check(typeof(TBody).GetMethod("VertexMorph_FromProcItem", new[] { typeof(string), typeof(float) }) != null, "vertex entry point");
Check(typeof(TBody).GetMethod("BoneMorph_FromProcItem", new[] { typeof(string), typeof(float) }) != null, "bone entry point");
Check(typeof(MaidParts).GetMethod("GetPartsColorTableTex", new[] { typeof(MaidParts.PARTS_COLOR) }) != null, "runtime color table entry point");
Check(typeof(Maid).GetMethod("Uninit") != null, "unload entry point");
Console.WriteLine($"PASS: {checks} profile/math/native API checks.");
