using System.Globalization;
using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using V=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
using NM=System.Numerics.Matrix4x4;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunLegNavelBindingTests(string[] args)
    {
        Shape=new();
        foreach(var line in File.ReadAllLines(Path.Combine(args[4],"parameters.txt")))
        {
            var pair=line.Split('=');if(pair.Length!=2)continue;
            var f=typeof(VtxSettings).GetField(pair[0]);if(f==null)continue;
            f.SetValue(Shape,f.FieldType==typeof(bool)?bool.Parse(pair[1]):(object)float.Parse(pair[1],CultureInfo.InvariantCulture));
        }
        var body=ReadModel(args[2],"body");body.sharedMesh.vertices=ReadPoints(args[5]);
        var cloth=ReadModel(args[3],"onep");cloth.transform.parent=new Transform{name="_SM_dress652_onep"};
        var rows=File.ReadAllLines(Path.Combine(args[4],"vertices.csv")).Skip(1).Select(s=>s.Split(',').Select(v=>v.Trim('"')).ToArray()).ToArray();
        float F(int i,int col)=>float.Parse(rows[i][col],CultureInfo.InvariantCulture);
        cloth.sharedMesh.vertices=Enumerable.Range(0,rows.Length).Select(i=>new Vector3(F(i,5),F(i,6),F(i,7))).ToArray();
        var original=cloth.sharedMesh.vertices;var weights=cloth.sharedMesh.boneWeights;var bones=cloth.bones;var binds=cloth.sharedMesh.bindposes;
        for(int i=0;i<weights.Length;i++)
            for(int j=0;j<4;j++)
            {
                var w=Influences(weights[i])[j];if(w.Weight<=0)continue;
                Check(RigBoneNames.Canonical(bones[w.Bone].name)==RigBoneNames.Canonical(rows[i][30+j*2]) && Math.Abs(w.Weight-F(i,31+j*2))<1e-6f,"actual exported native weights");
            }
        // Independent expected set: this real garment has direct Calf weights and no skirt bones.
        var selected=Enumerable.Range(0,weights.Length).Where(i=>Influences(weights[i]).Any(w=>w.Weight>0 && bones[w.Bone].name.Contains("Calf"))).ToArray();
        Check(selected.Length>958,"includes minority calf weights, not just calf-majority vertices");
        Check(new[]{1256,1809,3622}.All(selected.Contains),"all three observed bad vertices covered");
        Check(selected.All(i=>BuildLowerLegClothExclusion(new[]{weights[i]},bones,1)[0]),"all actual calf-influenced vertices classified");
        var rest=bones.Select(b=>b.localToWorldMatrix).ToArray();var maid=new Maid();var reports=new List<object>();
        foreach(float stage in new[]{.4f,.815f,1f})
        {
            for(int i=0;i<bones.Length;i++)bones[i].localToWorldMatrix=rest[i];
            PrepareALContext(maid,new(){body,cloth},stage);ApplySMR(maid,cloth,stage,MeshMorphClass.OuterCloth,false);
            var rec=FindRecord(maid,cloth);var actual=cloth.sharedMesh.vertices;var boundWeights=cloth.sharedMesh.boneWeights;
            float maxDelta=0,maxPoseError=0;
            foreach(int i in selected)
            {
                maxDelta=Math.Max(maxDelta,Vector3.Distance(actual[i],original[i]));
                Check(actual[i].Equals(original[i]),"excluded vertex retains exact original position after repair");
                Check(Attachment(rec,i,stage)==0,"excluded vertex has zero virtual abdominal attachment");
                Check(boundWeights[i].Equals(weights[i]),"excluded vertex retains exact original bone weights");
            }
            Check(actual.Where((v,i)=>!selected.Contains(i) && Vector3.Distance(v,original[i])>1e-5f).Count()>100,"upper clothing still deforms");
            foreach(float angle in new[]{0f,.7f,-1.2f})
            {
                var bend=new Matrix4x4{Rows=NM.CreateRotationX(angle)*NM.CreateTranslation(.03f,-.08f,.04f)};
                for(int j=0;j<bones.Length;j++)bones[j].localToWorldMatrix=bones[j].name.Contains("Calf")?bend*rest[j]:rest[j];
                UpdateALBindings(maid);
                foreach(int i in selected)
                {
                    var native=Vector3.zero;
                    foreach(var w in Influences(weights[i]))if(w.Weight>0)native+=(bones[w.Bone].localToWorldMatrix*binds[w.Bone]).MultiplyPoint3x4(original[i])*w.Weight;
                    float error=Vector3.Distance(SkinPoint(cloth,actual[i],i),native);maxPoseError=Math.Max(maxPoseError,error);
                    Check(error<1e-7f,"excluded geometry follows original leg animation exactly");
                }
            }
            // A new Apply while bent must select the same excluded set.
            var bent=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=original};
            Check(TryDeformAL(cloth,bent,MeshMorphClass.OuterCloth,stage,out var bentResult,out _),"bent pose rebuild");
            foreach(int i in selected)Check(bentResult[i].Equals(original[i]),"exclusion independent of current pose");
            reports.Add(new{stage,excluded=selected.Length,maxLocalDelta=maxDelta,maxPosedNativeError=maxPoseError});
        }
        for(int i=0;i<bones.Length;i++)bones[i].localToWorldMatrix=rest[i];
        // The same exclusion applies to inner clothes, including their motion bindings.
        PrepareALContext(maid,new(){body,cloth},1);ApplySMR(maid,cloth,1,MeshMorphClass.InnerCloth,false);
        var inner=cloth.sharedMesh.vertices;
        foreach(int i in selected){Check(inner[i].Equals(original[i]),"inner lower-leg geometry frozen");Check(cloth.sharedMesh.boneWeights[i].Equals(weights[i]),"inner motion binding preserves leg weights");}
        ReleaseALBindings(maid);cloth.sharedMesh.vertices=original;
        var storage=NM.CreateScale(-1.3f,.7f,1.8f)*NM.CreateRotationY(.5f)*NM.CreateTranslation(1,2,-3);
        var other=Reexpress(cloth,storage,"onep-reexpressed");var otherOriginal=other.sharedMesh.vertices;
        PrepareALContext(maid,new(){body,other},1);ApplySMR(maid,other,1,MeshMorphClass.OuterCloth,false);
        var otherResult=other.sharedMesh.vertices;
        foreach(int i in selected)Check(otherResult[i].Equals(otherOriginal[i]),"exclusion independent of mesh storage coordinates");
        ReleaseALBindings(maid);

        // Skirt ownership overrides exclusion even when the calf has more weight.
        var thigh=new Transform{name="Bip01 L Thigh"};var calf=new Transform{name="Bip01 L Calf",parent=thigh};
        var skirt=new Transform{name="_yure_skirt_0"};var skirtChild=new Transform{name="clothTip",parent=skirt};
        var footChild=new Transform{name="clothTip",parent=new Transform{name="Bip01 L Foot",parent=calf}};
        var controls=new[]{thigh,calf,skirt,skirtChild,footChild};
        bool Ex(BoneWeight w)=>BuildLowerLegClothExclusion(new[]{w},controls,1)[0];
        Check(Ex(new(){boneIndex0=0,weight0=.999f,boneIndex1=1,weight1=.001f}),"positive minority calf weight excluded");
        Check(!Ex(new(){boneIndex0=0,weight0=1,boneIndex1=1,weight1=0}),"zero calf slot does not exclude thigh");
        Check(!Ex(new(){boneIndex0=1,weight0=.9f,boneIndex1=2,weight1=.1f}),"mixed skirt and calf stays eligible");
        Check(!Ex(new(){boneIndex0=1,weight0=.9f,boneIndex1=3,weight1=.1f}),"skirt descendant ownership stays eligible");
        Check(Ex(new(){boneIndex0=4,weight0=1}),"foot descendants excluded without skirt ownership");

        var navelReport=CheckActualNavelBinding(args[2]);
        var backReport=CheckBackClothingClassification();
        File.WriteAllText(args[1],JsonSerializer.Serialize(new{assertions=tests,garment="dress652_onep",vertices=original.Length,reports,navelReport,backReport},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {tests} lower-leg exclusion, actual navel binding and back clothing assertions.");
    }
    static object CheckActualNavelBinding(string bodyPath)
    {
        Shape=new();var body=ReadModel(bodyPath,"body");var maid=new Maid();
        PrepareALContext(maid,new(){body},1);var accessory=TestAccessory(body,_alWorking);
        var nativeBones=body.bones;var rest=nativeBones.Select(b=>b.localToWorldMatrix).ToArray();
        PrepareALContext(maid,new(){body,accessory},1);
        // Install accessory first so dictionary iteration cannot hide a stale source palette.
        ApplySMR(maid,accessory,1,MeshMorphClass.NavelAccessory,false);
        ApplySMR(maid,body,1,MeshMorphClass.Body,false);UpdateALBindings(maid);
        var ab=ActiveBinding(accessory);var a=ab.Navel;float maxError=0,maxRigidError=0;
        void Validate(string label)
        {
            for(int k=0;k<a.Indices.Length;k++)
            {
                float error=V.Distance(N(Rendered(body,a.Indices[k])),a.PosedVertices[k]);maxError=Math.Max(maxError,error);
                Check(error<3e-6f,label+" uses exact final body skin");
            }
            for(int i=1;i<accessory.sharedMesh.vertexCount;i++)
            {
                float before=Vector3.Distance(accessory.sharedMesh.vertices[0],accessory.sharedMesh.vertices[i]);
                float after=Vector3.Distance(Rendered(accessory,0),Rendered(accessory,i));
                float error=Math.Abs(before-after);maxRigidError=Math.Max(maxRigidError,error);
                Check(error<3e-6f,label+" preserves accessory dimensions");
            }
        }
        Validate("accessory applied before body");
        for(int i=0;i<nativeBones.Length;i++)if(nativeBones[i].name.Contains("Spine"))nativeBones[i].localToWorldMatrix=new Matrix4x4{Rows=NM.CreateRotationX(.8f)}*rest[i];
        UpdateNavelBinding(ab);Validate("accessory updated before body");
        // Give the final source skin a different native/virtual mixture from the analytical one.
        // This models a replaced final skin binding; the attachment must consume the actual
        // arrays installed on the renderer, not repeat Attachment(originalWeights).
        var rec=FindRecord(maid,body);InstallALBinding(maid,rec,body.sharedMesh.vertices,1);
        var bb=ActiveBinding(body);int nativeSpine=Array.FindIndex(bb.Bones,b=>b.name.Contains("Spine"));
        foreach(int i in a.Indices)bb.SkinWeights[i]=new BoneWeight{boneIndex0=nativeSpine,weight0=1};
        bb.Mesh.boneWeights=bb.SkinWeights;
        var changedVertices=body.sharedMesh.vertices;
        foreach(int i in a.Indices)changedVertices[i]+=new Vector3(.003f,-.004f,.007f);
        bb.SkinVertices=changedVertices;body.sharedMesh.vertices=changedVertices;
        int writes=accessory.sharedMesh.VertexWrites;
        UpdateNavelBinding(ab);Validate("replacement final weights and vertices");
        Check(accessory.sharedMesh.VertexWrites==writes,"source rebinding only changes accessory transform");
        Check(a.Weights.All(w=>w.boneIndex0==nativeSpine && w.weight0==1),"attachment adopts the support points' actual final bone weights");
        foreach(float angle in new[]{-.7f,.4f,1.1f})
        {
            for(int i=0;i<nativeBones.Length;i++)nativeBones[i].localToWorldMatrix=nativeBones[i].name.Contains("Spine")?new Matrix4x4{Rows=NM.CreateRotationX(angle)}*rest[i]:rest[i];
            UpdateALBindings(maid);Validate("changed final skin in subsequent poses");
        }
        ReleaseALBindings(maid);
        return new{supportVertices=a.Indices.Length,maxSupportError=maxError,maxRigidError,accessoryFirst=true,replacementSkin=true};
    }
    static object CheckBackClothingClassification()
    {
        RuntimeClassification=true;Shape=new();var (maid,body,cloth)=LiveFixture();
        cloth.sharedMesh.name="accSenaka";cloth.gameObject.name="accSenaka";
        Check(ClassifyMesh(cloth)==MeshMorphClass.OuterCloth,"game back slot enters ordinary outer cloth classification");
        var original=cloth.sharedMesh.vertices;ApplyProgress(maid,1);
        Check(ActiveBinding(cloth)!=null,"back clothing included in full Apply");
        Check(cloth.sharedMesh.vertices.Where((v,i)=>Vector3.Distance(v,original[i])>1e-5f).Count()>100,"back slot geometry participates in deformation");
        var current=cloth.sharedMesh.vertices;int writes=cloth.sharedMesh.VertexWrites;
        ApplyProgress(maid,1);Check(cloth.sharedMesh.VertexWrites==writes,"back slot reuses unchanged calculation");
        cloth.enabled=false;EnsureMonitor(maid).RequestVisibilityApply(maid.GetHashCode());Tick(maid,3);
        cloth.enabled=true;EnsureMonitor(maid).RequestVisibilityApply(maid.GetHashCode());Tick(maid,3);
        Same(current,cloth.sharedMesh.vertices,"back hide/show remains stable");
        foreach(var entry in new[]{("accheso",MeshMorphClass.NavelAccessory),("body",MeshMorphClass.Body),("bra",MeshMorphClass.InnerCloth),("wear",MeshMorphClass.OuterCloth),("accSenaka",MeshMorphClass.OuterCloth),("accShippo",MeshMorphClass.Ignore),("acchat",MeshMorphClass.Ignore),("face",MeshMorphClass.Ignore)})
        {cloth.sharedMesh.name=entry.Item1;cloth.gameObject.name=entry.Item1;Check(ClassifyMesh(cloth)==entry.Item2,"category control "+entry.Item1);}
        cloth.sharedMesh.name="";cloth.gameObject.name="accSenaka";
        Check(ClassifyMesh(cloth)==MeshMorphClass.OuterCloth,"slot recognized with unnamed mesh");
        ReleaseALBindings(maid);RuntimeClassification=false;
        return new{slot="accSenaka",fullApply=true,repeatApplyCache=true,hideShow=true,otherCategoriesUnchanged=true};
    }
}
