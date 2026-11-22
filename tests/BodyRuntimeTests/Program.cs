using COM3D2.Pregnancy.Plugin;
using System.Reflection;

int checks = 0;
void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
void Apply(Maid m, float p) { var state = BodyChanges.Prepare(m, p); if (state != null) BodyChanges.Apply(state); }
bool Near(float a, float b) => Math.Abs(a - b) < .00001;
string folder = AppContext.BaseDirectory;
foreach (var name in new[] { "base.preset", "reference.preset" })
{
    using var stream = File.Create(Path.Combine(folder, name)); using var writer = new BinaryWriter(stream);
    writer.Write("CM3D2_PRESET"); writer.Write(24700); writer.Write(0); writer.Write(0);
}
var first = new Maid(13, 35); string original = first.SaveShape();
Apply(first, .5f);
Check(Near(first.body0.Values["RegMeet"], .17f) && Near(first.body0.Values["west"], .37f), "add deltas to own baseline");
Check(first.SaveShape() == original, "serializable props, ranges, temp flags and colors remain unchanged");
var color = first.body0.LastColor;
Check(color != first.Parts.Original && color.Color.m_nMainBrightness == 289, "runtime palette used");
for (int i=0;i<100;i++) Apply(first, .5f);
Check(first.body0.Updates == 2 && first.body0.LastColor == color, "repeated apply does not accumulate or rebuild");
var second = new Maid(50, 65) { Births = 2 };
Apply(second, .5f);
Check(Near(second.body0.Values["RegMeet"], .68f), "other cregmeetcter uses its own starting values");
Check(Near(first.body0.Values["RegMeet"], .17f), "other cregmeetcter unchanged");
first.body0.VertexMorph_FromProcItem("regmeet", .5f);
Check(Near(first.body0.Values["regmeet"], .54f), "AYS live argument receives delta");
first.body0.VertexMorph_FromProcItem("regmeet", .5f);
Check(Near(first.body0.Values["regmeet"], .54f), "AYS refresh does not accumulate");
Apply(first, .5f);
Check(Near(first.body0.Values["RegMeet"], .17f), "manual apply recalculates from maid source");
first.GetProp(MPN.RegMeet).value = 20; Apply(first, .5f);
Check(Near(first.body0.Values["RegMeet"], .24f), "editing source rebases without subtracting old saturated delta");
first.GetProp(MPN.RegMeet).value = 13;
first.Births = 4; Apply(first, 1f); float atBirth = first.body0.Values["RegMeet"];
first.Births = 5; Apply(first, 0f);
Check(Near(first.body0.Values["RegMeet"], atBirth), "full birth transition retains change");
first.Births = 99; Apply(first, .8f);
Check(Near(first.body0.Values["RegMeet"], .48f), "after x births only one full delta");
BodyChanges.Reset(first);
Check(Near(first.body0.Values["RegMeet"], .13f) && first.body0.LastColor == first.Parts.Original, "reset restores source geometry and palette");
Check(color.Destroyed, "old palette released");
Check(first.SaveShape() == original, "all runtime operations left save payload unchanged");
PregnancyPlugin.CfgBodyChangeLower.Value = -200; PregnancyPlugin.CfgBodyChangeUpper.Value = 500;
first.GetProp(MPN.RegMeet).value = 180; first.GetProp(MPN.west).value = -70; Apply(first, 0f);
Check(Near(first.body0.Values["RegMeet"], 2.15f) && Near(first.body0.Values["west"], -.46f), "unlocked range independent of MaidProp min/max");
PregnancyPlugin.CfgBodyChangeLower.Value = 0; PregnancyPlugin.CfgBodyChangeUpper.Value = 100; Apply(first, 0f);
Check(Near(first.body0.Values["RegMeet"], 1f) && Near(first.body0.Values["west"], 0f), "user range clamps final slider values");
PregnancyPlugin.CfgBodyChangeUpper.Value = 500; PregnancyPlugin.CfgBodyChangeLower.Value = -200; Apply(first, 0f);
Check(Near(first.body0.Values["RegMeet"], 2.15f), "expanding clamp restores baseline rather than losing prior overflow");
first.Parts.Color.m_nMainBrightness = 280; Apply(first, 0f);
Check(first.body0.LastColor.Color.m_nMainBrightness == 163 && first.Parts.Color.m_nMainBrightness == 280, "live color edit still reaches the absolute target without changing saved color");
BodyChanges.Forget(first);
Check(first.Parts.GetPartsColorTableTex(MaidParts.PARTS_COLOR.NIPPLE) == first.Parts.Original, "unload removes runtime palette");
Apply(second, 0f);
Check(Near(second.body0.Values["RegMeet"], .64f), "postpartum history works at zero pregnancy progress");
NativeBuildTests.Run(Check);
NippleColorTests.Run(Check);
File.Delete(Path.Combine(folder, "reference.preset")); Apply(second, .8f);
Check(Near(second.body0.Values["RegMeet"], .5f) && second.body0.LastColor == second.Parts.Original, "missing profile removes prior overlay safely");
Console.WriteLine($"PASS: {checks} runtime lifecycle checks.");
