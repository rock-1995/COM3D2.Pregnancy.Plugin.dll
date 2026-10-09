using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using V=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
using M=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;
using NM=System.Numerics.Matrix4x4;

namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static Vector3 Rendered(SkinnedMeshRenderer r,int i)
    {
        var point=r.sharedMesh.vertices[i];var result=Vector3.zero;
        foreach(var w in Influences(r.sharedMesh.boneWeights[i]))if(w.Weight>0)
            result+=(r.bones[w.Bone].localToWorldMatrix*r.sharedMesh.bindposes[w.Bone]).MultiplyPoint3x4(point)*w.Weight;
        return result;
    }
    static SkinnedMeshRenderer TestAccessory(SkinnedMeshRenderer body,ALContext c)
    {
        var p=c.Frame.Profile;float y=p.SkinNavelY+Shape.NavelVerticalOffset*p.Span;
        M.Invert(c.Maps[body],out var inv);
        var verts=new List<Vector3>();
        for(int i=0;i<16;i++)
        {
            float angle=i*MathF.Tau/16;
            var v=new V(MathF.Cos(angle)*p.Span*.015f,y+MathF.Sin(angle)*p.Span*.02f,p.SkinNavelZ+p.Span*.02f);
            verts.Add(U(V.Transform(v,c.FrameMatrix*inv)));
        }
        var bones=NativeBones(body);var binds=NativeBinds(body);int pelvis=FindBoneIndex(bones,"Bip01 Pelvis_SCL_","Bip01 Pelvis");
        var triangles=Enumerable.Range(1,14).SelectMany(i=>new[]{0,i,i+1}).ToArray();
        return new SkinnedMeshRenderer {sharedMesh=new Mesh{name="accheso-test",vertices=verts.ToArray(),normals=new Vector3[16],triangles=triangles,
            boneWeights=Enumerable.Repeat(new BoneWeight{boneIndex0=0,weight0=1},16).ToArray(),bindposes=new[]{binds[pelvis]}},bones=new[]{bones[pelvis]},rootBone=bones[pelvis]};
    }
    static void TestNavelAccessory(string[] args)
    {
        var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        var points=Enumerable.Range(0,12).Select(i=>{float a=i*MathF.Tau/12;return new V(MathF.Cos(a),1.3f*MathF.Sin(a),.4f*MathF.Cos(a)+.2f*MathF.Sin(a));}).ToArray();
        Check(NavelPlane.Fit(points,1,out var plane),"analytic plane fit");
        var expected=V.Normalize(new V(-.4f,-.2f/1.3f,1));var normal=new V(plane.M31,plane.M32,plane.M33);
        Check(V.Distance(normal,expected)<1e-5f,"least squares plane normal");
        long allocation=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++)NavelPlane.Fit(points,1,out _);
        Check(GC.GetAllocatedBytesForCurrentThread()==allocation,"plane fitter allocates zero on steady poses");
        Check(!NavelPlane.Fit(new V[12],1,out _),"collapsed ring rejected");
        var transform=M.CreateFromAxisAngle(V.UnitY,.7f)*M.CreateTranslation(new V(3,-2,4));
        var rotated=points.Select(v=>V.Transform(v,transform)).ToArray();
        Check(NavelPlane.Fit(rotated,1,out var rotatedPlane),"rotated plane fits");
        Check(V.Distance(new V(rotatedPlane.M31,rotatedPlane.M32,rotatedPlane.M33),V.TransformNormal(expected,transform))<1e-5f,"plane rotation covariant");

        var body=ReadModel(args[2],"body-real");var maid=new Maid();Shape=new VtxSettings();
        PrepareALContext(maid,new(){body},1);Check(float.IsFinite(_alWorking.Frame.Profile.SkinNavelZ),"actual RV navel found");
        var accessory=TestAccessory(body,_alWorking);var originalAccessory=accessory.sharedMesh.vertices;
        var nativeBones=body.bones;var nativeBinds=body.sharedMesh.bindposes;
        var originalBody=body.sharedMesh.vertices;var poses=nativeBones.Select(b=>b.localToWorldMatrix).ToArray();
        var renderers=new List<SkinnedMeshRenderer>{accessory,body};var rows=new List<object>();
        foreach(float stage in new[]{0f,1f/3,4f/9,5f/9,1f})
        {
            for(int i=0;i<nativeBones.Length;i++)nativeBones[i].localToWorldMatrix=poses[i];
            PrepareALContext(maid,renderers,stage);Check(_alWorking!=null,"context with accessory");
            ApplySMR(maid,body,stage,MeshMorphClass.Body,false);ApplySMR(maid,accessory,stage,MeshMorphClass.NavelAccessory,false);
            var rec=FindRecord(maid,accessory);var a=rec.Navel;Check(a!=null,"12-point boundary support");
            Check(a.Indices.Length<=36,"bounded body stencil");
            M.Invert(a.OriginalFrame,out var oldPlaneInv);
            float maxRigid=0;
            for(int i=0;i<originalAccessory.Length;i++)
            {
                var from=V.Transform(N(originalAccessory[i]),rec.ToReference*oldPlaneInv);
                var to=V.Transform(N(accessory.sharedMesh.vertices[i]),rec.ToReference*a.BakedFrameInverse);
                maxRigid=Math.Max(maxRigid,V.Distance(from,to));
            }
            Check(maxRigid<3e-6f,"shape attachment preserves plane-local offset and dimensions");
            if(stage==0){Check(!_alBindings.ContainsKey(accessory),"stage zero native binding");Check(accessory.sharedMesh.vertices.SequenceEqual(originalAccessory),"stage zero exact vertices");continue;}
            Check(_alBindings.ContainsKey(accessory),"single accessory palette slot installed");
            int writes=accessory.sharedMesh.VertexWrites;float maxSkinError=0,maxPlaneError=0;
            foreach(float angle in new[]{0f,.6f,-1f,1.2f})
            {
                var bend=new Matrix4x4{Rows=NM.CreateRotationX(angle)};
                for(int i=0;i<nativeBones.Length;i++)nativeBones[i].localToWorldMatrix=nativeBones[i].name.ToLowerInvariant().Contains("spine")?bend*poses[i]:poses[i];
                UpdateALBindings(maid);Check(accessory.sharedMesh.VertexWrites==writes,"pose never rewrites accessory vertices");
                for(int k=0;k<a.Indices.Length;k++)maxSkinError=Math.Max(maxSkinError,V.Distance(N(Rendered(body,a.Indices[k])),a.PosedVertices[k]));
                M.Invert(a.LastFrame,out var posedInv);
                for(int i=0;i<originalAccessory.Length;i++)
                {
                    var from=V.Transform(N(originalAccessory[i]),rec.ToReference*oldPlaneInv);
                    var to=V.Transform(N(Rendered(accessory,i)),posedInv);maxPlaneError=Math.Max(maxPlaneError,V.Distance(from,to));
                    var rootPoint=accessory.rootBone.worldToLocalMatrix.MultiplyPoint3x4(Rendered(accessory,i))-accessory.localBounds.center;
                    Check(Math.Abs(rootPoint.x)<=accessory.localBounds.size.x*.5f+1e-5f&&Math.Abs(rootPoint.y)<=accessory.localBounds.size.y*.5f+1e-5f&&Math.Abs(rootPoint.z)<=accessory.localBounds.size.z*.5f+1e-5f,"accessory culling bounds");
                }
            }
            Check(maxSkinError<3e-6f,"plane samples equal actual rendered body skin");Check(maxPlaneError<3e-6f,"posed accessory rigidly follows fitted plane");
            rows.Add(new {stage,uniqueVertices=a.Indices.Length,maxRigid,maxSkinError,maxPlaneError});
            if(stage==1)
            {
                var export=new {body=originalBody.Select(v=>new[]{v.x,v.y,v.z}),triangles=body.sharedMesh.triangles,
                    deformed=body.sharedMesh.vertices.Select(v=>new[]{v.x,v.y,v.z}),toReference=Enumerable.Range(0,16).Select(i=>rec.ToReference[i/4,i%4]),
                    referenceToLocal=Enumerable.Range(0,16).Select(i=>rec.GrowthContext.InverseFrame[i/4,i%4]),
                    originalRing=a.OriginalRing.Select(v=>new[]{v.X,v.Y,v.Z}),deformedRing=a.MorphedRing.Select(v=>new[]{v.X,v.Y,v.Z}),posedRing=a.PosedRing.Select(v=>new[]{v.X,v.Y,v.Z}),
                    accessoryOriginal=originalAccessory.Select(v=>new[]{v.x,v.y,v.z}),accessoryDeformed=accessory.sharedMesh.vertices.Select(v=>new[]{v.x,v.y,v.z})};
                File.WriteAllText(Path.Combine(output,"geometry.json"),JsonSerializer.Serialize(export));
            }
        }
        for(int i=0;i<nativeBones.Length;i++)nativeBones[i].localToWorldMatrix=poses[i];
        var liveAccessoryBinding=_alBindings[accessory];
        var modifiedBase=originalBody.Select(v=>new Vector3(v.x*1.01f,v.y,v.z)).ToArray();
        body.sharedMesh.vertices=modifiedBase;
        PrepareALContext(maid,renderers,1);ApplySMR(maid,body,1,MeshMorphClass.Body,false);
        int accessoryWrites=accessory.sharedMesh.VertexWrites;
        UpdateALBindings(maid);
        Check(liveAccessoryBinding.Navel.Context==_alWorking,"body-only refresh retargets accessory support");
        Check(accessory.sharedMesh.VertexWrites==accessoryWrites,"body-only refresh uses only attachment matrix");
        var support=liveAccessoryBinding.Navel;
        for(int k=0;k<support.Indices.Length;k++)Check(V.Distance(N(Rendered(body,support.Indices[k])),support.PosedVertices[k])<3e-6f,"refreshed support equals rendered body");
        // Same accessory in an unrelated storage coordinate system, including a reflection.
        var before=accessory.sharedMesh.vertices;ReleaseALBindings(maid);
        var nativeCopy=TestAccessory(body,_alWorking);var storage=NM.CreateScale(-1.7f,.6f,2.2f)*NM.CreateRotationY(.7f)*NM.CreateTranslation(2,-3,1);
        var other=Reexpress(nativeCopy,storage,"accheso-reexpressed");
        var reference=new MeshRecord {SMR=nativeCopy,Mesh=nativeCopy.sharedMesh,OrigVerts=nativeCopy.sharedMesh.vertices};
        PrepareALContext(maid,new(){body,nativeCopy,other},1);Check(TryDeformNavelAccessory(nativeCopy,reference,1,out var nativeResult,out _),"native accessory rest map");
        var rr=new MeshRecord{SMR=other,Mesh=other.sharedMesh,OrigVerts=other.sharedMesh.vertices};
        Check(TryDeformNavelAccessory(other,rr,1,out var otherResult,out _),"transformed accessory rest map");
        NM.Invert(storage,out var storageInv);
        Check(otherResult.Select((v,i)=>Vector3.Distance((Vector3)System.Numerics.Vector3.Transform(v,storageInv),nativeResult[i])).Max()<5e-6f,"coordinate translation, rotation, mirror and scale invariant");
        // Unrelated body/clothing settings remain identical; parameter edits only reposition the plane region.
        foreach(var setting in new[]{(-.04f,.03f),(0f,.08f),(0f,0f)})
        {
            Shape.NavelVerticalOffset=setting.Item1;Shape.NavelRadius=setting.Item2;
            PrepareALContext(maid,new(){body,other},1);Check(TryDeformNavelAccessory(other,rr,1,out _,out _),"plane tracks numeric navel settings");
            Check(Math.Abs(rr.Navel.CenterY-(_alWorking.Frame.Profile.SkinNavelY+setting.Item1*_alWorking.Frame.Profile.Span))<1e-6f,"same navel offset as body patch");
        }
        Shape=new VtxSettings();body.enabled=false;
        PrepareALContext(maid,new(){body,other},1);
        Check(_alWorking.Surface.Meshes.Count==0,"accessory support does not change hidden-body clothing surface policy");
        Check(TryDeformNavelAccessory(other,rr,1,out _,out _),"navel plane works with hidden body");body.enabled=true;
        Shape.VirtualAxisStrength=0;
        PrepareALContext(maid,new(){body,accessory},1);ApplySMR(maid,body,1,MeshMorphClass.Body,false);ApplySMR(maid,accessory,1,MeshMorphClass.NavelAccessory,false);
        UpdateALBindings(maid);Check(_alBindings[accessory].Navel.Alpha.All(x=>x==0),"native-only body binding supported");
        PrepareALContext(maid,new(){body,accessory},0);ApplySMR(maid,body,0,MeshMorphClass.Body,false);ApplySMR(maid,accessory,0,MeshMorphClass.NavelAccessory,false);
        Check(!_alBindings.ContainsKey(accessory)&&accessory.bones.Length==1,"return to stage zero removes accessory virtual binding");
        Check(accessory.sharedMesh.vertices.Select((v,i)=>Vector3.Distance(v,originalAccessory[i])).Max()<1e-6f,"return to stage zero restores accessory geometry");
        Check(accessory.sharedMesh.boneWeights.All(w=>w.boneIndex0==0&&w.weight0==1)&&accessory.quality==SkinQuality.Auto&&!accessory.updateWhenOffscreen,"native accessory weights and renderer flags restored");
        File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{assertions=tests,stages=rows},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {tests} assertions; real RV body + synthetic navel accessory, 12 boundary samples, pregnancy stages, posed skin, rigidity, bounds, coordinate transforms, zero-stage restore, parameters, allocation-free plane fit.");
    }
}
