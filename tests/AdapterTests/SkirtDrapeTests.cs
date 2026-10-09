using System.Diagnostics;
using UnityEngine;
using NM=System.Numerics.Matrix4x4;
using NV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
using NX=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;
using COM3D2.Pregnancy.Plugin.Growth;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunSkirtTests(string[] args)
    {
        TestHiddenBodyTopology();
        TestOuterClothContact();
        TestArmExclusion();
        TestSkirtOptimization();
        var parents=new[]{2,3,-1,0};var sample=new[]{new NV(0,0,.20f),new NV(0,0,.05f),new NV(0,0,.10f),new NV(0,0,.15f)};
        SkirtDrape.Resolve(parents,sample,new[]{true,true,true,true},.001f,out var offsets,out var free);
        Check(!free[2]&&free[0]&&free[3]&&free[1],"shuffled hierarchy releases one joint before first decrease");
        Check(NV.Distance(offsets[3],sample[0])==0&&NV.Distance(offsets[1],sample[0])==0,"last supported offset carried without accumulating");
        SkirtDrape.Resolve(new[]{-1,0,1},new[]{new NV(0,0,.10f),new NV(0,0,.0999f),NV.Zero},new[]{true,true,true},.001f,out offsets,out free);
        Check(!free[0]&&free[1]&&free[2],"sub-tolerance noise does not move the release point");
        SkirtDrape.Resolve(new[]{-1,0,1,-1,3,4},new[]{new NV(0,0,.1f),new NV(0,0,.2f),new NV(0,0,.15f),new NV(0,0,.3f),new NV(0,0,.4f),new NV(0,0,.5f)},new[]{true,true,true,true,true,true},.001f,out offsets,out free);
        Check(!free[0]&&free[1]&&free[2]&&!free[3]&&!free[4]&&!free[5],"each chain finds its own crest without releasing neighbors");
        Check(NV.Distance(offsets[2],new NV(0,0,.2f))==0&&NV.Distance(offsets[5],new NV(0,0,.5f))==0,"independent chain offsets are preserved");

        foreach(string path in args.Skip(2))TestRealSkirt(args[1],path);
        Console.WriteLine($"PASS: {tests} skirt/contact assertions; real game models, production deformation and skinning, managed Unity stubs.");
    }
    static void TestRealSkirt(string bodyPath,string garmentPath)
    {
        var body=ReadModel(bodyPath,"body");var garment=ReadModel(garmentPath,Path.GetFileNameWithoutExtension(garmentPath));var maid=new Maid();
        var nativeVertices=garment.sharedMesh.vertices;var nativeWeights=garment.sharedMesh.boneWeights;var nativeBones=garment.bones;var nativeBinds=garment.sharedMesh.bindposes;
        PrepareALContext(maid,new(){body,garment},1);var c=_alWorking;Check(c!=null,"real calibration");
        var rec=new MeshRecord{SMR=garment,Mesh=garment.sharedMesh,OrigVerts=nativeVertices,OrigNormals=garment.sharedMesh.normals};
        Check(TryDeformAL(garment,rec,MeshMorphClass.OuterCloth,1,out var residual,out _),"real skirt deforms");
        Check(rec.Skirt!=null&&rec.Skirt.Released.Any(v=>v),"real skirt has released chains");
        int smallWeightChecks=0;
        for(int i=0;i<nativeVertices.Length;i++)
        {
            float skirtWeight=0,freeWeight=0;var carried=NV.Zero;var ws=Influences(nativeWeights[i]);
            for(int k=0;k<ws.Length;k++)
            {
                var w=ws[k];if(w.Weight<=0||!rec.Skirt.Controlled[w.Bone])continue;
                skirtWeight+=w.Weight;freeWeight+=w.Weight*rec.Skirt.Free[i][k];
                carried+=rec.Skirt.Offsets[w.Bone]*(w.Weight*rec.Skirt.Free[i][k]);
            }
            if(skirtWeight>1e-7f&&skirtWeight<.05f&&freeWeight/skirtWeight>.99999f)
            {
                var actual=NV.TransformNormal(N(rec.Skirt.VisualVertices[i]-nativeVertices[i]),rec.ToReference);
                Check(float.IsFinite(actual.X+actual.Y+actual.Z) && rec.Skirt.Free[i].Any(v=>v>0),"positive small skirt weight participates in material transition and native carry");smallWeightChecks++;
            }
        }
        var meshToWorld=MatrixBridge.ToUnity(rec.ToReference*c.PelvisBind*MatrixBridge.ToManaged(c.Pelvis.localToWorldMatrix));
        for(int i=0;i<nativeBones.Length;i++)nativeBones[i].localToWorldMatrix=meshToWorld*nativeBinds[i].inverse;
        garment.sharedMesh.vertices=residual;InstallALBinding(maid,rec,residual,1);
        var binding=_alBindings[garment];int paletteLength=garment.bones.Length;
        var nativeRest=nativeBones.Select(v=>v.localToWorldMatrix).ToArray();
        var bodyRest=body.bones.Select(v=>v.localToWorldMatrix).ToArray();
        for(int i=0;i<residual.Length;i++)
        {
            var actual=meshToWorld.inverse.MultiplyPoint3x4(SkinPoint(garment,residual[i],i));
            Check(Vector3.Distance(actual,rec.Skirt.VisualVertices[i])<2e-5f,"bone/mesh compensation equals intended visible rest shape");
        }
        int allFree=0;
        for(int i=0;i<residual.Length;i++)
        {
            var ws=Influences(nativeWeights[i]);float fraction=ws.Select((w,k)=>w.Weight*rec.Skirt.Free[i][k]).Sum();
            if(fraction>.99999f){allFree++;Check(Vector3.Distance(residual[i],nativeVertices[i])<2e-6f,"fully released vertex has original local geometry");}
        }
        Check(allFree>0,"real garment covers fully released vertices");
        int writes=garment.sharedMesh.VertexWrites;
        foreach(float sway in new[]{0f,.35f,-.55f,1.1f})
        {
            var global=new Matrix4x4{Rows=NM.CreateRotationY(.7f)*NM.CreateTranslation(2,-1,3)};
            var swing=new Matrix4x4{Rows=NM.CreateRotationX(sway)};
            for(int i=0;i<body.bones.Length;i++)body.bones[i].localToWorldMatrix=global*bodyRest[i];
            // Independent native skirt rotation stands in for the physical solver.
            for(int i=0;i<nativeBones.Length;i++)nativeBones[i].localToWorldMatrix=global*(IsDrapeBone(nativeBones[i])?swing:Matrix4x4.identity)*nativeRest[i];
            var before=nativeBones.Select(v=>v.localToWorldMatrix).ToArray();
            RefreshSkirtPose(new TBody{maid=maid});
            Check(garment.sharedMesh.VertexWrites==writes,"poses do not rewrite mesh vertices");
            var vt=VirtualAxisMath.Evaluate(c.Axis,c.PelvisBind*MatrixBridge.ToManaged(c.Pelvis.localToWorldMatrix),c.SpineBind*MatrixBridge.ToManaged(c.Spine.localToWorldMatrix),Shape).Transform;
            for(int i=0;i<residual.Length;i++)
            {
                var ws=Influences(nativeWeights[i]);float fraction=ws.Select((w,k)=>w.Weight*rec.Skirt.Free[i][k]).Sum();
                var actual=SkinPoint(garment,residual[i],i);
                if(rec.BreastExcluded[i] || rec.TorsoOwnership[i]<=0)
                {
                    var expected=Vector3.zero;
                    foreach(var w in ws)if(w.Weight>0)
                        expected+=(nativeBones[w.Bone].localToWorldMatrix*nativeBinds[w.Bone]).MultiplyPoint3x4(nativeVertices[i])*w.Weight;
                    Check(Vector3.Distance(actual,expected)<2e-5f,"excluded breast/arm vertex bypasses skirt binding and keeps native pose");
                }
                if(fraction>.99999f)
                {
                    var expected=Vector3.zero;
                    foreach(var w in ws)if(w.Weight>0)
                        expected+=((nativeBones[w.Bone].localToWorldMatrix*nativeBinds[w.Bone]).MultiplyPoint3x4(nativeVertices[i])+
                            U(NV.TransformNormal(rec.Skirt.Offsets[w.Bone],vt)))*w.Weight;
                    Check(Vector3.Distance(actual,expected)<2e-5f,"free hem retains native sway plus carried displacement");
                }
                var local=garment.rootBone.worldToLocalMatrix.MultiplyPoint3x4(actual)-garment.localBounds.center;
                Check(Math.Abs(local.x)<=garment.localBounds.size.x*.5f+1e-5f&&Math.Abs(local.y)<=garment.localBounds.size.y*.5f+1e-5f&&Math.Abs(local.z)<=garment.localBounds.size.z*.5f+1e-5f,"posed skirt stays within culling bounds");
            }
            for(int i=0;i<nativeBones.Length;i++)Check(nativeBones[i].localToWorldMatrix.Rows.Equals(before[i].Rows),"native physical transforms untouched");
            foreach(var recipe in binding.SkirtRecipes.Select((r,i)=>(r,i)).Where(x=>x.r.Free==1))
            {
                var actual=MatrixBridge.ToManaged(c.Pelvis.localToWorldMatrix*binding.Palette[nativeBinds.Length+recipe.i]);
                var expected=MatrixBridge.ToManaged(nativeBinds[recipe.r.Bone])*MatrixBridge.ToManaged(nativeBones[recipe.r.Bone].localToWorldMatrix);
                foreach(var v in new[]{NV.UnitX,NV.UnitY,NV.UnitZ})Check(NV.Distance(NV.TransformNormal(v,actual),NV.TransformNormal(v,expected))<2e-5f,"released bone linear part is native, no direction restoration");
            }
            var posed=residual.Select((v,i)=>SkinPoint(garment,v,i)).ToArray();int tick=binding.Tick;
            for(int repeat=0;repeat<6;repeat++)UpdateALBinding(binding);
            Check(binding.Tick==tick,"unchanged pose skips palette and bounds recomputation across both hooks");
            for(int i=0;i<residual.Length;i+=17)Check(Vector3.Distance(SkinPoint(garment,residual[i],i),posed[i])<2e-5f,"repeated pose update does not accumulate offsets");
        }
        for(int i=0;i<body.bones.Length;i++)body.bones[i].localToWorldMatrix=bodyRest[i];
        for(int i=0;i<nativeBones.Length;i++)nativeBones[i].localToWorldMatrix=nativeRest[i];
        for(int repeat=0;repeat<3;repeat++)
        {
            TryDeformAL(garment,rec,MeshMorphClass.OuterCloth,1,out var again,out _);
            Check(again.Select((v,i)=>Vector3.Distance(v,residual[i])).Max()<2e-6f,"repeat shape does not bake offset twice");
            InstallALBinding(maid,rec,again,1);Check(garment.bones.Length==paletteLength,"repeat binding does not grow palette");
        }
        binding=_alBindings[garment];var clock=Stopwatch.StartNew();
        for(int i=0;i<100;i++)UpdateALBinding(binding);clock.Stop();
        Console.WriteLine($"{garment.name}: released bones={rec.Skirt.Released.Count(v=>v)}, fully free vertices={allFree}, small-weight checks={smallWeightChecks}, palette={paletteLength}, managed pose mean={clock.Elapsed.TotalMilliseconds/100:F3}ms (not game timing)");
        ReleaseALBindings(maid);
        Check(garment.bones==nativeBones&&garment.sharedMesh.boneWeights==nativeWeights&&garment.sharedMesh.bindposes==nativeBinds,"release restores exact native bindings");
        float oldStrength=Shape.VirtualAxisStrength;Shape.VirtualAxisStrength=0;
        InstallALBinding(maid,rec,residual,1);Check(_alBindings.ContainsKey(garment),"skirt carry works with virtual axis disabled");
        ReleaseALBindings(maid);Shape.VirtualAxisStrength=oldStrength;
        TryDeformAL(garment,rec,MeshMorphClass.OuterCloth,0,out var zero,out _);
        Check(rec.Skirt==null&&zero.Select((v,i)=>Vector3.Distance(v,nativeVertices[i])).Max()<2e-6f,"stage zero removes drape and restores vertices");
        InstallALBinding(maid,rec,zero,0);Check(!_alBindings.ContainsKey(garment),"stage zero leaves no binding");
        TryDeformAL(garment,rec,MeshMorphClass.OuterCloth,1,out residual,out _);InstallALBinding(maid,rec,residual,1);
        var oldMesh=garment.sharedMesh;garment.sharedMesh=new Mesh{name="replacement"};
        RefreshSkirtPose(new TBody{maid=maid});Check(!_alBindings.ContainsKey(garment),"replacement mesh prunes old skirt binding");
        Check(oldMesh.boneWeights==nativeWeights&&oldMesh.bindposes==nativeBinds,"old garment native binding restored on replacement");


    }
}

