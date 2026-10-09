using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NM=System.Numerics.Matrix4x4;
using NV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;

namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunGuardSimulation(string[] args)
    {
        var body=ReadModel(args[1],"body-guard");var maid=new Maid();
        var original=body.sharedMesh.vertices;
        var cloth=Reexpress(body,NM.Identity,"wear-guard");
        PrepareALContext(maid,new(){body,cloth},1);var context=_alWorking;
        var record=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=original};
        Check(Shape.ThighGuardSpeed==0 && Shape.InnerThighGuardStrength==3 && Shape.ThighGuardSmoothStrength==1,"requested guard defaults");
        Shape.InnerThighGuardStrength=0;Shape.ThighGuardSmoothStrength=0;
        Check(TryDeformAL(body,record,MeshMorphClass.Body,1,out var baseline,out _),"guard off body");
        Check(record.ThighGuardRestore==null,"off does not allocate or evaluate per-vertex guard");
        float previousReduction=-1;
        foreach(float speed in new[]{.5f,4f,8f,16f})
        {
            Shape.ThighGuardSpeed=speed;
            Check(TryDeformAL(body,record,MeshMorphClass.Body,1,out var result,out _),"guard on body");
            int reduced=0;float largestReduction=0;
            for(int i=0;i<original.Length;i++)
            {
                float a=(baseline[i]-original[i]).magnitude,b=(result[i]-original[i]).magnitude;
                Check(b<=a+2e-7f,"guard can only restore toward original");
                if(a-b>1e-7f){reduced++;largestReduction=Math.Max(largestReduction,a-b);}
                var local=Growth.Numerics.Vector3.Transform(N(original[i]),record.ToReference*context.InverseFrame);
                if(local.Y>=context.Frame.Profile.Navel)Check((result[i]-baseline[i]).sqrMagnitude==0,"upper abdomen unchanged by lower guard");
            }
            Check(reduced>0 && largestReduction>=previousReduction,"speed increases restoration including beyond slider");previousReduction=largestReduction;
            Console.WriteLine($"speed={speed}: restored vertices={reduced}, largest reduction={largestReduction:R}");
        }
        Shape.ThighGuardSpeed=4;Shape.InnerThighGuardStrength=1;
        Check(TryDeformAL(body,record,MeshMorphClass.Body,1,out var guarded,out _),"combined body guards");
        var savedMask=(float[])record.ThighGuardRestore.Clone();
        int inner=0;
        for(int i=0;i<original.Length;i++)
            if(BoneSum(body.sharedMesh.boneWeights[i],body.bones,IsInnerThighGuardBone)>=.25f)
            {inner++;Check((guarded[i]-original[i]).sqrMagnitude==0 && Attachment(record,i,1)==0,"full inner guard restores geometry and native binding");}
        Check(inner>0,"actual model has inner thigh bone coverage");
        var clothRecord=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=original};
        Check(TryDeformAL(cloth,clothRecord,MeshMorphClass.OuterCloth,1,out var clothed,out _),"guard on clothing");
        InstallALBinding(maid,clothRecord,clothed,1);
        for(int i=0;i<original.Length;i++)if(clothRecord.ThighGuardRestore[i]>=1)
            Check(cloth.sharedMesh.boneWeights[i].Equals(NativeWeights(cloth)[i]),"cloth sampling cannot reattach fully guarded vertices");
        ReleaseALBindings(maid);
        Shape.ThighGuardSmoothStrength=.35f;
        Check(TryDeformAL(body,record,MeshMorphClass.Body,1,out var smoothed,out _),"guard smoothing applies");
        Check(smoothed.Select((v,i)=>(v-guarded[i]).sqrMagnitude).Max()>1e-10f,"smoothing has observable effect");
        Check(smoothed.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"smoothed guard finite");
        Shape.ThighGuardSmoothStrength=0;
        foreach(var coordinates in new[]{NM.CreateScale(-1,1,1),NM.CreateScale(100)*NM.CreateRotationX(.6f)*NM.CreateTranslation(3,-2,1),NM.CreateScale(.7f,1.8f,1.1f)*NM.CreateRotationZ(.6f)*NM.CreateTranslation(1,2,-1)})
        {
            var transformed=Reexpress(body,coordinates,"body-guard-transform");var m=new Maid();
            PrepareALContext(m,new(){transformed},1);
            var r=new MeshRecord{SMR=transformed,Mesh=transformed.sharedMesh,OrigVerts=transformed.sharedMesh.vertices};
            Check(TryDeformAL(transformed,r,MeshMorphClass.Body,1,out var changed,out _),"coordinate guard applies");
            NM.Invert(coordinates,out var inverse);float maximum=0;
            for(int i=0;i<changed.Length;i++)
            {
                float distance=Vector3.Distance((Vector3)System.Numerics.Vector3.Transform(changed[i],inverse),guarded[i]);
                maximum=Math.Max(maximum,distance);
                Check(distance<3e-5f,"guard is invariant to mesh coordinate expression");
                Check(Math.Abs(r.ThighGuardRestore[i]-savedMask[i])<2e-5f,"guard mask uses consistent torso units");
            }
            Console.WriteLine("Guard coordinate maximum error="+maximum.ToString("R"));
        }
        PrepareALContext(maid,new(){body},1);
        Shape.ThighGuardSpeed=0;Shape.InnerThighGuardStrength=0;Shape.ThighGuardSmoothStrength=1;
        Check(TryDeformAL(body,record,MeshMorphClass.Body,1,out var off,out _),"both guards disabled again");
        Check(record.ThighGuardRestore==null && off.Select((v,i)=>(v-baseline[i]).sqrMagnitude).Max()==0,"disabling restores exact baseline; smoothing alone inactive");
        Shape.ThighGuardSmoothStrength=0;
        Console.WriteLine($"PASS: {tests} thigh guard assertions; {inner} fully protected inner-thigh vertices.");
    }
}
