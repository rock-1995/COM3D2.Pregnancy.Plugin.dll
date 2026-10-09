using System.Text.Json;
using UnityEngine;
using NM=System.Numerics.Matrix4x4;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunBodyIsolationStudy(string[] args)
    {
        var snapshots=new List<object>();
        foreach(float stage in new[]{.987f,1f})
        foreach(bool withCloth in new[]{false,true})
        {
            var body=ReadModel(args[1],"body");body.sharedMesh.vertices=ReadPoints(args[2]);
            var cloth=ReadModel(args[4],"dress284_wear");
            var maid=new Maid();PrepareALContext(maid,withCloth?new(){body,cloth}:new(){body},stage);
            var nativeBones=body.bones;var binds=body.sharedMesh.bindposes;
            var rec=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=body.sharedMesh.vertices};
            TryDeformAL(body,rec,MeshMorphClass.Body,stage,out var changed,out _);
            var beforeClothing=(Vector3[])changed.Clone();
            if(withCloth)
            {
                var cr=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=cloth.sharedMesh.vertices};
                TryDeformAL(cloth,cr,MeshMorphClass.OuterCloth,stage,out var cv,out _);
                cloth.sharedMesh.vertices=cv;InstallALBinding(maid,cr,cv,stage);UpdateALBindings(maid);
                TryDeformAL(body,rec,MeshMorphClass.Body,stage,out changed,out _);
                Check(beforeClothing.Select((v,i)=>Vector3.Distance(v,changed[i])).Max()==0,"clothing evaluation does not alter body deformation");
            }
            var nativeRest=nativeBones.Select(b=>b.localToWorldMatrix).ToArray();
            var chest=body.sharedMesh.boneWeights.Select((w,i)=>(w,i)).Where(x=>BoneSum(x.w,nativeBones,IsBreastBoneName)>0).Select(x=>x.i).ToArray();
            float[] P(Vector3 v)=>new[]{v.x,v.y,v.z};
            var original=rec.OrigVerts.Select(P).ToArray();
            var shape=changed.Select(P).ToArray();
            var normals=DeformationNormals.Build(rec.OrigVerts,changed,body.sharedMesh.normals,body.sharedMesh.triangles).Select(P).ToArray();
            var alpha=changed.Select((_,i)=>Attachment(rec,i,stage)).ToArray();
            if(Shape.BreastExclusionEnabled)
                foreach(int i in chest)
                {
                    Check((changed[i]-rec.OrigVerts[i]).sqrMagnitude==0,"excluded breast vertex has no growth deformation");
                    Check(alpha[i]==0,"excluded breast vertex has no virtual attachment");
                }
            body.sharedMesh.vertices=changed;InstallALBinding(maid,rec,changed,stage);
            var poses=new List<object>();
            foreach(float bend in new[]{0f,.45f,-.55f})
            {
                var rotation=new Matrix4x4{Rows=NM.CreateRotationX(bend)};
                for(int i=0;i<nativeBones.Length;i++)nativeBones[i].localToWorldMatrix=(i==0?Matrix4x4.identity:rotation)*nativeRest[i];
                UpdateALBindings(maid);
                if(Shape.BreastExclusionEnabled)
                    foreach(int i in chest)
                    {
                        var expected=Vector3.zero;
                        foreach(var w in Influences(NativeWeights(body)[i]))if(w.Weight>0)
                            expected+=(nativeBones[w.Bone].localToWorldMatrix*binds[w.Bone]).MultiplyPoint3x4(rec.OrigVerts[i])*w.Weight;
                        Check(Vector3.Distance(expected,SkinPoint(body,changed[i],i))<2e-6f,"excluded breast retains native pose");
                    }
                poses.Add(new{bend,vertices=changed.Select((v,i)=>P(SkinPoint(body,v,i))).ToArray()});
            }
            snapshots.Add(new{stage,withCloth,original,shape,normals,alpha,chest,poses});
            ReleaseALBindings(maid);
        }
        File.WriteAllText(args[3],JsonSerializer.Serialize(snapshots));
        Console.WriteLine("Body and chest isolation snapshots: 2 stages, with/without clothing, 3 poses.");
    }
}
