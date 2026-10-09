using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using GV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunNavelStudy(string[] args)
    {
        var dir=Path.GetFullPath(args[1]);Directory.CreateDirectory(dir);
        var exported=JsonSerializer.Deserialize<VtxSettings>(File.ReadAllText(args[3]),new JsonSerializerOptions{IncludeFields=true});
        var defaults=new VtxSettings();
        foreach(var f in typeof(VtxSettings).GetFields())if(f.Name!="NavelVerticalOffset")Check(Equals(f.GetValue(defaults),f.GetValue(exported)),"export equals default "+f.Name);
        var body=ReadModel(args[2],"body-real");var maid=new Maid();Shape=defaults.Copy();
        PrepareALContext(maid,new(){body},1);var c=_alWorking;var p=c.Frame.Profile;var original=body.sharedMesh.vertices;
        var baseline=Shape.Copy();baseline.NavelVerticalOffset=0;
        var off=baseline.Copy();off.NavelEversion=0;off.NavelProportion=0;
        var material=original.Select(v=>GV.Transform(N(v),c.Maps[body]*c.InverseFrame)).ToArray();var skin=body.sharedMesh.GetTriangles(0).ToHashSet();
        var report=new List<object>();
        foreach(float offset in new[]{0f,-.005f,-.015f,.015f,-.06f,.06f})
        {
            Shape=defaults.Copy();Shape.NavelVerticalOffset=offset;PrepareALContext(maid,new(){body},1);
            var r=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=original};
            Check(TryDeformAL(body,r,MeshMorphClass.Body,1,out var result,out _),"offset applies");
            int changed=0;double total=0,center=0;float max=0,peakY=0;
            float centerY=p.SkinNavelY+offset*p.Span,rad=Shape.NavelRadius*p.Span;
            using var csv=new StreamWriter(Path.Combine(dir,$"offset-{offset:0.000}.csv"));csv.WriteLine("x,y,z,dx,dy,dz,patchZ");
            for(int i=0;i<original.Length;i++)
            {
                var v=material[i];var baseP=BellyShape.Deform(v,p,1,off);var b=BellyShape.Deform(v,p,1,baseline);var q=BellyShape.Deform(v,p,1,Shape);
                Check(Scalar.IsFinite(q.X+q.Y+q.Z),"finite navel result");
                float r0=MathF.Sqrt(v.X*v.X/(rad*rad)+(v.Y-p.SkinNavelY)*(v.Y-p.SkinNavelY)/(rad*rad*1.69f));
                float r1=MathF.Sqrt(v.X*v.X/(rad*rad)+(v.Y-centerY)*(v.Y-centerY)/(rad*rad*1.69f));
                if(r0>=1 && r1>=1)Check((q-b).LengthSquared()==0,"outside both navel patches exact identity");
                if((q-b).LengthSquared()>1e-15f)changed++;
                float bump=Math.Max(0,q.Z-baseP.Z);
                if(skin.Contains(i) && bump>0){total+=bump;center+=v.Y*bump;if(bump>max){max=bump;peakY=v.Y;}}
                csv.WriteLine(FormattableString.Invariant($"{v.X:R},{v.Y:R},{v.Z:R},{q.X-v.X:R},{q.Y-v.Y:R},{q.Z-v.Z:R},{bump:R}"));
                if(BoneSum(body.sharedMesh.boneWeights[i],body.bones,ArmBone)>.999f)Check((result[i]-original[i]).sqrMagnitude==0,"arms unchanged");
            }
            report.Add(new{offset,centerY,patchCentroid=total>0?center/total:double.NaN,peakY,max,changedVertices=changed});
        }
        var saved=new VtxSettings{NavelVerticalOffset=0};saved.CompleteGrowthDefaults("{\"NavelVerticalOffset\":0}");Check(saved.NavelVerticalOffset==0,"saved zero preserved");
        saved.CompleteGrowthDefaults("{}");Check(saved.NavelVerticalOffset==defaults.NavelVerticalOffset,"missing offset gets default");
        Shape=defaults.Copy();int hash=ComputeMorphBakeSignature(1,MeshMorphClass.Body);Shape.NavelVerticalOffset+=.000001f;Check(hash!=ComputeMorphBakeSignature(1,MeshMorphClass.Body),"offset invalidates cache");Shape=defaults.Copy();
        File.WriteAllText(Path.Combine(dir,"navel-study.json"),JsonSerializer.Serialize(new{p.Span,p.SkinNavelY,rows=report,assertions=tests},new JsonSerializerOptions{WriteIndented=true,NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals}));
        Console.WriteLine($"PASS: {tests} export/default, local navel shift, arm, finite, range and cache assertions.");Console.WriteLine(JsonSerializer.Serialize(report));
    }
}
