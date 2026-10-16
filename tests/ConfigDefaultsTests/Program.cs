using BepInEx.Configuration;
using COM3D2.Pregnancy.Plugin;
using COM3D2.Pregnancy.Plugin.Growth;
using System.Text.Json;
using System.Globalization;

int checks=0;
void Check(bool condition,string reason){checks++;if(!condition)throw new Exception(reason);}
string oldX="\u8fbe\u5230\u5b8c\u6574\u53d8\u5316\u7684\u751f\u4ea7\u6b21\u6570 x";
string oldLo="\u4f53\u578b\u6570\u503c\u4e0b\u9650",oldHi="\u4f53\u578b\u6570\u503c\u4e0a\u9650";
string folder=Path.Combine(AppContext.BaseDirectory,"fixtures");Directory.CreateDirectory(folder);
var descriptions=new[]{("Births for Full Change",oldX,5),("Body Value Minimum",oldLo,0),("Body Value Maximum",oldHi,100)};
foreach(bool english in new[]{false,true})
{
    string path=Path.Combine(folder,english?"both.cfg":"legacy.cfg");
    File.WriteAllText(path,$"[Body Changes]\n{oldX} = 3\n{oldLo} = -200\n{oldHi} = 250\n"+(english?"Births for Full Change = 5\nBody Value Minimum = -90\nBody Value Maximum = 180\n":"")+"[General]\nPregnancy Weeks = 20\n");
    var config=new ConfigFile(path,false);
    int[] expected=english?new[]{5,-90,180}:new[]{3,-200,250};
    for(int i=0;i<descriptions.Length;i++)
    {
        var (key,legacy,def)=descriptions[i];var entry=ConfigKeyMigration.BindInt(config,"Body Changes",key,legacy,def,"English description");
        Check(entry.Value==expected[i],"saved value preserved or English key takes precedence");
        Check((int)entry.DefaultValue==def,"metadata keeps documented default");
        Check(entry.Description.AcceptableValues==null,"no extra range restriction");
    }
    string saved=File.ReadAllText(path);
    Check(!saved.Contains(oldX)&&!saved.Contains(oldLo)&&!saved.Contains(oldHi),"legacy keys removed");
    Check(saved.Contains("Pregnancy Weeks = 20"),"unrelated setting retained");
    var reload=new ConfigFile(path,false);
    for(int i=0;i<descriptions.Length;i++)
    {
        var(key,legacy,def)=descriptions[i];
        Check(ConfigKeyMigration.BindInt(reload,"Body Changes",key,legacy,def,"English description").Value==expected[i],"upgrade is idempotent");
    }
}
var defaults=new VtxSettings();
var expectedDefaults=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"expected-defaults.json")));
Check(expectedDefaults.Count==51,"complete logged parameter block");
foreach(var pair in expectedDefaults)
{
    var field=typeof(VtxSettings).GetField(pair.Key);Check(field!=null,"logged field exists");
    object expected=field.FieldType==typeof(bool)?bool.Parse(pair.Value):float.Parse(pair.Value,CultureInfo.InvariantCulture);
    Check(field.GetValue(defaults).Equals(expected),"default matches logged value: "+pair.Key);
}
Check(defaults.LowerBoundaryArc==.10f&&defaults.UpperBoundaryArc==.10f,"arc defaults preserve previous curvature");
var legacyArc=new VtxSettings{LowerBoundaryArc=0,UpperBoundaryArc=0};
legacyArc.CompleteGrowthDefaults("{\"growth\":{}}");
Check(legacyArc.LowerBoundaryArc==.10f&&legacyArc.UpperBoundaryArc==.10f,"missing arc fields migrate from legacy zero initialization");
foreach(float lower in new[]{0f,-.12f,.24f,.7f})
{
    var options=new JsonSerializerOptions{IncludeFields=true};
    var saved=new VtxSettings{LowerBoundaryArc=lower,UpperBoundaryArc=.23f};
    string json=JsonSerializer.Serialize(saved,options);
    var loaded=JsonSerializer.Deserialize<VtxSettings>(json,options);loaded.CompleteGrowthDefaults(json);
    Check(loaded.LowerBoundaryArc==lower&&loaded.UpperBoundaryArc==.23f,"explicit zero, signed and large arcs survive save/reload");
    Check(loaded.Copy().LowerBoundaryArc==lower&&loaded.Copy().UpperBoundaryArc==.23f,"per-maid copy preserves separate arc controls");
}
var partialArc=new VtxSettings{LowerBoundaryArc=0,UpperBoundaryArc=0};
partialArc.CompleteGrowthDefaults("{\"LowerBoundaryArc\":0}");
Check(partialArc.LowerBoundaryArc==0&&partialArc.UpperBoundaryArc==.10f,"migration preserves explicit zero independently");
Console.WriteLine($"PASS: {checks} config migration, logged-default and arc persistence checks.");
