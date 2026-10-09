using UnityEngine;
using NM=System.Numerics.Matrix4x4;
using NV=System.Numerics.Vector3;
using COM3D2.Pregnancy.Plugin.Growth;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        static int tests;
        static void Check(bool value,string name){tests++;if(!value)throw new Exception(name);}
        static Matrix4x4 Translation(float x,float y,float z)=>new(){Rows=NM.CreateTranslation(x,y,z)};
        static (Maid,SkinnedMeshRenderer,SkinnedMeshRenderer) Fixture()
        {
            var names=new[]{"Bip01 Pelvis_SCL_","Bip01 Spine_SCL_","Bip01 Spine0a_SCL_","Bip01 Spine1_SCL_","Bip01 L Thigh_SCL_","Bip01 R Thigh_SCL_","Mune_L"};
            var positions=new[]{new Vector3(0,0,0),new Vector3(0,1.4f,0),new Vector3(0,2.2f,0),new Vector3(0,3.1f,0),new Vector3(-.5f,0,0),new Vector3(.5f,0,0),new Vector3(-.3f,2.5f,.4f)};
            var bones=names.Select((name,i)=>new Transform{name=name,localToWorldMatrix=Translation(positions[i].x,positions[i].y,positions[i].z)}).ToArray();
            const int ny=41,nt=64;
            var vertices=new Vector3[ny*nt];var weights=new BoneWeight[vertices.Length];var triangles=new List<int>();
            for(int i=0;i<ny;i++)for(int j=0;j<nt;j++)
            {
                int k=i*nt+j;float y=-.2f+i*.085f,a=j*MathF.Tau/nt;
                vertices[k]=new Vector3(1.35f*MathF.Sin(a),y,-.15f+.85f*MathF.Cos(a));
                weights[k]=new BoneWeight {boneIndex0=0,weight0=.45f,boneIndex1=1,weight1=.3f,boneIndex2=2,weight2=.15f,boneIndex3=3,weight3=.1f};
                if(i<ny-1){int jn=(j+1)%nt;triangles.AddRange(new[]{k,i*nt+jn,k+nt,k+nt,i*nt+jn,(i+1)*nt+jn});}
            }
            var bodyMesh=new Mesh{name="body",vertices=vertices,normals=new Vector3[vertices.Length],boneWeights=weights,bindposes=bones.Select(b=>b.worldToLocalMatrix).ToArray(),triangles=triangles.ToArray()};
            var body=new SkinnedMeshRenderer{sharedMesh=bodyMesh,bones=bones,rootBone=bones[0]};
            var clothVertices=vertices.Select(v=>new Vector3(v.x*1.04f,v.y,(v.z+.15f)*1.04f-.15f)).ToArray();
            var clothMesh=new Mesh{name="wear",vertices=clothVertices,normals=new Vector3[vertices.Length],boneWeights=(BoneWeight[])weights.Clone(),bindposes=(Matrix4x4[])bodyMesh.bindposes.Clone(),triangles=triangles.ToArray()};
            return(new Maid(),body,new SkinnedMeshRenderer{sharedMesh=clothMesh,bones=bones,rootBone=bones[0]});
        }
        public static void Main(string[] args)
        {
            if(args.Length>0 && args[0]=="--leg-navel-tests") { RunLegNavelBindingTests(args); return; }
            if(args.Length>0 && args[0]=="--refresh-tests") { RunRefreshTests(args); return; }
            if(args.Length>0 && args[0]=="--refresh-benchmark") { RunRefreshBenchmark(args); return; }
            if(args.Length>0 && args[0]=="--skirt-exception") { RunSkirtExceptionTests(args); return; }
            if(args.Length>0 && args[0]=="--navel-timing") { RunNavelTimingTests(args); return; }
            if(args.Contains("--enable-sag")){Shape.BellySag=2;args=args.Where(a=>a!="--enable-sag").ToArray();}
            if(args.Length>0 && args[0]=="--sag-tests") { RunSagTests(args); return; }
            if(args.Length>0 && args[0]=="--clothing-pose-study") { RunClothingPoseStudy(args); return; }
            if(args.Length>0 && args[0]=="--breast-clothing-tests") { RunBreastClothingTests(); return; }
            if(args.Length>0 && args[0]=="--skirt-sparse") { RunSkirtSparseBoneTests(args); return; }
            if(args.Length>0 && args[0]=="--skirt-perf") { RunSkirtPerfStudy(args); return; }
            if(args.Length>0 && args[0]=="--body-isolation") { RunBodyIsolationStudy(args); return; }
            if(args.Length>0 && args[0]=="--drape-dump-study") { RunDrapeDumpStudy(args); return; }
            if(args.Length>0 && args[0]=="--clothing-study") { RunClothingStudy(args); return; }
            if(args.Length>0 && args[0]=="--upper-study") { RunUpperClothStudy(args); return; }
            if(args.Length>0 && args[0]=="--skirt-tests") { RunSkirtTests(args); return; }
            if(args.Length>0 && args[0]=="--navel-accessory") { TestNavelAccessory(args); return; }
            if(args.Length>0 && args[0]=="--stage-study") { RunStageStudy(args); return; }
            if(args.Length>0 && args[0]=="--timeline-tests") { TestGrowthTimeline(); return; }
            if(args.Length>0 && args[0]=="--normal-study") { RunNormalStudy(args); return; }
            if(args.Length>0 && args[0]=="--binding-study") { RunBindingStudy(args); return; }
            if(args.Length>0 && args[0]=="--perf-study") { RunPerfStudy(args); return; }
            if(args.Length>0 && args[0]=="--navel-study") { RunNavelStudy(args); return; }
            if(args.Length>0 && args[0]=="--volume") { RunVolumeSimulation(args); return; }
            if(args.Length>0 && args[0]=="--sensitivity") { RunSensitivity(args); return; }
            if(args.Length>0 && args[0]=="--region") { RunRegionSimulation(args); return; }
            if(args.Length>0 && args[0]=="--guard") { RunGuardSimulation(args); return; }
            var (maid,body,cloth)=Fixture();var renderers=new List<SkinnedMeshRenderer>{cloth,body};
            var original=body.sharedMesh.vertices;var originalCloth=cloth.sharedMesh.vertices;
            var originalWeights=(BoneWeight[])body.sharedMesh.boneWeights.Clone();var originalBinds=(Matrix4x4[])body.sharedMesh.bindposes.Clone();
            var originalBones=body.bones;var originalBounds=body.localBounds;
            PrepareALContext(maid,renderers,1);
            Check(_alWorking!=null,"COM rest calibration");
            Check(_alWorking.Surface.SurfaceTriangles.Count>0,"AL body surface built before clothing");
            ApplySMR(maid,cloth,1,MeshMorphClass.OuterCloth,false);
            ApplySMR(maid,body,1,MeshMorphClass.Body,false);
            var first=body.sharedMesh.vertices;var firstCloth=cloth.sharedMesh.vertices;
            Check(first.Where((v,i)=>(v-original[i]).sqrMagnitude>1e-6f).Count()>300,"belly grows");
            Check(_alBindings.Count==2,"body and clothing bound");
            Check(body.bones.Length>originalBones.Length,"virtual palette installed");
            foreach(float stage in new[]{1f,1f,1f})
            {
                PrepareALContext(maid,renderers,stage);
                ApplySMR(maid,body,stage,MeshMorphClass.Body,false);ApplySMR(maid,cloth,stage,MeshMorphClass.OuterCloth,false);
                var actual=body.sharedMesh.vertices;var ca=cloth.sharedMesh.vertices;
                Check(actual.Select((v,i)=>(v-first[i]).magnitude).Max()<2e-6f,"repeat apply body identity");
                Check(ca.Select((v,i)=>(v-firstCloth[i]).magnitude).Max()<2e-6f,"repeat apply cloth identity");
                Check(body.bones.Length==_alBindings[body].Bones.Length+_alBindings[body].Recipes.Length,"palette not appended twice");
            }
            var binding=_alBindings[body];int writes=body.sharedMesh.VertexWrites;
            foreach(float angle in new[]{0f,.7f,-1.4f,2.5f})
            {
                var bend=new Matrix4x4{Rows=NM.CreateTranslation(0,-1.4f,0)*NM.CreateRotationX(angle)*NM.CreateTranslation(0,1.4f,0)};
                for(int i=1;i<4;i++)body.bones[i].localToWorldMatrix=bend*originalBinds[i].inverse;
                UpdateALBindings(maid);
                Check(body.sharedMesh.VertexWrites==writes,"pose only updates matrices");
                var c=binding.Context;var virtualMatrix=binding.ToReference*VirtualAxisMath.Evaluate(c.Axis,c.PelvisBind*MatrixBridge.ToManaged(c.Pelvis.localToWorldMatrix),c.SpineBind*MatrixBridge.ToManaged(c.Spine.localToWorldMatrix),Shape).Transform;
                var record=FindRecord(maid,body);
                for(int i=0;i<first.Length;i+=13)
                {
                    float alpha=Attachment(record,i,1);
                    var expected=Growth.Numerics.Vector3.Zero;
                    foreach(var w in Influences(originalWeights[i]))expected+=Growth.Numerics.Vector3.Transform(N(first[i]),MatrixBridge.ToManaged(originalBinds[w.Bone])*MatrixBridge.ToManaged(originalBones[w.Bone].localToWorldMatrix))*w.Weight*(1-alpha);
                    expected+=Growth.Numerics.Vector3.Transform(N(first[i]),virtualMatrix)*alpha;
                    var actual=Vector3.zero;
                    foreach(var w in Influences(body.sharedMesh.boneWeights[i]))if(w.Weight>0)actual+=(body.bones[w.Bone].localToWorldMatrix*body.sharedMesh.bindposes[w.Bone]).MultiplyPoint3x4(first[i])*w.Weight;
                    Check((actual-U(expected)).magnitude<1e-5f,"Unity palette agrees with AL full skin equation");
                    var inBounds=body.rootBone.worldToLocalMatrix.MultiplyPoint3x4(actual)-body.localBounds.center;
                    Check(Math.Abs(inBounds.x)<=body.localBounds.size.x*.5f+1e-4f && Math.Abs(inBounds.y)<=body.localBounds.size.y*.5f+1e-4f && Math.Abs(inBounds.z)<=body.localBounds.size.z*.5f+1e-4f,"virtual culling bounds enclose skin");
                }
            }
            ReleaseALBindings(maid);
            Check(body.bones.Length==originalBones.Length && body.sharedMesh.bindposes.Length==originalBinds.Length,"native palette restored");
            Check(!body.updateWhenOffscreen && body.quality==SkinQuality.Auto,"renderer binding settings restored");
            for(int i=0;i<originalWeights.Length;i++)Check(body.sharedMesh.boneWeights[i].Equals(originalWeights[i]),"native weights restored");
            PrepareALContext(maid,renderers,0);ApplySMR(maid,body,0,MeshMorphClass.Body,false);ApplySMR(maid,cloth,0,MeshMorphClass.OuterCloth,false);
            Check(body.sharedMesh.vertices.Select((v,i)=>(v-original[i]).magnitude).Max()<2e-6f,"stage zero restores body");
            Check(cloth.sharedMesh.vertices.Select((v,i)=>(v-originalCloth[i]).magnitude).Max()<2e-6f,"stage zero restores clothes");

            // TMorph baked base path followed by ordinary Apply must not add the belly twice.
            var (m2,b2,c2)=Fixture();PrepareALContext(m2,new(){b2,c2},1);
            var r2=new MeshRecord{SMR=b2,Mesh=b2.sharedMesh,OrigVerts=b2.sharedMesh.vertices};
            Check(TryDeformAL(b2,r2,MeshMorphClass.Body,1,out var baked,out _),"TMorph shape bake");
            var morph=new TMorph{m_vOriVert=baked};
            _morphBaseBakeStates[morph]=new MorphBaseBakeState {Renderer=b2,Mesh=b2.sharedMesh,OriginalVerts=r2.OrigVerts,BakedVerts=baked,AppliedSignature=ComputeVertexSignature(baked)};
            b2.sharedMesh.vertices=baked;
            PrepareALContext(m2,new(){b2,c2},1);ApplySMR(m2,b2,1,MeshMorphClass.Body,false);
            Check(b2.sharedMesh.vertices.Select((v,i)=>(v-baked[i]).magnitude).Max()<2e-6f,"baked base is not deformed twice");
            var context=_alWorking;bool negativeVerified=false;
            foreach(var probe in context.Surface.SurfaceTriangles)
            {
                var surface=context.Surface.Meshes[probe.MeshIndex];
                var oi=(surface.Original[probe.A]+surface.Original[probe.B]+surface.Original[probe.C])/3;
                var move=(surface.Morphed[probe.A]+surface.Morphed[probe.B]+surface.Morphed[probe.C])/3-oi;
                if(move.magnitude<.01f)continue;
                if(TryGetBodySurfaceClothTarget(context.Surface,oi,-.5f,context.Frame,out var target) && Vector3.Dot(target-oi,move)<0)
                {negativeVerified=true;break;}
            }
            Check(negativeVerified,"negative cloth input actually reverses surface displacement");
            var baselineSettings=Shape.Copy();
            foreach(float value in new[]{-1f,0f,2.5f})
            {
                Shape.ClothOffset=value;
                var cr=new MeshRecord{SMR=c2,Mesh=c2.sharedMesh,OrigVerts=c2.sharedMesh.vertices};
                Check(TryDeformAL(c2,cr,MeshMorphClass.OuterCloth,1,out var cv,out _),"out-of-range cloth applies");
                Check(cv.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"out-of-range cloth finite");
            }
            Shape=baselineSettings;
            var legacy=new VtxSettings{GrowthWidth=2.75f,LateForwardShift=0,GrowthAxisTilt=0,LateHeightScale=0,LateDepthScale=0,LateWidthScale=0};
            legacy.CompleteGrowthDefaults("{\"growthModelVersion\":1,\"growth\":{\"GrowthWidth\":2.75}}");
            Check(legacy.GrowthWidth==2.75f && legacy.LateHeightScale==1.15f && legacy.LateDepthScale==1.10f,"missing late fields get defaults without overwriting old controls");
            legacy.LateDepthScale=0;legacy.CompleteGrowthDefaults("{\"LateDepthScale\":0}");
            Check(legacy.LateDepthScale==0,"explicit saved zero preserved");
            var savedShape=Shape;Shape=new VtxSettings();int hash=ComputeMorphBakeSignature(1,MeshMorphClass.Body);
            Shape.LateForwardShift+=1e-6f;
            Check(hash!=ComputeMorphBakeSignature(1,MeshMorphClass.Body),"sub-1e-5 numerical edits invalidate shape cache");Shape=savedShape;
            Console.WriteLine($"PASS: {tests} adapter assertions (synthetic COM rig; real port entry points, stub Unity engine).");
        }
    }
}
