using UnityEngine;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void TestArmExclusion()
    {
        var (maid,body,cloth)=Fixture();
        var arm=new Transform{name="Bip01 R UpperArm",parent=body.bones[3],localToWorldMatrix=Matrix4x4.identity};
        var sleeve=new Transform{name="FurisodeSpring01",parent=arm,localToWorldMatrix=Matrix4x4.identity};
        int index=cloth.bones.Length;
        cloth.bones=cloth.bones.Concat(new[]{arm,sleeve}).ToArray();
        cloth.sharedMesh.bindposes=cloth.sharedMesh.bindposes.Concat(new[]{Matrix4x4.identity,Matrix4x4.identity}).ToArray();
        var native=Enumerable.Repeat(new BoneWeight{boneIndex0=0,weight0=.9999f,boneIndex1=index+1,weight1=.0001f},cloth.sharedMesh.vertexCount).ToArray();
        cloth.sharedMesh.boneWeights=native;
        PrepareALContext(maid,new(){body,cloth},1);
        var rec=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=cloth.sharedMesh.vertices};
        TryDeformAL(cloth,rec,MeshMorphClass.OuterCloth,1,out var changed,out _);
        for(int i=0;i<changed.Length;i++)
        {
            Check((changed[i]-rec.OrigVerts[i]).sqrMagnitude==0,"tiny custom sleeve weight excludes growth and contact repair");
            Check(rec.TorsoOwnership[i]==0 && Attachment(rec,i,1)==0,"sleeve keeps native pose ownership");
        }
        InstallALBinding(maid,rec,changed,1);
        Check(!_alBindings.ContainsKey(cloth),"excluded sleeve is not rebound");
        Check(!IsALArmHierarchy(body.bones[0]),"pelvis is not an arm descendant");
        Console.WriteLine("Arm exclusion regression: custom sleeve descendant, positive 0.01% weight, upper bone filter disabled.");
    }
}
