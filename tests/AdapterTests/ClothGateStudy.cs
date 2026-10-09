using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunClothGateStudy(string[] args)
    {
        Shape.BreastExclusionEnabled=false;Shape.UpperBoneFilterEnabled=false;
        var body=ReadModel(args[1],"body");body.sharedMesh.vertices=ReadPoints(args[2]);
        var cloth=ReadModel(args[3],"wear");cloth.sharedMesh.vertices=ReadPoints(args[4]);
        var maid=new Maid();PrepareALContext(maid,new(){body,cloth},1);var c=_alWorking;var t=c.Frame.Profile;
        var rec=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=cloth.sharedMesh.vertices};
        TryDeformAL(cloth,rec,MeshMorphClass.OuterCloth,1,out var changed,out _);
        float[] P(NV p)=>new[]{p.X,p.Y,p.Z};
        var rows=new List<object>();
        for(int i=0;i<rec.OrigVerts.Length;i++)
        {
            var reference=NV.Transform(N(rec.OrigVerts[i]),rec.ToReference);var local=NV.Transform(reference,c.InverseFrame);
            TryFindNearestBodySurfaceTriangle(c.Surface,U(reference),out var hit);var tri=c.Surface.SurfaceTriangles[hit.TriangleIndex];var m=c.Surface.Meshes[tri.MeshIndex];
            var surface=(m.Morphed[tri.A]*hit.Barycentric.x+m.Morphed[tri.B]*hit.Barycentric.y+m.Morphed[tri.C]*hit.Barycentric.z);
            var closest=NV.Transform(N(hit.Closest),c.InverseFrame);var motion=NV.TransformNormal(N(surface-hit.Closest)*Shape.ClothOffset,c.InverseFrame);
            TryGetBodySurfaceClothTarget(c.Surface,U(reference),Shape.ClothOffset,c.Frame,out var target);
            float reach=1-BellyShape.Smooth((Mathf.Sqrt(hit.DistanceSq)-motion.Length())/(t.Span*.025f));
            float mask=BellyShape.ClothingFootprint(local,t,1,Shape);float raw=BellyShape.ClothingFootprint(local,t,1,Shape,false);
            var offset=reference-N(hit.Closest);
            rows.Add(new{i,original=P(local),changed=P(NV.Transform(N(changed[i]),rec.ToReference*c.InverseFrame)),
                axis=t.AxisAt(local.Y),angleDegrees=Math.Atan2(local.X,local.Z-t.AxisAt(local.Y))*180/Math.PI,
                nearest=P(closest),nearestAxis=t.AxisAt(closest.Y),nearestAngleDegrees=Math.Atan2(closest.X,closest.Z-t.AxisAt(closest.Y))*180/Math.PI,
                directMask=mask,fieldMask=raw,radialSupport=raw>0?mask/raw:0,reach,effective=Mathf.Max(mask,raw*reach),
                skinFieldMask=BellyShape.ClothingFootprint(closest,t,1,Shape,false),gap=Mathf.Sqrt(hit.DistanceSq),bodyMotion=P(motion),
                targetDelta=P(NV.Transform(N(target),c.InverseFrame)-local),directDelta=P(BellyShape.Deform(local,t,1,Shape)-local),
                bodyNormalPush=offset.Length()>0?NV.Dot(NV.TransformNormal(N(surface-hit.Closest),c.InverseFrame),NV.TransformNormal(offset,c.InverseFrame)/offset.Length()):0,
                bones=Influences(NativeWeights(cloth)[i]).Select(w=>new{name=NativeBones(cloth)[w.Bone].name,weight=w.Weight}).ToArray()});
        }
        File.WriteAllText(args[5],JsonSerializer.Serialize(new{span=t.Span,rows}));
        Console.WriteLine("Recorded clothing/body coordinates and every v3 contact gate.");
    }
}
