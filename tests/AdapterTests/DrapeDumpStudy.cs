using System.Globalization;
using System.Text.Json;
using UnityEngine;
using NV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static Vector3[] ReadPoints(string path)=>File.ReadAllLines(path).Select(s=>s.Split(',').Select(x=>float.Parse(x,CultureInfo.InvariantCulture)).ToArray()).Select(a=>new Vector3(a[0],a[1],a[2])).ToArray();
    static void RunDrapeDumpStudy(string[] args)
    {
        string input=args[1],output=args[2],models=args[3];Directory.CreateDirectory(output);
        var settings=JsonDocument.Parse(File.ReadAllText(Path.Combine(input,"inputs.json"))).RootElement;
        foreach(var entry in settings.EnumerateArray())
        {
            string name=entry.GetProperty("model").GetString();float stage=entry.GetProperty("stage").GetSingle();
            var kind=entry.TryGetProperty("kind",out var kindEntry)&&kindEntry.GetString()=="inner"?MeshMorphClass.InnerCloth:MeshMorphClass.OuterCloth;
            var body=ReadModel(Path.Combine(models,"LOmobchara_extra_v1_beta.model"),"body");body.sharedMesh.vertices=ReadPoints(Path.Combine(input,name+"-body.csv"));
            var cloth=ReadModel(Path.Combine(models,name+".model"),name);cloth.sharedMesh.vertices=ReadPoints(Path.Combine(input,name+"-original.csv"));
            var original=cloth.sharedMesh.vertices;var weights=cloth.sharedMesh.boneWeights;var bones=cloth.bones;var binds=cloth.sharedMesh.bindposes;
            var maid=new Maid();PrepareALContext(maid,new(){body,cloth},stage);var c=_alWorking;Check(c!=null,"dump model context");
            var rec=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=original};
            TryDeformAL(cloth,rec,kind,stage,out var residual,out _);
            var visual=rec.Skirt?.VisualVertices??residual;
            var directRec=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=original};
            var savedNames=bones.Select(b=>b.name).ToArray();
            for(int k=0;k<bones.Length;k++)bones[k].name=bones[k].name.Replace("_yure_skirt_","_study_drape_");
            TryDeformAL(cloth,directRec,MeshMorphClass.OuterCloth,stage,out var direct,out _);
            for(int k=0;k<bones.Length;k++)bones[k].name=savedNames[k];
            var br=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=body.sharedMesh.vertices};
            TryDeformAL(body,br,MeshMorphClass.Body,stage,out var deformedBody,out _);
            NV ToLocal(Vector3 p,MeshRecord record)=>NV.Transform(N(p),record.ToReference*c.InverseFrame);
            float[] P(NV p)=>new[]{p.X,p.Y,p.Z};
            float[][] Points(Vector3[] v,MeshRecord r)=>v.Select(p=>P(ToLocal(p,r))).ToArray();
            var rows=new List<object>();
            var allBody=c.Surface.Meshes.SelectMany(m=>m.Original.Select((v,i)=>new{point=v,excluded=m.BreastExcluded[i]})).ToArray();
            for(int i=0;i<original.Length;i++)
            {
                var ws=Influences(weights[i]);float skirt=ws.Where(w=>w.Weight>0&&IsDrapeBone(bones[w.Bone])).Sum(w=>w.Weight);
                var reference=U(NV.Transform(N(original[i]),rec.ToReference));var local=ToLocal(original[i],rec);
                bool hit=TryGetBodySurfaceClothTarget(c.Surface,reference,Shape.ClothOffset,c.Frame,out var target);
                TryFindNearestBodySurfaceTriangle(c.Surface,reference,out var surface);
                float footprint=Growth.BellyShape.ClothingFootprint(local,c.Frame.Profile,stage,Shape);
                var closest=NV.Transform(N(surface.Closest),c.InverseFrame);
                TryFindNearestBodyAnchorPoint(c.Surface,reference,false,out var oldAnchor);
                bool anchorExcluded=c.Surface.Meshes[oldAnchor.MeshIndex].BreastExcluded[oldAnchor.VertexIndex];
                int allNearest=0;float allDistance=float.MaxValue;
                for(int k=0;k<allBody.Length;k++){float ds=(allBody[k].point-reference).sqrMagnitude;if(ds<allDistance){allDistance=ds;allNearest=k;}}
                bool breastSurfaceHit=TryGetBreastClothingTarget(c.Surface,reference,c.Frame.Profile.Span*.2f,Shape.ClothOffset,out var breastTarget,out var breastSurfaceExcluded);
                rows.Add(new{i,excluded=rec.BreastExcluded[i],skirtWeight=skirt,alpha=Attachment(rec,i,stage),ownership=rec.TorsoOwnership[i],
                    breastSurfaceHit,breastSurfaceExcluded,breastTarget=P(NV.Transform(N(breastTarget),c.InverseFrame)),
                    anchorExcluded,allNearestExcluded=allBody[allNearest].excluded,allNearestPoint=P(NV.Transform(N(allBody[allNearest].point),c.InverseFrame)),
                    footprint,contactFootprint=Growth.BellyShape.ClothingFootprint(closest,c.Frame.Profile,stage,Shape),hit,
                    target=P(NV.Transform(N(target),c.InverseFrame)),closest=P(closest),contactDistance=Mathf.Sqrt(surface.DistanceSq),
                    weights=ws.Select((w,k)=>new{bone=bones[w.Bone].name,index=w.Bone,weight=w.Weight,free=rec.Skirt?.Free[i][k]??0}).ToArray()});
            }
            var boneRows=bones.Select((b,i)=>new{name=b.name,index=i,parent=Array.IndexOf(bones,b.parent),position=P(ToLocal(binds[i].inverse.MultiplyPoint3x4(Vector3.zero),rec)),
                controlled=rec.Skirt?.Controlled[i]??false,released=rec.Skirt?.Released[i]??false,offset=P(rec.Skirt!=null?NV.TransformNormal(rec.Skirt.Offsets[i],c.InverseFrame):NV.Zero)}).ToArray();
            var meshToWorld=MatrixBridge.ToUnity(rec.ToReference*c.PelvisBind*MatrixBridge.ToManaged(c.Pelvis.localToWorldMatrix));
            for(int i=0;i<bones.Length;i++)bones[i].localToWorldMatrix=meshToWorld*binds[i].inverse;
            cloth.sharedMesh.vertices=residual;InstallALBinding(maid,rec,residual,stage);
            var rendered=residual.Select((v,i)=>meshToWorld.inverse.MultiplyPoint3x4(SkinPoint(cloth,v,i))).ToArray();
            float error=rendered.Select((v,i)=>Vector3.Distance(v,visual[i])).Max();Check(error<2e-5f,"dump rest compensation");ReleaseALBindings(maid);
            var bodyTorsoTriangles=body.sharedMesh.GetTriangles(0).Chunk(3).Where(t=>t.All(i=>br.TorsoOwnership[i]>0)).SelectMany(t=>t).ToArray();
            File.WriteAllText(Path.Combine(output,name+".json"),JsonSerializer.Serialize(new{model=name,stage,span=c.Frame.Profile.Span,navel=c.Frame.Profile.Navel,
                original=Points(original,rec),changed=Points(visual,rec),direct=Points(direct,directRec),residual=Points(residual,rec),triangles=cloth.sharedMesh.triangles,
                body=Points(deformedBody,br),bodyOriginal=Points(br.OrigVerts,br),bodyTriangles=body.sharedMesh.GetTriangles(0),bodyTorsoTriangles,rows,bones=boneRows}));
            Console.WriteLine($"{name}: vertices={original.Length}, skirt bones={bones.Count(IsDrapeBone)}, released={rec.Skirt?.Released.Count(x=>x)??0}, zero skirt-weight={rows.Count-original.Select((_,i)=>i).Count(i=>Influences(weights[i]).Any(w=>w.Weight>0&&IsDrapeBone(bones[w.Bone])))}, compensation={error:R}");
        }
    }
}
