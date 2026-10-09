using System.Globalization;
using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using GV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;

namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunNavelTimingTests(string[] args)
    {
        string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        var defaults=new VtxSettings();Shape=defaults.Copy();
        var exported=JsonDocument.Parse(File.ReadAllText(args[3])).RootElement.GetProperty("fields");
        foreach(var field in typeof(VtxSettings).GetFields())
        {
            string text=exported.GetProperty(field.Name).GetString();var value=field.GetValue(defaults);
            if(field.Name=="NavelPreviewFull")Check(!(bool)value,"full preview defaults off for pregnancy timing");
            else if(field.Name=="NavelStart")Check((float)value==.4f,"user-selected start is 40 percent");
            else if(value is bool boolean)Check(boolean==bool.Parse(text),"exported bool default "+field.Name);
            else Check(float.Parse(((float)value).ToString("G7",CultureInfo.InvariantCulture),CultureInfo.InvariantCulture)==float.Parse(text,CultureInfo.InvariantCulture),"exported float default "+field.Name);
        }
        Check(defaults.SagStrength==-.8f && defaults.BellySag==2f,"last exported sag settings are defaults");
        float previous=0;
        for(int i=0;i<=10000;i++)
        {
            float stage=i/10000f,response=BellyShape.NavelStageResponse(stage,defaults);
            float expected=stage<=.4f?0:(stage-.4f)/.6f;
            Check(float.IsFinite(response) && Math.Abs(response-expected)<2e-7f,"linear pregnancy response");
            Check(response>=previous,"monotone response");
            if(stage>.4f)Check(response>previous,"keeps advancing until full term");
            if(stage<1)Check(response<1,"does not reach full response before 100 percent");
            previous=response;
        }
        Check(BellyShape.NavelStageResponse(MathF.BitDecrement(1f),defaults)<1,"last representable progress below full term remains below full response");
        Check(BellyShape.NavelStageResponse(1f,defaults)==1,"100 percent exactly full");
        var retimed=defaults.Copy();retimed.SubtleStageProgress=.1f;retimed.VisibleStageProgress=.2f;retimed.MidStageProgress=.7f;
        foreach(float progress in new[]{.4f,.5f,.7f,.9f,.99f,1f})
            Check(BellyShape.NavelStageResponse(progress,retimed)==BellyShape.NavelStageResponse(progress,defaults),"navel uses pregnancy progress independently of belly growth remap");
        foreach(float start in new[]{-.5f,0f,.4f,.8518868f,1f,2f})
        {
            var p=defaults.Copy();p.NavelStart=start;
            foreach(float progress in new[]{0f,.1f,.4f,.7f,.9f,.99f,1f})
            {
                float value=BellyShape.NavelStageResponse(progress,p);
                Check(float.IsFinite(value)&&value>=0&&value<=1,"start edge cases remain finite and bounded");
                if(progress<Math.Clamp(start,0,1))Check(value==0,"no response before configured start");
            }
        }
        var preview=defaults.Copy();preview.NavelPreviewFull=true;
        Check(BellyShape.NavelStageResponse(.1f,preview)==1 && BellyShape.NavelStageResponse(0,preview)==0,"explicit full preview remains available");

        var body=ReadModel(args[2],"body-navel-timing");var maid=new Maid();
        PrepareALContext(maid,new(){body},1);var c=_alWorking;var profile=c.Frame.Profile;
        var material=body.sharedMesh.vertices.Select(v=>GV.Transform(N(v),c.Maps[body]*c.InverseFrame)).ToArray();
        var without=defaults.Copy();without.NavelEversion=without.NavelProportion=0;
        int unrelatedComparisons=0,fullTermComparisons=0;float worstRampError=0;var rows=new List<object>();
        foreach(float stage in new[]{0f,.28f,.4f,.4001f,.5f,.6f,.7f,.8f,.9f,.95f,.99f,1f})
        {
            var off=material.Select(v=>V10BellyShape.Deform(v,profile,stage,without)).ToArray();
            var full=material.Select(v=>V10BellyShape.Deform(v,profile,stage,preview)).ToArray();
            float response=BellyShape.NavelStageResponse(stage,defaults),peakEversion=0,stageError=0;
            var current=material.Select(v=>BellyShape.Deform(v,profile,stage,defaults)).ToArray();
            for(int i=0;i<material.Length;i++)
            {
                Check(Scalar.IsFinite(current[i].X+current[i].Y+current[i].Z),"finite actual navel geometry");
                float error=GV.Distance(current[i],off[i]+(full[i]-off[i])*response);
                stageError=Math.Max(stageError,error);
                Check(error<profile.Span*2e-6f,"actual navel correction follows the entire 40-to-100 ramp");
                if(full[i].Equals(off[i])){Check(current[i].Equals(full[i]),"unrelated body geometry exactly unchanged");unrelatedComparisons++;}
                if(stage<=.4f)Check(current[i].Equals(off[i]),"no navel correction before or at start");
                if(stage==1){Check(current[i].Equals(full[i]),"full-term geometry exactly retains exported full appearance");fullTermComparisons++;}
                peakEversion=Math.Max(peakEversion,current[i].Z-off[i].Z);
            }
            worstRampError=Math.Max(worstRampError,stageError);
            rows.Add(new{progress=stage,response,peakEversionMetres=peakEversion,maxLinearRampError=stageError});
        }
        int before=ComputeMorphBakeSignature(.7f,MeshMorphClass.Body);Shape.NavelStart=.5f;
        Check(before!=ComputeMorphBakeSignature(.7f,MeshMorphClass.Body),"start change invalidates bake cache");Shape=defaults;
        var result=new{assertions=tests,exportedFields=51,defaultSag=defaults.SagStrength,defaultBellySag=defaults.BellySag,
            defaultNavelStart=defaults.NavelStart,defaultPreview=defaults.NavelPreviewFull,unrelatedComparisons,fullTermComparisons,worstRampError,rows};
        File.WriteAllText(Path.Combine(output,"navel-timing.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {tests} default/timing/mesh assertions; {unrelatedComparisons} unrelated points and {fullTermComparisons} full-term points exactly preserved; max ramp error {worstRampError:R}.");
    }
}
