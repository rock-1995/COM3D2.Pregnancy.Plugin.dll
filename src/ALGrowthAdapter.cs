using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NVector=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
using NMatrix=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        sealed class ALContext
        {
            public Maid Maid;
            public int Signature;
            public VtxSettings Settings;
            public NMatrix BodyToReference;
            public readonly Dictionary<SkinnedMeshRenderer,int> MapSignatures=new Dictionary<SkinnedMeshRenderer,int>();
            public SkinnedMeshRenderer Body;
            public LocalFrame Frame;
            public NMatrix FrameMatrix, InverseFrame;
            public Transform Pelvis, Spine;
            public NMatrix PelvisBind, SpineBind;
            public VirtualAxisMath.Axis Axis;
            public BodyAnchorContext Surface;
            public MaterialBlendField Material;
            public float Stage;
            public readonly List<MeshRecord> NavelBodies=new List<MeshRecord>();
            public readonly Dictionary<SkinnedMeshRenderer,NMatrix> Maps=new Dictionary<SkinnedMeshRenderer,NMatrix>();
        }
        static readonly Dictionary<int,ALContext> _alContexts=new Dictionary<int,ALContext>();
        static ALContext _alWorking;

        static NVector N(Vector3 v) => new NVector(v.x,v.y,v.z);
        static Vector3 U(NVector v) => new Vector3(v.X,v.Y,v.Z);
        static NMatrix FrameMatrix(LocalFrame f) => new NMatrix(f.Right.x,f.Right.y,f.Right.z,0,f.Up.x,f.Up.y,f.Up.z,0,f.Fwd.x,f.Fwd.y,f.Fwd.z,0,f.Center.x,f.Center.y,f.Center.z,1);

        static Vector3[] CleanVertices(Maid maid,SkinnedMeshRenderer smr)
        {
            var current=smr.sharedMesh.vertices;
            int sig=ComputeVertexSignature(current);
            var record=FindRecord(maid,smr);
            if(record!=null && record.OrigVerts!=null && record.OrigVerts.Length==current.Length && record.AppliedSignature==sig)
                return record.OrigVerts;
            foreach(var pair in _morphBaseBakeStates)
            {
                var b=pair.Value;
                if(b.Renderer!=smr || b.Mesh!=smr.sharedMesh || b.OriginalVerts==null || b.OriginalVerts.Length!=current.Length)continue;
                // TMorph may add other morph deltas on top of our baked original.
                // Subtract only the known pregnancy delta, preserving the game's morph.
                if(ComputeVertexSignature(pair.Key.m_vOriVert)!=b.AppliedSignature)continue;
                var clean=(Vector3[])current.Clone();
                for(int i=0;i<clean.Length;i++)clean[i]-=b.BakedVerts[i]-b.OriginalVerts[i];
                return clean;
            }
            return current;
        }

        static bool RestMap(SkinnedMeshRenderer source,SkinnedMeshRenderer target,out NMatrix map)
        {
            map=NMatrix.Identity;
            if(source==target)return true;
            var sb=NativeBones(source);var tb=NativeBones(target);
            var sp=NativeBinds(source);var tp=NativeBinds(target);
            var sn=Array.ConvertAll(sb,b=>b==null?null:RigBoneNames.Canonical(b.name));
            var tn=Array.ConvertAll(tb,b=>b==null?null:RigBoneNames.Canonical(b.name));
            var s=Array.ConvertAll(sp,MatrixBridge.ToManaged);var t=Array.ConvertAll(tp,MatrixBridge.ToManaged);
            if(RestSpaceMapping.TryCreate(sn,s,tn,t,out map,out _,out _,out _))return true;
            return RestSpaceMapping.TryCreateAliased(sn,s,tn,t,out map,out _,out _,out _);
        }

        static void PrepareALContext(Maid maid,List<SkinnedMeshRenderer> renderers,float stage)
        {
            _alWorking=null;
            long contextStarted=System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if(maid==null)return;
                PruneDetachedBindings(maid,renderers);
                var bodies=renderers.Where(r=>r!=null && r.sharedMesh!=null && ClassifyMesh(r)==MeshMorphClass.Body && r.sharedMesh.vertexCount>=50)
                    .OrderByDescending(r=>r.sharedMesh.vertexCount).ToArray();
                if(bodies.Length==0)throw new InvalidOperationException("No readable body mesh for growth calibration.");
                var body=bodies[0];
                var originals=new Dictionary<SkinnedMeshRenderer,Vector3[]>();
                int signature=ComputeMorphBakeSignature(stage,MeshMorphClass.Body);
                bool hasNavelAccessory=renderers.Any(r=>r!=null && ClassifyMesh(r)==MeshMorphClass.NavelAccessory);
                var topologies=new Dictionary<SkinnedMeshRenderer,int[]>();
                foreach(var r in bodies)
                {
                    var v=CleanVertices(maid,r);originals[r]=v;
                    var topology=ClothingBodyTopology(maid,r);topologies[r]=topology;
                    unchecked
                    {
                        signature=signature*31+r.GetHashCode();signature=signature*31+r.sharedMesh.GetInstanceID();
                        signature=signature*31+ComputeVertexSignature(v);signature=signature*31+MeshStructureSignature(r);
                        signature=signature*31+ExactIndices(topology);signature=signature*31+(r.enabled&&r.gameObject.activeInHierarchy?1:0);
                    }
                }
                unchecked{signature=signature*31+(hasNavelAccessory?1:0);}
                if(_alContexts.TryGetValue(maid.GetHashCode(),out var cached) && cached.Signature==signature && cached.Body==body)
                {
                    SyncContextMaps(cached,renderers);_alWorking=cached;
                    if(_refreshStats!=null)_refreshStats.ContextHits++;return;
                }
                if(_refreshStats!=null)_refreshStats.ContextBuilds++;

                var bones=NativeBones(body);var binds=NativeBinds(body);
                int pelvis=FindBoneIndex(bones,"Bip01 Pelvis_SCL_","Bip01 Pelvis");
                int waist=FindBoneIndex(bones,"Bip01 Spine_SCL_","Bip01 Spine");
                int left=FindBoneIndex(bones,"Bip01 L Thigh_SCL_","Bip01 L Thigh","Hip_L");
                int right=FindBoneIndex(bones,"Bip01 R Thigh_SCL_","Bip01 R Thigh","Hip_R");
                if(pelvis<0 || pelvis>=binds.Length)throw new InvalidOperationException("Missing COM3D2 pelvis rest landmark.");
                // Use one skeletal rest metric, independent of each mesh's storage
                // axes, origin, handedness and scale. Never use the current pose here.
                var bodyToReference=MatrixBridge.ToManaged(binds[pelvis]);
                if(!NMatrix.Invert(bodyToReference,out var referenceToBody))throw new InvalidOperationException("Singular pelvis bind pose.");
                NVector Rest(int i)
                {
                    if(i<0 || i>=binds.Length || !NMatrix.Invert(MatrixBridge.ToManaged(binds[i]),out var inverse))
                        throw new InvalidOperationException("Missing COM3D2 pelvis/waist/lateral rest landmark.");
                    return NVector.Transform(inverse.Translation,bodyToReference);
                }
                var floor=Rest(pelvis);var navel=Rest(waist);
                var up=NVector.Normalize(navel-floor);
                var lateral=Rest(right)-Rest(left);lateral-=up*NVector.Dot(lateral,up);
                var side=NVector.Normalize(lateral);var forward=NVector.Normalize(NVector.Cross(side,up));
                // A consistent front is established from skeletal rest landmarks,
                // not the character's current pose or world facing direction.
                var frame=new LocalFrame {Center=U(floor),Up=U(up),Right=U(side),Fwd=U(forward),BoneLen=NVector.Distance(floor,navel)};
                var fm=FrameMatrix(frame);
                if(!NMatrix.Invert(fm,out var inv))throw new InvalidOperationException("Degenerate COM3D2 torso frame.");
                var context=new ALContext {Maid=maid,Body=body,Signature=signature,Settings=Shape.Copy(),BodyToReference=bodyToReference,Frame=frame,FrameMatrix=fm,InverseFrame=inv,Stage=stage};
                SyncContextMaps(context,renderers);

                int chest=-1;float chestY=float.MinValue;
                for(int i=0;i<bones.Length && i<binds.Length;i++)
                {
                    string name=bones[i]==null?"":RigBoneNames.Canonical(bones[i].name);
                    if(!name.StartsWith("Bip01 Spine",StringComparison.Ordinal))continue;
                    float y=NVector.Transform(Rest(i),inv).Y;
                    if(y>chestY){chestY=y;chest=i;}
                }
                float navelY=NVector.Transform(navel,inv).Y;
                if(chest<0 || chestY<=navelY)throw new InvalidOperationException("Upper-spine rest landmark missing.");
                var points=new List<NVector>();
                foreach(var r in bodies)
                    if(r.enabled && r.gameObject.activeInHierarchy && context.Maps.TryGetValue(r,out var map))
                        AddTorsoSamples(points,r,originals[r],map*inv);
                if(points.Count<50 || points.Min(v=>v.Y)>0 || points.Max(v=>v.Y)<chestY)
                {
                    points.Clear();AddTorsoSamples(points,body,originals[body],bodyToReference*inv);
                }
                frame.Profile=TorsoProfile.Build(points,0,navelY,chestY);context.Frame=frame;
                LocateCervix(body,originals[body],bodyToReference*inv,frame.Profile);
                var axis=VirtualAxisMath.Reference(frame.Profile,stage,Shape);
                context.Axis=new VirtualAxisMath.Axis(NVector.Transform(axis.Anchor,fm),NVector.Transform(axis.Upper,fm));
                int spine=-1;float best=float.MaxValue;
                for(int i=0;i<bones.Length && i<binds.Length;i++)
                {
                    if(bones[i]==null || !RigBoneNames.Canonical(bones[i].name).StartsWith("Bip01 Spine",StringComparison.Ordinal))continue;
                    float d=NVector.DistanceSquared(context.Axis.Upper,Rest(i));if(d<best){best=d;spine=i;}
                }
                context.Pelvis=bones[pelvis];context.Spine=bones[spine];
                context.PelvisBind=referenceToBody*MatrixBridge.ToManaged(binds[pelvis]);context.SpineBind=referenceToBody*MatrixBridge.ToManaged(binds[spine]);
                context.Surface=CreateBodyAnchorContext(frame);context.Material=new MaterialBlendField(frame.Profile.Span);
                _alWorking=context;
                // Build body correspondences before any clothing, independently of slot enumeration order.
                foreach(var r in bodies)
                {
                    bool visible=r.enabled && r.gameObject.activeInHierarchy;
                    if((!visible && !hasNavelAccessory) || !context.Maps.ContainsKey(r))continue;
                    var record=new MeshRecord {SMR=r,Mesh=r.sharedMesh,OrigVerts=originals[r]};
                    if(!TryDeformAL(r,record,MeshMorphClass.Body,stage,out var morphed,out _))continue;
                    if(hasNavelAccessory)context.NavelBodies.Add(record);
                    // Hidden-body support is scoped to the accessory; clothing behavior stays unchanged.
                    if(!visible)continue;
                    AddBodyAnchorMesh(context.Surface,r,record,null,morphed,frame,topologies[r]);
                    for(int i=0;i<record.OrigVerts.Length;i++)
                    {
                        var o=NVector.Transform(N(record.OrigVerts[i]),record.ToReference);
                        context.Material.Add(o,Attachment(record,i,stage));
                    }
                }
                FinalizeBodyAnchorContext(context.Surface);
                _alContexts[maid.GetHashCode()]=context;
                _log.LogInfo(string.Format("[ALGrowth] calibrated mesh={0} floor={1} waist={2} upper={3}; span={4:F6}; stage={5:F4}; navel={6}",body.sharedMesh.name,bones[pelvis].name,bones[waist].name,bones[chest].name,frame.Profile.Span,stage,Scalar.IsFinite(frame.Profile.SkinNavelZ)));
            }
            catch(Exception e){_alWorking=null;_log.LogWarning("[ALGrowth] Calibration failed: "+e.Message);}
            finally{if(_refreshStats!=null)_refreshStats.ContextTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-contextStarted;}
        }

        static float BoneSum(BoneWeight w,Transform[] bones,Func<string,bool> predicate)
        {
            float value=0;
            void Add(int i,float weight){if(weight>0 && i>=0 && i<bones.Length && bones[i]!=null && predicate(bones[i].name.ToLowerInvariant()))value+=weight;}
            Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
            return value;
        }
        // Only calibration uses material connectivity. Nothing here runs per pose.
        static void LocateCervix(SkinnedMeshRenderer body,Vector3[] vertices,NMatrix toLocal,TorsoProfile profile)
        {
            var materials=body.sharedMaterials;var mesh=body.sharedMesh;
            if(materials==null)return;
            var local=Array.ConvertAll(vertices,v=>NVector.Transform(N(v),toLocal));
            var candidates=new List<NVector>();
            for(int sub=0;sub<Math.Min(materials.Length,mesh.subMeshCount);sub++)
            {
                if(materials[sub]==null || materials[sub].name.IndexOf("kupa",StringComparison.OrdinalIgnoreCase)<0)continue;
                var triangles=mesh.GetTriangles(sub);var parent=new int[vertices.Length];
                for(int i=0;i<parent.Length;i++)parent[i]=i;
                int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
                void Join(int a,int b){a=Root(a);b=Root(b);if(a!=b)parent[b]=a;}
                var used=new HashSet<int>(triangles);var weld=new Dictionary<WeldKey,int>();
                foreach(int i in used)
                {
                    var v=local[i]/profile.Span;
                    var key=new WeldKey(Mathf.RoundToInt(v.X*1000000),Mathf.RoundToInt(v.Y*1000000),Mathf.RoundToInt(v.Z*1000000));
                    if(weld.TryGetValue(key,out int j))Join(i,j);else weld[key]=i;
                }
                for(int i=0;i<triangles.Length;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
                foreach(var component in used.GroupBy(Root))
                {
                    var part=component.Select(i=>local[i]).ToArray();
                    if(part.Length<100)continue;
                    float top=part.Max(v=>v.Y),bottom=part.Min(v=>v.Y);
                    if(top<=profile.PelvicFloor || top>=profile.Navel || top-bottom<profile.Span*.10f)continue;
                    var cap=part.Where(v=>v.Y>=top-profile.Span*.045f).ToArray();
                    float x=(cap.Min(v=>v.X)+cap.Max(v=>v.X))*.5f,z=(cap.Min(v=>v.Z)+cap.Max(v=>v.Z))*.5f;
                    var center=cap.OrderBy(v=>(v.X-x)*(v.X-x)+(v.Z-z)*(v.Z-z)).First();
                    if(Math.Abs(center.X)<profile.Span*.03f)candidates.Add(center);
                }
            }
            if(candidates.Count==0)return;
            // The anterior upper-ended canal is distinct from the posterior tube.
            profile.Cervix=candidates.OrderByDescending(v=>v.Z).First();
            profile.HasCervix=true;profile.ShapeCache=null;
        }
        static bool IsALBellyBone(string name) => name.Contains("pelvis") || name.Contains("spine") || name.Contains("hip") || name.Contains("waist") || name.Contains("hara") || name.Contains("belly");
        static bool IsALLegBone(string name) => name.Contains("thigh") || name.Contains("calf") || name.Contains("knee") || name.Contains("momo") || name.Contains("foot");

        // COM includes arms in the same mesh as the torso. AL's upper-abdomen
        // filter switch must not turn limb ownership into abdominal ownership.
        static bool IsALArmBoneName(string name) => name.Contains("clavicle") || name.Contains("upperarm") || name.Contains("forearm") ||
            name.Contains("hand") || name.Contains("finger") || name.Contains("uppertwist") || name.Contains("foretwist") ||
            name.Contains("kata_");
        static bool IsALPeripheralBone(string name) => IsALArmBoneName(name) || name.Contains("neck") || name.Contains("head");
        static bool IsALArmHierarchy(Transform bone)
        {
            // Loose sleeve rigs may have only custom spring-bone names. Their
            // ownership still comes from the native arm hierarchy.
            for(var current=bone;current!=null;current=current.parent)
                if(IsALArmBoneName(current.name.ToLowerInvariant()))return true;
            return false;
        }
        static bool HasArmWeight(BoneWeight weight,bool[] arms)
        {
            bool Has(int i,float value)=>value>0 && i>=0 && i<arms.Length && arms[i];
            return Has(weight.boneIndex0,weight.weight0) || Has(weight.boneIndex1,weight.weight1) ||
                Has(weight.boneIndex2,weight.weight2) || Has(weight.boneIndex3,weight.weight3);
        }
        static float TorsoOwnership(BoneWeight weight,Transform[] bones)
            => BellyShape.Smooth(1-BoneSum(weight,bones,IsALPeripheralBone));
        static void AddTorsoSamples(List<NVector> points,SkinnedMeshRenderer renderer,Vector3[] vertices,NMatrix toLocal)
        {
            var weights=NativeWeights(renderer);var bones=NativeBones(renderer);
            if(weights.Length!=vertices.Length)return;
            var arms=Array.ConvertAll(bones,IsALArmHierarchy);
            for(int i=0;i<vertices.Length;i++)
                if(!HasArmWeight(weights[i],arms) && TorsoOwnership(weights[i],bones)>=.999f &&
                    BellyShape.BoneInfluence(BoneSum(weights[i],bones,IsALBellyBone),BoneSum(weights[i],bones,IsALLegBone))>=.95f)
                    points.Add(NVector.Transform(N(vertices[i]),toLocal));
        }

        static bool TryDeformAL(SkinnedMeshRenderer smr,MeshRecord rec,MeshMorphClass kind,float stage,out Vector3[] result,out DeformStats stats)
        {
            if(kind==MeshMorphClass.NavelAccessory)return TryDeformNavelAccessory(smr,rec,stage,out result,out stats);
            result=null;stats=new DeformStats();
            var context=_alWorking;
            if(context==null || rec.OrigVerts==null || !context.Maps.TryGetValue(smr,out var map) || !NMatrix.Invert(map,out var inverse))return false;
            var weights=NativeWeights(smr);var bones=NativeBones(smr);
            rec.Skirt=null;
            rec.ClothingMotion=null;
            rec.RestHits=null;rec.RestHitState=null;
            bool cloth=kind!=MeshMorphClass.Body;
            int count=rec.OrigVerts.Length;
            var lowerLegExcluded=cloth?BuildLowerLegClothExclusion(weights,bones,count):null;
            rec.GrowthContext=context;rec.ToReference=map;
            rec.ThighGuardRestore=BuildThighGuard(rec,weights,bones,context);
            rec.BellyInfluence=cloth?null:new float[count];
            rec.TorsoOwnership=new float[count];
            var breast=new float[count];
            var armBones=Array.ConvertAll(bones,IsALArmHierarchy);var armExcluded=new bool[count];
            for(int i=0;i<count && i<weights.Length;i++)
            {
                breast[i]=BoneSum(weights[i],bones,IsBreastBoneName);
                rec.TorsoOwnership[i]=TorsoOwnership(weights[i],bones);
                // Exclusion persists through surface matching, repair, virtual
                // attachment and motion binding, not just the first shape pass.
                if(cloth && lowerLegExcluded[i])rec.TorsoOwnership[i]=0;
                armExcluded[i]=HasArmWeight(weights[i],armBones);
                if(!cloth)rec.BellyInfluence[i]=BellyShape.BoneInfluence(BoneSum(weights[i],bones,IsALBellyBone),BoneSum(weights[i],bones,IsALLegBone));
            }
            var weldGroups=ComputeNormalWeldGroup(rec.OrigVerts);
            rec.BreastExcluded=BreastExclusion.Build(breast,count,weldGroups,Shape.BreastExclusionEnabled);
            BreastExclusion.ShareWelds(armExcluded,weldGroups);
            for(int i=0;i<count;i++)if(armExcluded[i])rec.TorsoOwnership[i]=0;
            result=(Vector3[])rec.OrigVerts.Clone();
            var original=new Vector3[count];var changed=new Vector3[count];var moved=new bool[count];
            var breastSurface=cloth && Shape.BreastExclusionEnabled?new bool[count]:null;
            var breastTargets=breastSurface==null?null:new Vector3[count];
            var profile=context.Frame.Profile;
            for(int i=0;i<count;i++)
            {
                var reference=NVector.Transform(N(rec.OrigVerts[i]),map);
                original[i]=changed[i]=U(reference);
                bool hangingBreastCloth=false;
                if(kind==MeshMorphClass.InnerCloth && Shape.BreastExclusionEnabled &&
                    TryFindRecordRestSurface(rec,i,U(reference),out var innerHit))
                {
                    var triangle=context.Surface.ClothingRest.Triangles[innerHit.TriangleIndex];
                    var body=context.Surface.Meshes[triangle.MeshIndex];
                    bool touchesBreast=body.BreastExcluded[triangle.A] || body.BreastExcluded[triangle.B] || body.BreastExcluded[triangle.C];
                    hangingBreastCloth=rec.BreastExcluded[i] && !touchesBreast;
                    rec.BreastExcluded[i]=false;
                    if(touchesBreast && TryGetBreastClothingTarget(context.Surface,U(reference),Shape.ClothOffset,innerHit,out var target,out var excluded))
                    {
                        breastSurface[i]=true;breastTargets[i]=target;rec.BreastExcluded[i]=excluded;
                    }
                }
                else if(cloth && Shape.BreastExclusionEnabled && TryFindNearestBodyAnchorPoint(context.Surface,U(reference),false,out var nearest))
                {
                    var b=context.Surface.Meshes[nearest.MeshIndex];
                    if((nearest.Original-U(reference)).sqrMagnitude<=profile.Span*profile.Span*.04f)
                    {
                        bool onBreast=BreastExclusion.Contains(b.BreastExcluded,nearest.VertexIndex);
                        // Bra frills can keep breast animation weights while
                        // hanging in front of the abdomen. Their rest-body
                        // attachment identifies fabric, not breast skin/cups.
                        hangingBreastCloth=kind==MeshMorphClass.InnerCloth && rec.BreastExcluded[i] && !onBreast;
                        if(hangingBreastCloth)rec.BreastExcluded[i]=false;
                        else if(onBreast)rec.BreastExcluded[i]=true;
                        // Resolve only fabric frozen by the breast exclusion.
                        // Already released inner frills keep their restored path.
                        if(rec.BreastExcluded[i] && TryGetBreastClothingTarget(context.Surface,U(reference),
                            profile.Span*.2f,Shape.ClothOffset,out var breastTarget,out var surfaceExcluded))
                        {
                            breastSurface[i]=true;
                            breastTargets[i]=breastTarget;
                            rec.BreastExcluded[i]=surfaceExcluded;
                        }
                    }
                }
                if(rec.BreastExcluded[i] || rec.TorsoOwnership[i]<=0)continue;
                var local=NVector.Transform(reference,context.InverseFrame);
                float influence=BellyShape.DeformationInfluence(cloth?1:rec.BellyInfluence[i],local.Y,profile,Shape);
                if(influence<=0)continue;
                var shape=kind==MeshMorphClass.OuterCloth || hangingBreastCloth?BellyShape.DeformOuterCloth(local,profile,stage,Shape):BellyShape.Deform(local,profile,stage,Shape);
                if(!Scalar.IsFinite(shape.X) || !Scalar.IsFinite(shape.Y) || !Scalar.IsFinite(shape.Z))
                    throw new ArithmeticException("Shape parameters produced a non-finite vertex; geometry was not applied.");
                var delta=NVector.TransformNormal(shape-local,context.FrameMatrix)*influence;
                if(cloth)delta*=Shape.ClothOffset;
                changed[i]=U(reference+delta);
                stats.MaskedVerts++;
            }
            if(cloth)
            {
                for(int i=0;i<count;i++)
                {
                    if(rec.BreastExcluded[i] || rec.TorsoOwnership[i]<=0)continue;
                    var local=NVector.Transform(N(original[i]),context.InverseFrame);
                    float attachment=BellyShape.ClothingFootprint(local,profile,stage,Shape);
                    if(breastSurface!=null && breastSurface[i])
                        changed[i]=breastTargets[i];
                    else if(attachment>0 && TryGetBodySurfaceClothTarget(context.Surface,original[i],Shape.ClothOffset,context.Frame,out var target))
                        changed[i]=Vector3.Lerp(changed[i],target,attachment);
                    moved[i]=(changed[i]-original[i]).sqrMagnitude>=1e-14f;
                }
                rec.Neighbors=rec.Neighbors??BuildMeshNeighbors(rec.Mesh,count);
                RepairClothDistortion(original,changed,moved,context.Frame,rec.Neighbors);
            }
            ApplyThighGuard(rec,original,changed);
            for(int i=0;i<count;i++)
            {
                if(rec.BreastExcluded[i] || rec.TorsoOwnership[i]<=0)continue;
                // Apply the continuous ownership boundary after clothing repair too,
                // so a neighboring sleeve/hand cannot acquire propagated motion.
                var ownedDelta=(changed[i]-original[i])*rec.TorsoOwnership[i];
                float distance=ownedDelta.magnitude;
                if(distance<1e-7f)continue;
                result[i]=U(NVector.Transform(N(original[i]+ownedDelta),inverse));
                stats.NonZeroVerts++;stats.EllipsoidVerts++;stats.MaxDelta=Mathf.Max(stats.MaxDelta,distance);stats.MaxStrength=1;
            }
            rec.LastNewV=result;
            if(kind==MeshMorphClass.InnerCloth)rec.ClothingMotion=BuildClothingMotion(rec,result,stage);
            if(kind==MeshMorphClass.OuterCloth)PrepareSkirtDrape(rec,result,stage);
            return true;
        }

        static float Attachment(MeshRecord record,int i,float stage)
        {
            if(record.BreastExcluded[i] || record.TorsoOwnership[i]<=0)return 0;
            var c=record.GrowthContext;
            var original=NVector.Transform(N(record.OrigVerts[i]),record.ToReference);
            var changed=NVector.Transform(N(record.Skirt==null?record.LastNewV[i]:record.Skirt.VisualVertices[i]),record.ToReference);
            var material=NVector.Transform(original,c.InverseFrame);
            float attachment=VirtualAxisMath.MaterialSurfaceWeight(NVector.Distance(original,changed),material,c.Frame.Profile,stage,c.Settings,
                BellyShape.DeformationInfluence(record.BellyInfluence==null?1:record.BellyInfluence[i],material.Y,c.Frame.Profile,c.Settings)*record.TorsoOwnership[i]);
            return record.ThighGuardRestore==null ? attachment : attachment*(1f-record.ThighGuardRestore[i]);
        }
        public static string GetALNavelStatus(Maid maid)
        {
            if(maid==null || !_alContexts.TryGetValue(maid.GetHashCode(),out var c))return "Apply Belly to calibrate original navel";
            return Scalar.IsFinite(c.Frame.Profile.SkinNavelZ)?"Original mesh navel detected":"No reliable original navel; navel geometry skipped";
        }
        public static string GetALAnchorStatus(Maid maid)
        {
            if(maid==null || !_alContexts.TryGetValue(maid.GetHashCode(),out var c))return "Apply Belly to locate the cervical opening";
            return c.Frame.Profile.HasCervix ? "Cervix detected: early anchor, late forward growth" : "No cervix surface detected: using torso landmarks";
        }
    }
}
