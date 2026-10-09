using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunClothingStudy(string[] args)
    {
        string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        var body=ReadModel(args[2],"body");
        foreach(string path in args.Skip(3))
        {
            var garment=ReadModel(path,Path.GetFileNameWithoutExtension(path));var maid=new Maid();
            PrepareALContext(maid,new(){body,garment},1);Check(_alWorking!=null,"model calibration");
            var ctx=_alWorking;
            var br=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=body.sharedMesh.vertices};
            TryDeformAL(body,br,MeshMorphClass.Body,1,out var bv,out _);
            var r=new MeshRecord{SMR=garment,Mesh=garment.sharedMesh,OrigVerts=garment.sharedMesh.vertices};
            TryDeformAL(garment,r,MeshMorphClass.OuterCloth,1,out var cv,out _);
            if(r.Skirt!=null)
            {
                var meshToWorld=MatrixBridge.ToUnity(r.ToReference*ctx.PelvisBind*MatrixBridge.ToManaged(ctx.Pelvis.localToWorldMatrix));
                for(int i=0;i<garment.bones.Length;i++)garment.bones[i].localToWorldMatrix=meshToWorld*garment.sharedMesh.bindposes[i].inverse;
                garment.sharedMesh.vertices=cv;InstallALBinding(maid,r,cv,1);
                var rendered=cv.Select((v,i)=>meshToWorld.inverse.MultiplyPoint3x4(SkinPoint(garment,v,i))).ToArray();
                float error=rendered.Select((v,i)=>Vector3.Distance(v,r.Skirt.VisualVertices[i])).Max();
                Console.WriteLine($"Compensation max error={error:R}");
                Check(error<2e-5f,"rest bone compensation");
                Console.WriteLine($"Skirt plan: released={r.Skirt.Released.Count(v=>v)}; rendered/residual error={error:R}");
                ReleaseALBindings(maid);cv=rendered;
            }
            Vector3[] Frame(Vector3[] v,MeshRecord rec)=>v.Select(p=>U(Growth.Numerics.Vector3.Transform(N(p),rec.ToReference*ctx.InverseFrame))).ToArray();
            float[][] Points(Vector3[] v)=>v.Select(p=>new[]{p.x,p.y,p.z}).ToArray();
            var originals=Frame(r.OrigVerts,r);var changed=Frame(cv,r);
            var boneRows=new List<object>();
            for(int b=0;b<garment.bones.Length;b++)
            {
                var bone=garment.bones[b];if(!bone.name.ToLowerInvariant().Contains("skirt"))continue;
                var rest=U(Growth.Numerics.Vector3.Transform(N(garment.sharedMesh.bindposes[b].inverse.MultiplyPoint3x4(Vector3.zero)),r.ToReference*ctx.InverseFrame));
                var delta=Vector3.zero;float total=0;
                for(int i=0;i<cv.Length;i++)foreach(var w in Influences(garment.sharedMesh.boneWeights[i]))if(w.Bone==b && w.Weight>0)
                {
                    float weight=w.Weight/(Mathf.Pow(ctx.Frame.Profile.Span*.03f,2)+(originals[i]-rest).sqrMagnitude);
                    delta+=(changed[i]-originals[i])*weight;total+=weight;
                }
                if(total>0)delta/=total;
                boneRows.Add(new{name=bone.name,position=new[]{rest.x,rest.y,rest.z},delta=new[]{delta.x,delta.y,delta.z},weight=total});
            }
            File.WriteAllText(Path.Combine(output,garment.name+".json"),JsonSerializer.Serialize(new{model=Path.GetFileName(path),span=ctx.Frame.Profile.Span,navel=ctx.Frame.Profile.Navel,body=Points(Frame(bv,br)),bodyOriginal=Points(Frame(br.OrigVerts,br)),bodyTriangles=body.sharedMesh.GetTriangles(0),original=Points(originals),changed=Points(changed),triangles=garment.sharedMesh.triangles,bones=boneRows},new JsonSerializerOptions{WriteIndented=false}));
            Console.WriteLine($"{garment.name}: {cv.Length} vertices; {boneRows.Count} skirt bones; max move={changed.Select((v,i)=>(v-originals[i]).magnitude).Max():F5}; span={ctx.Frame.Profile.Span:F5}");
        }
    }
}
