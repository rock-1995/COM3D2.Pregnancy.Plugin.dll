using System.Text.Json;
using UnityEngine;
using NM=System.Numerics.Matrix4x4;
using NV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunSkirtSparseBoneTests(string[] args)
    {
        var reports=new List<object>();
        foreach(var path in args.Skip(3))
        {
            Vector3[][] reference=null;float maxError=0;
            foreach(bool sparse in new[]{false,true})
            {
                var body=ReadModel(args[2],"body");var cloth=ReadModel(path,Path.GetFileNameWithoutExtension(path));var maid=new Maid();
                int originalCount=cloth.bones.Length;
                if(sparse)
                {
                    // Runtime palettes may contain optional unused/unresolved
                    // slots. All native vertex weights and bone indices stay intact.
                    cloth.bones=cloth.bones.Concat(new Transform[]{null,new Transform{name="UnusedOptionalBone"}}).ToArray();
                    cloth.sharedMesh.bindposes=cloth.sharedMesh.bindposes.Concat(new[]{Matrix4x4.identity,Matrix4x4.identity}).ToArray();
                }
                var nativeBones=cloth.bones;var nativeBinds=cloth.sharedMesh.bindposes;var nativeWeights=cloth.sharedMesh.boneWeights;
                PrepareALContext(maid,new(){body,cloth},1);Check(_alWorking!=null,"sparse-palette context calibrates");var c=_alWorking;
                var rec=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=cloth.sharedMesh.vertices};
                Check(TryDeformAL(cloth,rec,MeshMorphClass.OuterCloth,1,out var residual,out _) && rec.Skirt!=null,"sparse-palette skirt plan exists");
                var meshToWorld=MatrixBridge.ToUnity(rec.ToReference*c.PelvisBind*MatrixBridge.ToManaged(c.Pelvis.localToWorldMatrix));
                for(int i=0;i<originalCount;i++)nativeBones[i].localToWorldMatrix=meshToWorld*nativeBinds[i].inverse;
                cloth.sharedMesh.vertices=residual;InstallALBinding(maid,rec,residual,1);
                Check(ActiveBinding(cloth)!=null,"unused null palette slots must not discard skirt binding");
                var rest=nativeBones.Take(originalCount).Select(b=>b.localToWorldMatrix).ToArray();var bodyRest=body.bones.Select(b=>b.localToWorldMatrix).ToArray();
                var poses=new List<Vector3[]>();
                for(int pose=0;pose<5;pose++)
                {
                    var global=new Matrix4x4{Rows=NM.CreateRotationY(pose*.31f)*NM.CreateTranslation(pose*.2f,0,-pose*.1f)};
                    var sway=new Matrix4x4{Rows=NM.CreateRotationX(pose*.21f)};
                    for(int i=0;i<body.bones.Length;i++)body.bones[i].localToWorldMatrix=global*bodyRest[i];
                    for(int i=0;i<originalCount;i++)nativeBones[i].localToWorldMatrix=global*(IsDrapeBone(nativeBones[i])?sway:Matrix4x4.identity)*rest[i];
                    if(sparse && pose==1)nativeBones[originalCount+1]=null; // Optional slot disappears after installation.
                    UpdateALBindings(maid);RefreshSkirtPose(new TBody{maid=maid});
                    Check(ActiveBinding(cloth)!=null,"sparse skirt remains bound after both runtime pose hooks");
                    var actual=residual.Select((v,i)=>SkinPoint(cloth,v,i)).ToArray();poses.Add(actual);
                    if(sparse)for(int i=0;i<actual.Length;i++)
                    {float error=Vector3.Distance(actual[i],reference[pose][i]);maxError=Math.Max(maxError,error);Check(error<2e-5f,"empty optional slots do not change rendered vertices");}
                    if(pose==0)for(int i=0;i<actual.Length;i++)Check(Vector3.Distance(meshToWorld.inverse.MultiplyPoint3x4(actual[i]),rec.Skirt.VisualVertices[i])<2e-5f,"installed skirt receives its intended belly displacement");
                }
                if(!sparse)reference=poses.ToArray();
                else
                {
                    int used=Enumerable.Range(0,originalCount).First(i=>rec.Skirt.Controlled[i] && nativeWeights.Any(bw=>Influences(bw).Any(v=>v.Bone==i && v.Weight>0)));
                    nativeBones[used]=null;RefreshSkirtPose(new TBody{maid=maid});
                    Check(ActiveBinding(cloth)==null,"a missing weighted bone still restores native binding");
                    Check(cloth.sharedMesh.boneWeights==nativeWeights && cloth.sharedMesh.bindposes==nativeBinds,"failure restores exact weights and binds");
                }
                ReleaseALBindings(maid);
            }
            var row=new{model=Path.GetFileNameWithoutExtension(path),maxRenderedError=maxError,poses=5,unusedMissingAllowed=true,usedMissingRestores=true};reports.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));
        }
        File.WriteAllText(args[1],JsonSerializer.Serialize(new{assertions=tests,reports},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {tests} sparse-palette/runtime binding assertions.");
    }
}
