using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunStageStudy(string[] args)
    {
        var dir=Path.GetFullPath(args[1]);Directory.CreateDirectory(dir);
        var body=ReadModel(args[2],"body-real");var maid=new Maid();Shape=new VtxSettings();
        PrepareALContext(maid,new(){body},1);var context=_alWorking;var profile=context.Frame.Profile;
        var original=body.sharedMesh.vertices;var weights=body.sharedMesh.boneWeights;var bones=body.bones;
        var material=original.Select(v=>NV.Transform(N(v),context.Maps[body]*context.InverseFrame)).ToArray();
        var skin=body.sharedMesh.GetTriangles(0).ToHashSet();
        bool Abdomen(int i)=>skin.Contains(i) && Math.Abs(material[i].X)<profile.Span*.45f && material[i].Y>profile.PelvicFloor && material[i].Y<profile.Ribs && material[i].Z>profile.AxisAt(material[i].Y) && BoneSum(weights[i],bones,ArmBone)<.001f;
        var stages=new List<object>();
        foreach(float progress in args.Contains("--fine")?new[]{.425f,.43f,.435f,.44f,.46f}:new[]{0f,.30f,1f/3,.34f,.35f,.36f,.37f,.38f,.39f,.40f,.42f,4f/9,.48f,5f/9,.739f,1f})
        {
            var r=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=original};
            Check(TryDeformAL(body,r,MeshMorphClass.Body,progress,out var result,out _),"stage study result");
            float peak=0,sum=0;int moved=0,n=0;
            using var csv=new StreamWriter(Path.Combine(dir,$"stage-{progress:F4}.csv"));csv.WriteLine("x,y,z,dx,dy,dz");
            for(int i=0;i<result.Length;i++)
            {
                var delta=NV.Transform(N(result[i]),context.Maps[body]*context.InverseFrame)-material[i];
                if(Abdomen(i)){n++;peak=Math.Max(peak,delta.Length());sum+=delta.LengthSquared();if(delta.Length()>1e-6f)moved++;}
                csv.WriteLine(FormattableString.Invariant($"{material[i].X:R},{material[i].Y:R},{material[i].Z:R},{delta.X:R},{delta.Y:R},{delta.Z:R}"));
            }
            var egg=BellyShape.Growth(profile,progress,Shape);
            var info=new{progress,peak,peakPercentSpan=100*peak/profile.Span,rms=Math.Sqrt(sum/n),moved,n,width=2*egg.HalfWidth,height=egg.Top-egg.Bottom,depth=2*egg.Depth};
            stages.Add(info);Console.WriteLine(JsonSerializer.Serialize(info));
        }
        File.WriteAllText(Path.Combine(dir,"stages.json"),JsonSerializer.Serialize(stages,new JsonSerializerOptions{WriteIndented=true}));
        var variants=new List<object>();
        foreach(var values in new[]{(0f,0f,0f),(3f,0f,0f),(0f,3f,0f),(0f,3f,1f),(3f,3f,1f)})
        {
            Shape.ThighGuardSpeed=values.Item1;Shape.InnerThighGuardStrength=values.Item2;Shape.ThighGuardSmoothStrength=values.Item3;
            var r=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=original};
            Check(TryDeformAL(body,r,MeshMorphClass.Body,1,out var result,out _),"guard sample");
            var d=result.Select((v,i)=>Vector3.Distance(v,original[i])).ToArray();
            variants.Add(new{speed=values.Item1,inner=values.Item2,smooth=values.Item3,changed=d.Count(v=>v>1e-6f),max=d.Max(),sumSquares=d.Sum(v=>(double)v*v),maxRestore=r.ThighGuardRestore?.Max()??0});
        }
        File.WriteAllText(Path.Combine(dir,"guards.json"),JsonSerializer.Serialize(variants,new JsonSerializerOptions{WriteIndented=true}));
    }
}
