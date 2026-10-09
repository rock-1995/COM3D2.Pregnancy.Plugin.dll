using System.Text.Json;
using UnityEngine;
using NM=System.Numerics.Matrix4x4;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunClothingPoseStudy(string[] args)
    {
        string name=args.Length>4?args[4]:"dress014_bra",input=args[1],models=args[2];
        float stage=JsonDocument.Parse(File.ReadAllText(Path.Combine(input,"inputs.json"))).RootElement.EnumerateArray().First(e=>e.GetProperty("model").GetString()==name).GetProperty("stage").GetSingle();
        var body=ReadModel(Path.Combine(models,"LOmobchara_extra_v1_beta.model"),"body");body.sharedMesh.vertices=ReadPoints(Path.Combine(input,name+"-body.csv"));
        var cloth=ReadModel(Path.Combine(models,name+".model"),name);cloth.sharedMesh.vertices=ReadPoints(Path.Combine(input,name+"-original.csv"));
        var maid=new Maid();PrepareALContext(maid,new(){body,cloth},stage);var c=_alWorking;
        var records=new[]{body,cloth}.Select(r=>new MeshRecord{SMR=r,Mesh=r.sharedMesh,OrigVerts=r.sharedMesh.vertices}).ToArray();
        var bones=records.Select(r=>r.SMR.bones).ToArray();var binds=records.Select(r=>r.Mesh.bindposes).ToArray();var weights=records.Select(r=>r.Mesh.boneWeights).ToArray();
        for(int k=0;k<2;k++)
        {
            var r=records[k];TryDeformAL(r.SMR,r,k==0?MeshMorphClass.Body:MeshMorphClass.InnerCloth,stage,out var v,out _);r.Mesh.vertices=v;
            var world=MatrixBridge.ToUnity(r.ToReference*c.InverseFrame);
            for(int i=0;i<bones[k].Length;i++)bones[k][i].localToWorldMatrix=world*binds[k][i].inverse;
        }
        var rests=bones.Select(b=>b.Select(t=>t.localToWorldMatrix).ToArray()).ToArray();
        foreach(var r in records)InstallALBinding(maid,r,r.LastNewV,stage);
        int vertexWrites=cloth.sharedMesh.VertexWrites;float maxSupportError=0,maxFaceError=0;
        var poses=new List<object>();float[] P(Vector3 p)=>new[]{p.x,p.y,p.z};
        foreach(var (angle,jiggle) in new[]{(0f,0f),(.08f,0f),(-.08f,0f),(.25f,0f),(-.25f,0f),(0f,.005f),(0f,-.005f)})
        {
            var rotation=Translation(0,.16f,0)*new Matrix4x4{Rows=NM.CreateRotationX(angle)}*Translation(0,-.16f,0);
            for(int k=0;k<2;k++)for(int i=0;i<bones[k].Length;i++)
            {
                string n=bones[k][i].name.ToLowerInvariant();bool upper=n.Contains("spine")||n.Contains("mune")||n.Contains("neck")||n.Contains("head")||ArmBone(n);
                bones[k][i].localToWorldMatrix=(n.Contains("mune")?Translation(0,0,jiggle):Matrix4x4.identity)*(upper?rotation:Matrix4x4.identity)*rests[k][i];
            }
            UpdateALBindings(maid);
            Check(cloth.sharedMesh.VertexWrites==vertexWrites,"clothing pose never uploads vertices");
            var binding=ActiveBinding(cloth);Check(binding?.ClothingMotion!=null,"inner clothing surface binding installed");
            foreach(var support in binding.ClothingMotion.Supports)
                for(int k=0;k<support.Indices.Length;k++)
                {
                    int index=support.Indices[k];float error=Vector3.Distance(U(support.Posed[k]),SkinPoint(body,records[0].LastNewV[index],index));
                    maxSupportError=Math.Max(maxSupportError,error);Check(error<2e-6f,"clothing support equals rendered body vertex");
                }
            for(int f=0;f<binding.ClothingMotion.Faces.Length;f++)
            {
                var face=binding.ClothingMotion.Faces[f];var support=binding.ClothingMotion.Supports[face.Support];
                foreach(int k in new[]{face.A,face.B,face.C})
                {
                    var from=N(support.Body.Morphed[support.Indices[k]]);Growth.Numerics.Matrix4x4.Invert(binding.ToReference,out var inverse);
                    var actual=Growth.Numerics.Vector3.Transform(from,inverse*binding.ClothingMotion.Transforms[f]);
                    float error=Growth.Numerics.Vector3.Distance(actual,support.Posed[k]);maxFaceError=Math.Max(maxFaceError,error);Check(error<3e-6f,"clothing frame follows all three body corners");
                }
            }
            var virtualWorld=Growth.VirtualAxisMath.Evaluate(c.Axis,c.PelvisBind*MatrixBridge.ToManaged(c.Pelvis.localToWorldMatrix),c.SpineBind*MatrixBridge.ToManaged(c.Spine.localToWorldMatrix),Shape).Transform;
            for(int i=0;i<records[1].LastNewV.Length;i++)
            {
                var r=records[1];var point=r.LastNewV[i];var actual=SkinPoint(cloth,point,i);var native=Vector3.zero;
                foreach(var w in Influences(weights[1][i]))if(w.Weight>0)native+=(bones[1][w.Bone].localToWorldMatrix*binds[1][w.Bone]).MultiplyPoint3x4(point)*w.Weight;
                float alpha=Attachment(r,i,stage);
                if(alpha>0){alpha=c.Material.Sample(Growth.Numerics.Vector3.Transform(N(r.OrigVerts[i]),r.ToReference),alpha);if(r.ThighGuardRestore!=null)alpha*=1-r.ThighGuardRestore[i];}
                if(r.BreastExcluded[i])alpha=0;
                var expected=Vector3.Lerp(native,U(Growth.Numerics.Vector3.Transform(N(point),r.ToReference*virtualWorld)),alpha);
                int face=binding.ClothingMotion.VertexFaces[i];
                if(face>=0)expected=Vector3.Lerp(expected,U(Growth.Numerics.Vector3.Transform(N(point),binding.ClothingMotion.Transforms[face])),binding.ClothingMotion.Blend[i]);
                Check(Vector3.Distance(expected,actual)<3e-6f,"four-slot clothing binding matches surface/native equation");
                var root=cloth.rootBone??cloth.transform;var local=root.worldToLocalMatrix.MultiplyPoint3x4(actual)-cloth.localBounds.center;var ext=cloth.localBounds.size*.5f;
                Check(Math.Abs(local.x)<=ext.x+1e-5f && Math.Abs(local.y)<=ext.y+1e-5f && Math.Abs(local.z)<=ext.z+1e-5f,"surface-driven clothing remains inside culling bounds");
            }
            Vector3 Native(int k,int i)
            {
                var p=Vector3.zero;foreach(var v in Influences(weights[k][i]))if(v.Weight>0)p+=(bones[k][v.Bone].localToWorldMatrix*binds[k][v.Bone]).MultiplyPoint3x4(records[k].OrigVerts[i])*v.Weight;
                return p;
            }
            poses.Add(new{angle,jiggle,original=records[1].OrigVerts.Select((_,i)=>P(Native(1,i))).ToArray(),changed=records[1].LastNewV.Select((v,i)=>P(SkinPoint(cloth,v,i))).ToArray(),bodyOriginal=records[0].OrigVerts.Select((_,i)=>P(Native(0,i))).ToArray(),body=records[0].LastNewV.Select((v,i)=>P(SkinPoint(body,v,i))).ToArray()});
        }
        File.WriteAllText(args[3],JsonSerializer.Serialize(new{poses,triangles=cloth.sharedMesh.triangles,bodyTriangles=body.sharedMesh.GetTriangles(0).Chunk(3).Where(t=>t.All(i=>records[0].TorsoOwnership[i]>0)).SelectMany(t=>t),alpha=records[1].OrigVerts.Select((v,i)=>c.Material.Sample(Growth.Numerics.Vector3.Transform(N(v),records[1].ToReference),Attachment(records[1],i,stage)))}));
        var active=ActiveBinding(cloth);Console.WriteLine($"Surface support: {active.ClothingMotion.Supports.Sum(s=>s.Indices.Length)} vertices, {active.ClothingMotion.Faces.Length} faces, {active.ClothingRecipes.Length} palette recipes; body equality error {maxSupportError:R}, face error {maxFaceError:R}.");
        var ticks=active.ClothingMotion.Supports.Select(s=>s.Tick).ToArray();var timer=System.Diagnostics.Stopwatch.StartNew();for(int repeat=0;repeat<100;repeat++)UpdateALBindings(maid);timer.Stop();Console.WriteLine($"Managed unchanged binding update mean {timer.Elapsed.TotalMilliseconds/100:F3} ms (Unity stubs, not game FPS).");
        Check(ticks.SequenceEqual(active.ClothingMotion.Supports.Select(s=>s.Tick)),"unchanged pose reuses support and face matrices");
        timer.Restart();for(int repeat=0;repeat<100;repeat++){for(int k=0;k<2;k++)for(int i=0;i<bones[k].Length;i++)if(bones[k][i].name.Contains("Spine"))bones[k][i].localToWorldMatrix=Translation(0,0,.002f*MathF.Sin(repeat*.1f))*rests[k][i];UpdateALBindings(maid);}timer.Stop();Console.WriteLine($"Managed changing binding update mean {timer.Elapsed.TotalMilliseconds/100:F3} ms (Unity stubs, not game FPS).");
        ReleaseALBindings(maid);Console.WriteLine("Clothing poses recorded: rest, four torso bends and two breast translations.");
    }
}


