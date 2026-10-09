using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void TestOuterClothContact()
    {
        var (maid,body,cloth)=Fixture();
        // Closely fitted fabric must retain the exact existing inner-cloth path.
        cloth.sharedMesh.vertices=body.sharedMesh.vertices.Select(v=>new Vector3(v.x*1.002f,v.y,(v.z+.15f)*1.002f-.15f)).ToArray();
        PrepareALContext(maid,new(){body,cloth},1);var context=_alWorking;
        var rec=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=cloth.sharedMesh.vertices};
        TryDeformAL(cloth,rec,MeshMorphClass.OuterCloth,1,out var outer,out _);
        TryDeformAL(cloth,rec,MeshMorphClass.InnerCloth,1,out var legacy,out _);
        float fittedError=outer.Select((v,i)=>Vector3.Distance(v,legacy[i])).Max();
        Check(fittedError<2e-6f,"close fitted clothing keeps original deformation");
        // Height outside the field remains unchanged. Radial distance is no
        // longer a criterion for turning outer fabric deformation off.
        float span=context.Frame.Profile.Span;
        var far=body.sharedMesh.vertices.Select(v=>new Vector3(v.x,v.y+span*3,v.z)).ToArray();
        cloth.sharedMesh.vertices=far;var farRec=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=far};
        TryDeformAL(cloth,farRec,MeshMorphClass.OuterCloth,1,out var untouched,out _);
        Check(untouched.Select((v,i)=>Vector3.Distance(v,far[i])).Max()<2e-6f,"cloth outside field height remains unchanged");
        int closeCount=0,looseCount=0;
        foreach(var tri in context.Surface.SurfaceTriangles.Where((_,i)=>i%7==0))
        {
            var m=context.Surface.Meshes[tri.MeshIndex];var center=(m.Original[tri.A]+m.Original[tri.B]+m.Original[tri.C])/3;
            var normal=Vector3.Cross(m.Original[tri.B]-m.Original[tri.A],m.Original[tri.C]-m.Original[tri.A]).normalized;
            // Choose the outward orientation without relying on triangle winding.
            if(Vector3.Dot(normal,new Vector3(center.x,0,center.z+.15f))<0)normal=-normal;
            var p=center+normal*(span*.08f);
            var local=NV.Transform(N(p),context.InverseFrame);
            float old=BellyShape.ClothingFootprint(local,context.Frame.Profile,1,Shape);
            float radialFree=BellyShape.ClothingFootprint(local,context.Frame.Profile,1,Shape,false);
            var continuous=BellyShape.DeformOuterCloth(local,context.Frame.Profile,1,Shape)-local;
            var skin=BellyShape.Deform(local,context.Frame.Profile,1,Shape)-local;
            if(radialFree>old+.1f && continuous.Length()>1e-4f)looseCount++;
            if(old>.9999f){closeCount++;Check(NV.Distance(continuous,skin)<1e-5f,"fully supported cloth keeps existing field result");}
        }
        Check(looseCount>0,"outer fabric receives continuous field beyond original radial skin support");
        float y=context.Frame.Profile.Navel,angle=.45f;NV? first=null;
        for(int j=0;j<20;j++)
        {
            float radius=span*(1+j*.1f);
            var p=new NV(radius*MathF.Sin(angle),y,context.Frame.Profile.AxisAt(y)+radius*MathF.Cos(angle));
            var delta=BellyShape.DeformOuterCloth(p,context.Frame.Profile,1,Shape)-p;
            if(first.HasValue)Check(NV.Distance(first.Value,delta)<2e-5f,"loose cloth field has no radial cutoff or distance-based jump");
            else first=delta;
        }
        TryDeformAL(cloth,rec,MeshMorphClass.OuterCloth,0,out var zero,out _);
        Check(zero.Select((v,i)=>Vector3.Distance(v,rec.OrigVerts[i])).Max()<2e-6f,"stage zero restores loose-contact path");
        Console.WriteLine($"Outer field regression: fitted max change={fittedError:R}, extended-field probes={looseCount}");
    }
}
