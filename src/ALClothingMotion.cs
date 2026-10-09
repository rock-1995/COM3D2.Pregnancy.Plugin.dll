using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using V=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
using M=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        // Rest-surface correspondences are built only on a shape rebuild.
        // At runtime each shared support vertex and face is evaluated once;
        // no clothing vertex scan, spatial search or mesh vertex upload.
        sealed class ClothingMotionPlan
        {
            public int[] VertexFaces;
            public float[] Blend;
            public ClothingSupport[] Supports;
            public ClothingFace[] Faces;
            public M[] Transforms;
            public M ToReference;
            public M VirtualPose;
            public bool PoseReady;
        }
        sealed class ClothingSupport
        {
            public BodyAnchorMesh Body;
            public int[] Indices;
            public BoneWeight[] Weights;
            public Transform[] Bones;
            public Matrix4x4[] Binds;
            public M ToBody;
            public M[] Native;
            public int[] NativeTicks;
            public int Tick;
            public V[] Posed;
            public int[] UsedBones;
            public Matrix4x4[] BonePoses;
        }
        sealed class ClothingFace
        {
            public int Support,A,B,C;
            public M Inverse;
        }
        readonly record struct ClothingMotionRecipe(int SourceSlot,int Face,float Blend);

        static bool ClothingTriangleFrame(V a,V b,V c,out M frame)
        {
            var x=b-a;var y=c-a;var n=V.Cross(x,y);
            float area=n.Length();
            frame=M.Identity;
            if(!Scalar.IsFinite(area) || area<=1e-12f)return false;
            // Normal thickness scales with the geometric mean of the two
            // in-plane stretches, while the plane itself follows exactly.
            n/=Scalar.Sqrt(area);
            frame=new M(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,n.X,n.Y,n.Z,0,a.X,a.Y,a.Z,1);
            return true;
        }

        static ClothingMotionPlan BuildClothingMotion(MeshRecord record,Vector3[] deformed,float stage)
        {
            if(stage<=0 || Shape.VirtualAxisStrength<=0)return null;
            var context=record.GrowthContext;int count=deformed.Length;
            var plan=new ClothingMotionPlan {VertexFaces=Enumerable.Repeat(-1,count).ToArray(),Blend=new float[count],ToReference=record.ToReference};
            var faces=new List<ClothingFace>();var faceSlots=new Dictionary<int,int>();
            var supportSlots=new Dictionary<int,int>();var supports=new List<ClothingSupport>();var indices=new List<List<int>>();
            var weights=NativeWeights(record.SMR);var bones=NativeBones(record.SMR);
            if(weights.Length!=count)return null;
            for(int i=0;i<count;i++)
            {
                if(record.BreastExcluded[i] || record.TorsoOwnership[i]<=0 || (deformed[i]-record.OrigVerts[i]).sqrMagnitude<1e-14f)continue;
                // Native skirt motion belongs to the skirt solver, per vertex.
                if(Influences(weights[i]).Any(w=>w.Weight>0 && IsDrapeBone(bones[w.Bone])))continue;
                var original=V.Transform(N(record.OrigVerts[i]),record.ToReference);
                float blend=BellyShape.ClothingFootprint(V.Transform(original,context.InverseFrame),context.Frame.Profile,stage,Shape)*record.TorsoOwnership[i];
                if(record.ThighGuardRestore!=null)blend*=1-record.ThighGuardRestore[i];
                if(blend<=0 || !TryFindRecordRestSurface(record,i,U(original),out var hit))continue;
                var triangle=context.Surface.ClothingRest.Triangles[hit.TriangleIndex];
                var body=context.Surface.Meshes[triangle.MeshIndex];
                if(!body.Affected[triangle.A] && !body.Affected[triangle.B] && !body.Affected[triangle.C])continue;
                if(body.BreastExcluded[triangle.A] || body.BreastExcluded[triangle.B] || body.BreastExcluded[triangle.C])
                    blend=record.TorsoOwnership[i]*(record.ThighGuardRestore==null?1:1-record.ThighGuardRestore[i]);
                if(!faceSlots.TryGetValue(hit.TriangleIndex,out int face))
                {
                    if(!ClothingTriangleFrame(N(body.Morphed[triangle.A]),N(body.Morphed[triangle.B]),N(body.Morphed[triangle.C]),out var frame) || !M.Invert(frame,out var inverse))continue;
                    if(!supportSlots.TryGetValue(triangle.MeshIndex,out int support))
                    {
                        support=supports.Count;supportSlots.Add(triangle.MeshIndex,support);indices.Add(new List<int>());
                        var source=body.Source;var nativeBones=NativeBones(source.SMR);var nativeBinds=NativeBinds(source.SMR);
                        if(!M.Invert(source.ToReference,out var toBody))throw new InvalidOperationException("Singular clothing support mapping.");
                        supports.Add(new ClothingSupport {Body=body,Bones=nativeBones,Binds=nativeBinds,ToBody=toBody,Native=new M[nativeBones.Length],NativeTicks=new int[nativeBones.Length]});
                    }
                    int Slot(int index){int found=indices[support].IndexOf(index);if(found>=0)return found;indices[support].Add(index);return indices[support].Count-1;}
                    face=faces.Count;faceSlots.Add(hit.TriangleIndex,face);
                    faces.Add(new ClothingFace {Support=support,A=Slot(triangle.A),B=Slot(triangle.B),C=Slot(triangle.C),Inverse=inverse});
                }
                plan.VertexFaces[i]=face;plan.Blend[i]=blend;
            }
            if(faces.Count==0)return null;
            for(int i=0;i<supports.Count;i++)
            {
                var support=supports[i];support.Indices=indices[i].ToArray();support.Posed=new V[support.Indices.Length];
                var native=NativeWeights(support.Body.Source.SMR);support.Weights=support.Indices.Select(j=>native[j]).ToArray();
                support.UsedBones=support.Weights.SelectMany(Influences).Where(w=>w.Weight>0).Select(w=>w.Bone).Distinct().ToArray();
                support.BonePoses=new Matrix4x4[support.Bones.Length];
            }
            plan.Faces=faces.ToArray();plan.Supports=supports.ToArray();plan.Transforms=new M[faces.Count];return plan;
        }

        static void AttachClothingMotion(ALBinding binding,ClothingMotionPlan plan,BoneWeight[] packed,Vector3[] deformed,int firstSlot)
        {
            if(plan==null)return;
            var recipes=new List<ClothingMotionRecipe>();var slots=new Dictionary<ClothingMotionRecipe,int>();var boxes=new SkinBounds.Box[plan.Faces.Length];
            int Slot(ClothingMotionRecipe r)
            {
                if(!slots.TryGetValue(r,out int slot)){slot=recipes.Count;slots.Add(r,slot);recipes.Add(r);}
                return firstSlot+slot;
            }
            for(int i=0;i<packed.Length;i++)
            {
                int face=plan.VertexFaces[i];if(face<0)continue;float blend=plan.Blend[i];
                boxes[face].Include(N(deformed[i]));
                if(blend>=1)packed[i]=new BoneWeight {boneIndex0=Slot(new ClothingMotionRecipe(-1,face,1)),weight0=1};
                else
                {
                    // The same exact four-slot fusion used by VirtualWeights:
                    // retain three sources and fuse only the weakest source
                    // with the surface, rather than duplicating every source.
                    var source=Influences(packed[i]).Where(w=>w.Weight>0).OrderByDescending(w=>w.Weight).ThenBy(w=>w.Bone).ToArray();
                    var values=new List<VirtualWeights.Influence>();
                    int kept=Math.Min(3,source.Length);
                    for(int k=0;k<kept;k++)values.Add(new VirtualWeights.Influence(source[k].Bone,source[k].Weight*(1-blend)));
                    float weak=source.Length==4?source[3].Weight*(1-blend):0;
                    values.Add(new VirtualWeights.Influence(Slot(new ClothingMotionRecipe(weak>0?source[3].Bone:-1,face,blend/(weak+blend))),weak+blend));
                    packed[i]=Pack(values.ToArray());
                }
            }
            binding.ClothingMotion=plan;binding.ClothingRecipes=recipes.ToArray();binding.ClothingBoxes=boxes;
        }

        static bool UpdateClothingMotion(ALBinding binding,M virtualTransform,M carrier,M inverseCarrier,Func<int,M> native,int regularRecipes)
        {
            var plan=binding.ClothingMotion;if(plan==null)return false;
            bool dirty=!plan.PoseReady || !SameMatrix(plan.VirtualPose,virtualTransform);
            foreach(var support in plan.Supports)
                foreach(int bone in support.UsedBones)
                {
                    if(support.Bones[bone]==null)throw new InvalidOperationException("Clothing support bone missing.");
                    var pose=support.Bones[bone].localToWorldMatrix;
                    if(!SameMatrix(MatrixBridge.ToManaged(support.BonePoses[bone]),MatrixBridge.ToManaged(pose)))dirty=true;
                    support.BonePoses[bone]=pose;
                }
            if(dirty)
            {
            foreach(var support in plan.Supports)
            {
                support.Tick++;
                for(int k=0;k<support.Indices.Length;k++)
                {
                    int index=support.Indices[k];var point=N(support.Body.Morphed[index]);var value=V.Zero;var weights=support.Weights[k];
                    void Add(int bone,float weight)
                    {
                        if(weight<=0)return;
                        if(support.Bones[bone]==null)throw new InvalidOperationException("Clothing support bone missing.");
                        if(support.NativeTicks[bone]!=support.Tick)
                        {
                            support.Native[bone]=support.ToBody*MatrixBridge.ToManaged(support.Binds[bone])*MatrixBridge.ToManaged(support.BonePoses[bone]);
                            support.NativeTicks[bone]=support.Tick;
                        }
                        value+=V.Transform(point,support.Native[bone])*weight;
                    }
                    Add(weights.boneIndex0,weights.weight0);Add(weights.boneIndex1,weights.weight1);Add(weights.boneIndex2,weights.weight2);Add(weights.boneIndex3,weights.weight3);
                    float alpha=support.Body.AttachmentWeights[index];support.Posed[k]=value*(1-alpha)+V.Transform(point,virtualTransform)*alpha;
                }
            }
            for(int i=0;i<plan.Faces.Length;i++)
            {
                var face=plan.Faces[i];var points=plan.Supports[face.Support].Posed;
                if(!ClothingTriangleFrame(points[face.A],points[face.B],points[face.C],out var frame))throw new InvalidOperationException("Collapsed clothing support face.");
                plan.Transforms[i]=plan.ToReference*face.Inverse*frame;
            }
            plan.VirtualPose=virtualTransform;plan.PoseReady=true;
            }
            bool changed=false;int start=binding.Binds.Length+regularRecipes;
            for(int i=0;i<binding.ClothingRecipes.Length;i++)
            {
                var r=binding.ClothingRecipes[i];var transform=plan.Transforms[r.Face];
                if(r.Blend<1)
                {
                    var source=r.SourceSlot<binding.Binds.Length?native(r.SourceSlot):MatrixBridge.ToManaged(binding.Palette[r.SourceSlot])*carrier;
                    transform=M.Lerp(source,transform,r.Blend);
                }
                var matrix=transform*inverseCarrier;
                if(!VirtualAxisMath.Finite(matrix))throw new InvalidOperationException("Nonfinite clothing surface matrix.");
                if(!SameMatrix(MatrixBridge.ToManaged(binding.Palette[start+i]),matrix))changed=true;
                binding.Palette[start+i]=MatrixBridge.ToUnity(matrix);
            }
            return changed;
        }
    }
}
