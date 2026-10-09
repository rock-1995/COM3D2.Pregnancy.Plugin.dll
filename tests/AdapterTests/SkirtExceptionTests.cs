using System.Globalization;
using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunSkirtExceptionTests(string[] args)
    {
        var rows=File.ReadAllLines(args[4]).Skip(1).Select(l=>l.Split(',').Select(s=>s.Trim('"')).ToArray()).ToArray();
        float F(string s)=>float.Parse(s,CultureInfo.InvariantCulture);
        var reports=new List<object>();
        foreach(var path in args.Skip(3).Take(1))
        {
            Shape=new();var body=ReadModel(args[2],"body");var cloth=ReadModel(path,"");var maid=new Maid();
            Check(cloth.sharedMesh.vertexCount==rows.Length,"selected model matches exported 5502 vertices");
            for(int i=0;i<rows.Length;i++)
            {
                var influences=Influences(cloth.sharedMesh.boneWeights[i]);
                for(int k=0;k<4;k++)if(influences[k].Weight>0)
                {Check(RigBoneNames.Canonical(cloth.bones[influences[k].Bone].name)==RigBoneNames.Canonical(rows[i][30+2*k]),$"exported native bone identity vertex={i} slot={k} model={cloth.bones[influences[k].Bone].name} export={rows[i][30+2*k]}");Check(Math.Abs(influences[k].Weight-F(rows[i][31+2*k]))<1e-6f,"exported weight identity");}
            }
            cloth.sharedMesh.vertices=rows.Select(r=>new Vector3(F(r[5]),F(r[6]),F(r[7]))).ToArray();
            var original=cloth.sharedMesh.vertices;var originalNames=cloth.bones.Select(b=>b.name).ToArray();
            int exportedSkirtInfluences=rows.Sum(r=>Enumerable.Range(0,4).Count(k=>F(r[31+2*k])>0 && r[30+2*k].ToLowerInvariant().Contains("skirt")));
            Console.WriteLine($"Exported skirt bone influences: {exportedSkirtInfluences}; model skirt bones: {cloth.bones.Count(IsDrapeBone)}.");
            cloth.transform.parent=new Transform{name="_SM_dress652_onep"};
            foreach(float stage in new[]{.4f,.815f,1f})
            {
                PrepareALContext(maid,new(){body,cloth},stage);
                var rec=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=original};
                Check(TryDeformAL(cloth,rec,MeshMorphClass.OuterCloth,stage,out var actual,out _),"exception deforms");
                Check(rec.Skirt==null,"exact model bypasses skirt solver");
                Check(cloth.bones.Select(b=>b.name).SequenceEqual(originalNames),"native skirt bones preserved");
                cloth.transform.parent.name="_SM_reference_ordinary";
                for(int i=0;i<cloth.bones.Length;i++)cloth.bones[i].name=cloth.bones[i].name.Replace("_yure_skirt_","_ordinary_reference_");
                var directRec=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=original};
                Check(TryDeformAL(cloth,directRec,MeshMorphClass.OuterCloth,stage,out var expected,out _),"ordinary reference deforms");
                Same(actual,expected,"exception equals ordinary clothing");
                Check((rec.ClothingMotion?.Faces.Length??0)==(directRec.ClothingMotion?.Faces.Length??0),"exception receives ordinary motion support");
                for(int i=0;i<cloth.bones.Length;i++)cloth.bones[i].name=originalNames[i];
                cloth.transform.parent.name="_SM_dress652_onep";
                cloth.sharedMesh.vertices=actual;InstallALBinding(maid,rec,actual,stage);
                var binding=ActiveBinding(cloth);Check(binding!=null && binding.Skirt==null && binding.SkirtRecipes.Length==0,"no skirt palette recipes installed");
                for(int pose=0;pose<3;pose++){UpdateALBindings(maid);Check(Rendered(cloth).All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"ordinary rendered output finite");}
                ReleaseALBindings(maid);cloth.sharedMesh.vertices=original;
                reports.Add(new{stage,vertices=actual.Length,skirtSolver=false,ordinaryFaces=rec.ClothingMotion?.Faces.Length??0,maxOrdinaryError=actual.Select((v,i)=>Vector3.Distance(v,expected[i])).Max()});
            }
            foreach(string lookalike in new[]{"_SM_dress652_onep_extra","_SM_dress652_onep2","dress652_onep","_SM_dress218_skrt"})
            {
                cloth.transform.parent.name=lookalike;PrepareALContext(maid,new(){body,cloth},1);
                var rec=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=original};
                Check(!UseOrdinaryClothing(cloth),"lookalike is not exempt: "+lookalike);
            }
            // This tight model has no skirt-bone influences in the export.
            // Exercise the bypass itself with an actual draped rig too.
            var drape=ReadModel(args[5],"dress218_skrt");
            PrepareALContext(maid,new(){body,drape},1);
            var baseline=new MeshRecord{SMR=drape,Mesh=drape.sharedMesh,OrigVerts=drape.sharedMesh.vertices};
            Check(TryDeformAL(drape,baseline,MeshMorphClass.OuterCloth,1,out _,out _) && baseline.Skirt!=null,"ordinary skirt solver remains enabled");
            drape.transform.parent=new Transform{name="_SM_dress652_onep"};
            var exempt=new MeshRecord{SMR=drape,Mesh=drape.sharedMesh,OrigVerts=drape.sharedMesh.vertices};
            Check(TryDeformAL(drape,exempt,MeshMorphClass.OuterCloth,1,out _,out _) && exempt.Skirt==null,"exception bypasses even a draped rig");
            reports.Add(new{exportedSkirtInfluences,nativeModelSkirtBones=cloth.bones.Count(IsDrapeBone),ordinarySkirtControl=true});
        }
        File.WriteAllText(args[1],JsonSerializer.Serialize(new{assertions=tests,model="_SM_dress652_onep",exportVertices=rows.Length,reports},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {tests} exact skirt exception assertions.");
    }
}


