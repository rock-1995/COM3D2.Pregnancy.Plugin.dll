using System.Globalization;
using System.Text.Json;
using UnityEngine;
using NM=System.Numerics.Matrix4x4;
using NV=System.Numerics.Vector3;
using COM3D2.Pregnancy.Plugin.Growth;

namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    // Read geometry only; do not execute game code or inspect installed plugins.
    // Format references: CM3D2user/Blender-CM3D2-Converter model_import.py and
    // ShinHogera/CM3D2.SceneCapture.Plugin AssetLoader.cs (column-major bind poses).
    static SkinnedMeshRenderer ReadModel(string path,string name)
    {
        using var reader=new BinaryReader(File.OpenRead(path));
        if(reader.ReadString()!="CM3D2_MESH")throw new Exception("Invalid model header");
        int version=reader.ReadInt32(); reader.ReadString(); reader.ReadString();
        int boneCount=reader.ReadInt32();
        var hierarchy=new Transform[boneCount];
        for(int i=0;i<boneCount;i++){hierarchy[i]=new Transform{name=reader.ReadString()};reader.ReadByte();}
        for(int i=0;i<boneCount;i++){int p=reader.ReadInt32();if(p>=0)hierarchy[i].parent=hierarchy[p];}
        for(int i=0;i<boneCount;i++)
        {
            for(int j=0;j<7;j++)reader.ReadSingle();
            if(version>=2001 && reader.ReadBoolean())for(int j=0;j<3;j++)reader.ReadSingle();
        }
        int count=reader.ReadInt32(),submeshes=reader.ReadInt32(),localBones=reader.ReadInt32();
        if(count<0 || count>200000 || localBones<1 || localBones>1000)throw new Exception("Invalid model counts");
        var names=Enumerable.Range(0,localBones).Select(_=>reader.ReadString()).ToArray();
        var poses=new Matrix4x4[localBones];
        for(int i=0;i<localBones;i++)for(int c=0;c<4;c++)for(int r=0;r<4;r++)poses[i][r,c]=reader.ReadSingle();
        var vertices=new Vector3[count];var normals=new Vector3[count];
        for(int i=0;i<count;i++)
        {
            vertices[i]=new(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            normals[i]=new(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            reader.ReadSingle();reader.ReadSingle();
        }
        int tangents=reader.ReadInt32();reader.BaseStream.Seek(tangents*16,SeekOrigin.Current);
        var weights=new BoneWeight[count];
        for(int i=0;i<count;i++)weights[i]=new(){boneIndex0=reader.ReadUInt16(),boneIndex1=reader.ReadUInt16(),boneIndex2=reader.ReadUInt16(),boneIndex3=reader.ReadUInt16(),weight0=reader.ReadSingle(),weight1=reader.ReadSingle(),weight2=reader.ReadSingle(),weight3=reader.ReadSingle()};
        var triangles=new List<int>();
        var subs=new int[submeshes][];
        for(int i=0;i<submeshes;i++){int n=reader.ReadInt32();subs[i]=new int[n];for(int j=0;j<n;j++){int v=reader.ReadUInt16();triangles.Add(v);subs[i][j]=v;}}
        var materials=new Material[reader.ReadInt32()];
        for(int i=0;i<materials.Length;i++)
        {
            materials[i]=new Material{name=reader.ReadString()};reader.ReadString();reader.ReadString();
            while(true){string tag=reader.ReadString();if(tag=="end")break;reader.ReadString();
                if(tag=="tex"){string type=reader.ReadString();if(type=="tex2d"){reader.ReadString();reader.ReadString();reader.BaseStream.Seek(16,SeekOrigin.Current);}}
                else if(tag=="col"||tag=="vec")reader.BaseStream.Seek(16,SeekOrigin.Current);
                else if(tag=="f")reader.ReadSingle();else throw new Exception("Unknown material property "+tag);
            }
        }
        foreach(var weight in weights)foreach(var w in Influences(weight))
            Check(w.Weight>=0 && w.Weight<=1.001f && (w.Weight==0 || w.Bone<localBones),"valid model weight");
        var hierarchyMap=hierarchy.ToDictionary(b=>b.name);
        var bones=names.Select((n,i)=>hierarchyMap.TryGetValue(n,out var b)?b:new Transform{name=n}).ToArray();
        for(int i=0;i<bones.Length;i++)bones[i].localToWorldMatrix=poses[i].inverse;
        var mesh=new Mesh{name=name,vertices=vertices,normals=normals,boneWeights=weights,bindposes=poses,triangles=triangles.ToArray(),submeshes=subs};
        return new SkinnedMeshRenderer{sharedMesh=mesh,bones=bones,rootBone=bones[0],sharedMaterials=materials};
    }
    static bool ArmBone(string name)=>new[]{"clavicle","upperarm","forearm","hand","finger","uppertwist","foretwist","kata_"}.Any(name.Contains);
    static SkinnedMeshRenderer Reexpress(SkinnedMeshRenderer source,NM coordinates,string name)
    {
        NM.Invert(coordinates,out var inverse);
        var mesh=source.sharedMesh;
        return new SkinnedMeshRenderer{sharedMesh=new Mesh{name=name,
            vertices=mesh.vertices.Select(v=>(Vector3)NV.Transform(v,coordinates)).ToArray(),
            normals=new Vector3[mesh.vertexCount],triangles=(int[])mesh.triangles.Clone(),submeshes=mesh.submeshes,boneWeights=(BoneWeight[])mesh.boneWeights.Clone(),
            bindposes=mesh.bindposes.Select(b=>new Matrix4x4{Rows=inverse*b.Rows}).ToArray()},
            bones=source.bones,rootBone=source.rootBone,sharedMaterials=source.sharedMaterials,transform=new Transform{localToWorldMatrix=new Matrix4x4{Rows=inverse}}};
    }
    static void RunRegionSimulation(string[] args)
    {
        var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        var body=ReadModel(args[2],"body-real");var maid=new Maid();
        var original=body.sharedMesh.vertices;var weights=body.sharedMesh.boneWeights;var bones=body.bones;
        Console.WriteLine("Model: "+Path.GetFileName(args[2])+" vertices="+original.Length+" bones="+bones.Length);
        File.WriteAllLines(Path.Combine(output,"bones.txt"),bones.Select(b=>b.name));
        PrepareALContext(maid,new(){body},1);Check(_alWorking!=null,"actual model calibration");
        var context=_alWorking;
        Console.WriteLine($"Frame: right={context.Frame.Right.x},{context.Frame.Right.y},{context.Frame.Right.z}; up={context.Frame.Up.x},{context.Frame.Up.y},{context.Frame.Up.z}; forward={context.Frame.Fwd.x},{context.Frame.Fwd.y},{context.Frame.Fwd.z}; span={context.Frame.Profile.Span}");
        var record=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=original};
        Check(TryDeformAL(body,record,MeshMorphClass.Body,1,out var changed,out _),"actual model deformation");
        float maxArm=0;int armCount=0,movedArm=0,attachedArm=0;float maxAlpha=0;
        using(var csv=new StreamWriter(Path.Combine(output,"vertices.csv")))
        {
            csv.WriteLine("x,y,z,dx,dy,dz,armWeight,bellyWeight,alpha");
            for(int i=0;i<original.Length;i++)
            {
                float arm=BoneSum(weights[i],bones,ArmBone),belly=BoneSum(weights[i],bones,IsALBellyBone);
                var o=Growth.Numerics.Vector3.Transform(N(original[i]),record.ToReference*context.InverseFrame);
                var n=Growth.Numerics.Vector3.Transform(N(changed[i]),record.ToReference*context.InverseFrame);
                float distance=(changed[i]-original[i]).magnitude,alpha=Attachment(record,i,1);
                if(arm>.999f){armCount++;if(distance>1e-7f)movedArm++;if(alpha>1e-7f)attachedArm++;maxArm=Math.Max(maxArm,distance);maxAlpha=Math.Max(maxAlpha,alpha);}
                csv.WriteLine(FormattableString.Invariant($"{o.X:R},{o.Y:R},{o.Z:R},{n.X-o.X:R},{n.Y-o.Y:R},{n.Z-o.Z:R},{arm:R},{belly:R},{alpha:R}"));
            }
        }
        var report=new{model=Path.GetFileName(args[2]),vertices=original.Length,armCount,movedArm,attachedArm,maxArm,maxAlpha,span=context.Frame.Profile.Span};
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(report));
        if(args.Contains("--coordinates"))
        {
            foreach(var (name,matrix) in new[]{("rotation",NM.CreateRotationX(.8f)*NM.CreateRotationY(-1.2f)),("translation",NM.CreateTranslation(2,-3,4)),("units",NM.CreateScale(100)),("mirror",NM.CreateScale(-1,1,1)),("nonuniform",NM.CreateScale(.7f,1.8f,1.1f)*NM.CreateRotationZ(.6f))})
            {
                var transformed=Reexpress(body,matrix,"body-"+name);var m=new Maid();
                PrepareALContext(m,new(){transformed},1);Check(_alWorking!=null,"reexpressed calibration "+name);
                var r=new MeshRecord{SMR=transformed,Mesh=transformed.sharedMesh,OrigVerts=transformed.sharedMesh.vertices};
                Check(TryDeformAL(transformed,r,MeshMorphClass.Body,1,out var result,out _),"reexpressed shape "+name);
                NM.Invert(matrix,out var inverse);
                float error=result.Select((v,i)=>Vector3.Distance(NV.Transform(v,inverse),changed[i])).Max();
                Console.WriteLine($"Coordinate reexpression {name}: max equivalent-rest error={error:R}");
                if(args.Contains("--strict"))Check(error<2e-5f,"coordinate independence "+name);
            }
        }
        if(args.Contains("--strict"))Check(movedArm==0 && attachedArm==0,"hands and arms unchanged and not rebound");
        if(args.Contains("--suite"))
        {
            TestStagesAndClothing(body,output,args);
            TestCoordinateSkinning(body);
            TestProportionsAndOverlappingHands();
            Console.WriteLine($"PASS: {tests} region/model/coordinate/pose assertions.");
        }
    }

    static Vector3 SkinPoint(SkinnedMeshRenderer mesh,Vector3 point,int i)
    {
        var value=Vector3.zero;
        foreach(var w in Influences(mesh.sharedMesh.boneWeights[i]))if(w.Weight>0)
            value+=(mesh.bones[w.Bone].localToWorldMatrix*mesh.sharedMesh.bindposes[w.Bone]).MultiplyPoint3x4(point)*w.Weight;
        return value;
    }
    static void AssertArmsUnchanged(MeshRecord record,string label,bool requireArms=true)
    {
        var native=NativeWeights(record.SMR);var bones=NativeBones(record.SMR);
        int checkedArms=0;
        for(int i=0;i<record.OrigVerts.Length;i++)if(BoneSum(native[i],bones,ArmBone)>.999f)
        {
            Check((record.LastNewV[i]-record.OrigVerts[i]).sqrMagnitude==0,label+" static hand/arm identity");
            Check(Attachment(record,i,1)==0,label+" no virtual hand/arm attachment");
            Check(record.SMR.sharedMesh.boneWeights[i].Equals(native[i]),label+" original arm weights preserved");
            checkedArms++;
        }
        if(requireArms)Check(checkedArms>0,label+" has arm test coverage");
    }
    static void TestStagesAndClothing(SkinnedMeshRenderer body,string output,string[] args)
    {
        var maid=new Maid();var cloth=Reexpress(body,NM.CreateScale(.8f,1.3f,.6f)*NM.CreateRotationY(.9f)*NM.CreateTranslation(.4f,-.8f,.2f),"wear-full-body");
        var renderers=new List<SkinnedMeshRenderer>{cloth,body};
        var ci=Array.IndexOf(args,"--cloth");
        SkinnedMeshRenderer garment=ci<0?null:ReadModel(args[ci+1],"wear-real");
        if(garment!=null)
        {
            renderers.Insert(0,garment);
            Console.WriteLine($"Actual garment: {Path.GetFileName(args[ci+1])}; vertices={garment.sharedMesh.vertexCount}; pure-arm vertices={garment.sharedMesh.boneWeights.Count(w=>BoneSum(w,garment.bones,ArmBone)>.999f)}.");
        }
        foreach(bool upperFilter in new[]{false,true})
        foreach(float stage in new[]{0f,1f/3,4f/9,5f/9,1f})
        {
            Shape.UpperBoneFilterEnabled=upperFilter;
            PrepareALContext(maid,renderers,stage);Check(_alWorking!=null,"stage calibration");
            foreach(var renderer in renderers)
            {
                Check(_alWorking.Maps.ContainsKey(renderer),"clothing rest map available: "+renderer.name);
                ApplySMR(maid,renderer,stage,renderer==body?MeshMorphClass.Body:MeshMorphClass.OuterCloth,false);
                var r=FindRecord(maid,renderer);AssertArmsUnchanged(r,renderer.name+" stage "+stage,renderer!=garment);
                Check(r.LastNewV.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"finite shape");
                if(stage==0)Check(r.LastNewV.Select((v,i)=>(v-r.OrigVerts[i]).sqrMagnitude).Max()==0,"stage zero exact identity");
            }
            if(!upperFilter)
            {
                var r=FindRecord(maid,body);var toLocal=r.ToReference*r.GrowthContext.InverseFrame;
                using var csv=new StreamWriter(Path.Combine(output,$"stage-{stage:F4}.csv"));
                csv.WriteLine("x,y,z,dx,dy,dz");
                for(int i=0;i<r.OrigVerts.Length;i++)
                {
                    var a=Growth.Numerics.Vector3.Transform(N(r.OrigVerts[i]),toLocal);
                    var b=Growth.Numerics.Vector3.Transform(N(r.LastNewV[i]),toLocal);
                    csv.WriteLine(FormattableString.Invariant($"{a.X:R},{a.Y:R},{a.Z:R},{b.X-a.X:R},{b.Y-a.Y:R},{b.Z-a.Z:R}"));
                }
            }
            Console.WriteLine($"PASS: body + full-body clothing{(garment==null?"":" + actual garment")}, stage={stage:F4}, upperFilter={upperFilter}; all pure arm vertices fixed.");
        }
        Shape.UpperBoneFilterEnabled=false;
        // Pose native bones, including moving the arms in front of the abdomen.
        var allBones=renderers.SelectMany(NativeBones).Distinct().ToArray();
        var rest=allBones.Select(b=>b.localToWorldMatrix).ToArray();
        foreach(float angle in new[]{0f,.6f,-.9f})
        {
            for(int i=0;i<allBones.Length;i++)
            {
                var name=allBones[i].name.ToLowerInvariant();
                NM motion=ArmBone(name)?NM.CreateRotationY(angle)*NM.CreateTranslation(.1f,0,-.2f):name.Contains("spine")?NM.CreateRotationX(angle):NM.Identity;
                allBones[i].localToWorldMatrix=new(){Rows=rest[i].Rows*motion*NM.CreateRotationY(.7f)*NM.CreateTranslation(4,2,-3)};
            }
            UpdateALBindings(maid);
            foreach(var renderer in renderers)
            {
                var r=FindRecord(maid,renderer);var nativeWeights=NativeWeights(renderer);var nativeBinds=NativeBinds(renderer);var nativeBones=NativeBones(renderer);
                for(int i=0;i<r.OrigVerts.Length;i++)if(BoneSum(nativeWeights[i],nativeBones,ArmBone)>.999f)
                {
                    var expected=Vector3.zero;
                    foreach(var w in Influences(nativeWeights[i]))if(w.Weight>0)expected+=(nativeBones[w.Bone].localToWorldMatrix*nativeBinds[w.Bone]).MultiplyPoint3x4(r.OrigVerts[i])*w.Weight;
                    Check((SkinPoint(renderer,r.LastNewV[i],i)-expected).magnitude<1e-6f,"posed arms retain native motion, including in front of abdomen");
                }
            }
        }
        for(int i=0;i<allBones.Length;i++)allBones[i].localToWorldMatrix=rest[i];
        ReleaseALBindings(maid);
        // Return model geometry to the unmodified rest mesh for the independent tests.
        foreach(var renderer in renderers)renderer.sharedMesh.vertices=FindRecord(maid,renderer).OrigVerts;
        Console.WriteLine("PASS: native arm/hand animation preserved in three poses and a rotated/translated world.");
    }
    static void TestCoordinateSkinning(SkinnedMeshRenderer body)
    {
        var maid=new Maid();PrepareALContext(maid,new(){body},1);
        var baseline=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=body.sharedMesh.vertices};
        Check(TryDeformAL(body,baseline,MeshMorphClass.Body,1,out var baselineVertices,out _),"baseline pose shape");
        InstallALBinding(maid,baseline,baselineVertices,1);
        var nativeBones=NativeBones(body);var rest=nativeBones.Select(b=>b.localToWorldMatrix).ToArray();
        float maxError=0;
        foreach(var coordinates in new[]{NM.CreateScale(-1,1,1),NM.CreateScale(.7f,1.8f,1.1f)*NM.CreateRotationZ(.6f)*NM.CreateTranslation(1,2,-1)})
        {
            ReleaseALBinding(body);
            var transformed=Reexpress(body,coordinates,"body-coordinate-pose");var m=new Maid();
            PrepareALContext(m,new(){transformed},1);
            var r=new MeshRecord{SMR=transformed,Mesh=transformed.sharedMesh,OrigVerts=transformed.sharedMesh.vertices};
            Check(TryDeformAL(transformed,r,MeshMorphClass.Body,1,out var vertices,out _),"coordinate pose shape");
            InstallALBinding(m,r,vertices,1);InstallALBinding(maid,baseline,baselineVertices,1);
            foreach(float angle in new[]{0f,.7f,-1.1f})
            {
                for(int i=0;i<nativeBones.Length;i++)nativeBones[i].localToWorldMatrix=new(){Rows=rest[i].Rows*(nativeBones[i].name.Contains("Spine")?NM.CreateRotationY(angle):NM.Identity)*NM.CreateRotationX(.4f)*NM.CreateTranslation(2,3,4)};
                UpdateALBindings(maid);UpdateALBindings(m);
                for(int i=0;i<vertices.Length;i+=7)
                {
                    float error=(SkinPoint(body,baselineVertices[i],i)-SkinPoint(transformed,vertices[i],i)).magnitude;
                    maxError=Math.Max(maxError,error);Check(error<2e-5f,"world skin invariant to mesh coordinates");
                }
            }
            for(int i=0;i<nativeBones.Length;i++)nativeBones[i].localToWorldMatrix=rest[i];
            ReleaseALBindings(m);
        }
        ReleaseALBindings(maid);
        Console.WriteLine($"PASS: mirror/nonuniform coordinates preserve virtual skinning in three poses; max world error={maxError:R}.");
    }
    static void TestProportionsAndOverlappingHands()
    {
        foreach(var proportions in new[]{new NV(.7f,.6f,.8f),new NV(1.4f,1.6f,1.2f)})
        {
            var (maid,body,_)=Fixture();var stretch=NM.CreateScale(proportions);
            body.sharedMesh.vertices=body.sharedMesh.vertices.Select(v=>(Vector3)NV.Transform(v,stretch)).ToArray();
            for(int i=0;i<body.bones.Length;i++)body.bones[i].localToWorldMatrix=new(){Rows=body.bones[i].localToWorldMatrix.Rows*stretch};
            body.sharedMesh.bindposes=body.bones.Select(b=>b.worldToLocalMatrix).ToArray();
            var originals=body.sharedMesh.vertices;var weights=body.sharedMesh.boneWeights;int firstHand=originals.Length;
            // Put hand-owned vertices directly on front torso vertices in rest space.
            // A purely spatial clamp would fail this fixture.
            int handBone=body.bones.Length;
            body.bones=body.bones.Concat(new[]{new Transform{name="Bip01 L Hand"}}).ToArray();
            body.sharedMesh.bindposes=body.sharedMesh.bindposes.Concat(new[]{Matrix4x4.identity}).ToArray();
            body.sharedMesh.vertices=originals.Concat(originals.Where((v,i)=>i%64==0 && v.y>1.4f*proportions.Y)).ToArray();
            body.sharedMesh.boneWeights=weights.Concat(Enumerable.Repeat(new BoneWeight{boneIndex0=handBone,weight0=1},body.sharedMesh.vertexCount-firstHand)).ToArray();
            foreach(float stage in new[]{4f/9,1f})
            {
                PrepareALContext(maid,new(){body},stage);Check(_alWorking!=null,"proportion calibration");
                var r=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=body.sharedMesh.vertices};
                Check(TryDeformAL(body,r,MeshMorphClass.Body,stage,out var result,out _),"proportion shape");
                Check(result.Take(firstHand).Where((v,i)=>(v-r.OrigVerts[i]).sqrMagnitude>1e-8f).Any(),"abdomen still grows");
                for(int i=firstHand;i<result.Length;i++){Check((result[i]-r.OrigVerts[i]).sqrMagnitude==0,"overlapping hand fixed");Check(Attachment(r,i,stage)==0,"overlapping hand native bound");}
            }
        }
        Console.WriteLine("PASS: short/narrow and tall/wide rigs; hands overlapping the abdomen remain unaffected.");
    }
}
