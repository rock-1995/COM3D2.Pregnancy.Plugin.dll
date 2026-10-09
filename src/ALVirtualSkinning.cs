using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NMatrix=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;
using NVector=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        sealed class ALBinding
        {
            public Maid Maid;
            public Mesh Mesh;
            public SkinnedMeshRenderer Renderer;
            public Transform[] Bones;
            public BoneWeight[] Weights;
            public Matrix4x4[] Binds, Palette;
            public VirtualWeights.Recipe[] Recipes;
            public NMatrix ToReference;
            public ALContext Context;
            public SkinBounds Envelope;
            public NMatrix[] NativeMatrices;
            public int[] NativeTicks;
            public int Tick;
            public Bounds Bounds;
            public SkinQuality Quality;
            public bool UpdateWhenOffscreen;
            public NavelAttachment Navel;
            public SkirtDrapePlan Skirt;
            public SkirtRecipe[] SkirtRecipes;
            public NMatrix[] SkirtFreeMatrices;
            public Matrix4x4[] SkirtBonePose;
            public NMatrix SkirtCarrier, SkirtVirtual, SkirtBoundsRoot;
            public bool SkirtPoseReady;
            public SkinBounds.Box NavelBox;
            public ClothingMotionPlan ClothingMotion;
            public ClothingMotionRecipe[] ClothingRecipes;
            public SkinBounds.Box[] ClothingBoxes;
            public void Restore()
            {
                if(Mesh!=null){Mesh.boneWeights=Weights;Mesh.bindposes=Binds;}
                if(Renderer!=null && Renderer.sharedMesh==Mesh)
                {Renderer.bones=Bones;Renderer.localBounds=Bounds;Renderer.quality=Quality;Renderer.updateWhenOffscreen=UpdateWhenOffscreen;}
            }
        }
        static readonly Dictionary<SkinnedMeshRenderer,ALBinding> _alBindings=new Dictionary<SkinnedMeshRenderer,ALBinding>();
        static ALBinding ActiveBinding(SkinnedMeshRenderer r)
            => r!=null && _alBindings.TryGetValue(r,out var b) && b.Mesh==r.sharedMesh ? b : null;
        static Transform[] NativeBones(SkinnedMeshRenderer r) => ActiveBinding(r)?.Bones ?? r.bones;
        static Matrix4x4[] NativeBinds(SkinnedMeshRenderer r) => ActiveBinding(r)?.Binds ?? r.sharedMesh.bindposes;
        static BoneWeight[] NativeWeights(SkinnedMeshRenderer r) => ActiveBinding(r)?.Weights ?? r.sharedMesh.boneWeights;

        static void ReleaseALBinding(SkinnedMeshRenderer renderer)
        {
            if(!_alBindings.TryGetValue(renderer,out var binding))return;
            try{binding.Restore();}
            catch(Exception e){_log.LogWarning("[ALGrowth] Binding restore: "+e.Message);}
            finally{_alBindings.Remove(renderer);}
        }
        static void ReleaseALBindings(Maid maid)
        {
            foreach(var pair in _alBindings.ToArray())
                if(pair.Value.Maid==maid)ReleaseALBinding(pair.Key);
        }
        static VirtualWeights.Influence[] Influences(BoneWeight b) => new[] {
            new VirtualWeights.Influence(b.boneIndex0,b.weight0),new VirtualWeights.Influence(b.boneIndex1,b.weight1),
            new VirtualWeights.Influence(b.boneIndex2,b.weight2),new VirtualWeights.Influence(b.boneIndex3,b.weight3)};
        static BoneWeight Pack(VirtualWeights.Influence[] values)
        {
            var b=new BoneWeight();
            for(int k=0;k<values.Length;k++)
                switch(k){case 0:b.boneIndex0=values[k].Bone;b.weight0=values[k].Weight;break;case 1:b.boneIndex1=values[k].Bone;b.weight1=values[k].Weight;break;case 2:b.boneIndex2=values[k].Bone;b.weight2=values[k].Weight;break;case 3:b.boneIndex3=values[k].Bone;b.weight3=values[k].Weight;break;}
            return b;
        }
        static void InstallALBinding(Maid maid,MeshRecord record,Vector3[] deformed,float stage)
        {
            var renderer=record.SMR;
            ReleaseALBinding(renderer);
            if(record.Navel!=null){InstallNavelBinding(maid,record,deformed,stage);return;}
            var c=record.GrowthContext;
            if(c==null || stage<=0 || (Shape.VirtualAxisStrength<=0 && record.Skirt==null) || renderer==null || renderer.sharedMesh!=record.Mesh)return;
            try
            {
                int count=record.OrigVerts.Length;
                var alpha=new float[count];
                for(int i=0;i<count;i++)
                {
                    alpha[i]=Attachment(record,i,stage);
                    if(record.BellyInfluence==null && alpha[i]>0)
                    {
                        alpha[i]=c.Material.Sample(NVector.Transform(N(record.OrigVerts[i]),record.ToReference),alpha[i]);
                        if(record.ThighGuardRestore!=null)alpha[i]*=1f-record.ThighGuardRestore[i];
                    }
                    if(record.BreastExcluded[i])alpha[i]=0;
                    if(Shape.VirtualAxisStrength<=0)alpha[i]=0;
                }
                if(record.Skirt==null && record.ClothingMotion==null && !alpha.Any(a=>a>1e-6f))return;
                var b=new ALBinding {Maid=maid,Mesh=record.Mesh,Renderer=renderer,Bones=renderer.bones,Weights=record.Mesh.boneWeights,
                    Binds=record.Mesh.bindposes,Context=c,ToReference=record.ToReference,Bounds=renderer.localBounds,Quality=renderer.quality,UpdateWhenOffscreen=renderer.updateWhenOffscreen,Skirt=record.Skirt};
                if(b.Weights.Length!=count || b.Bones.Length!=b.Binds.Length)return;
                int index=b.Bones.Length;
                var recipes=new List<VirtualWeights.Recipe>();var slots=new Dictionary<VirtualWeights.Recipe,int>();
                int Slot(VirtualWeights.Recipe r){if(!slots.TryGetValue(r,out int slot)){slot=index+recipes.Count;recipes.Add(r);slots[r]=slot;}return slot;}
                var skirtRecipes=new List<SkirtRecipe>();var skirtSlots=new Dictionary<SkirtRecipe,int>();
                int SkirtSlot(SkirtRecipe r)
                {
                    if(r.Bone>=0 && !b.Skirt.Controlled[r.Bone] && r.Alpha==0)return r.Bone;
                    if(r.Free==1)r=new SkirtRecipe(r.Bone,0,1);
                    if(!skirtSlots.TryGetValue(r,out int slot)){slot=index+skirtRecipes.Count;skirtRecipes.Add(r);skirtSlots[r]=slot;}return slot;
                }
                var packed=new BoneWeight[count];var native=new VirtualWeights.Influence[count][];
                for(int i=0;i<count;i++)
                {
                    native[i]=Influences(b.Weights[i]);
                    if(b.Skirt!=null && (record.BreastExcluded[i] || record.TorsoOwnership[i]<=0))
                    {packed[i]=b.Weights[i];continue;}
                    packed[i]=Pack(b.Skirt==null?VirtualWeights.Blend(native[i],alpha[i],Slot):BlendSkirtWeights(native[i],alpha[i],b.Skirt.Free[i],SkirtSlot));
                }
                b.Recipes=recipes.ToArray();
                b.SkirtRecipes=skirtRecipes.ToArray();
                int recipeCount=b.Skirt==null?recipes.Count:skirtRecipes.Count;
                if(b.Skirt!=null){b.SkirtFreeMatrices=new NMatrix[index];b.SkirtBonePose=new Matrix4x4[index];}
                b.NativeMatrices=new NMatrix[index];b.NativeTicks=new int[index];
                AttachClothingMotion(b,record.ClothingMotion,packed,deformed,index+recipeCount);
                int clothingCount=b.ClothingRecipes?.Length??0;
                b.Palette=b.Binds.Concat(Enumerable.Repeat(Matrix4x4.identity,recipeCount+clothingCount)).ToArray();
                b.Envelope=SkinBounds.Build(Array.ConvertAll(deformed,N),native,alpha,index);
                // Skirt recipes may retain native motion even when the ordinary
                // abdominal alpha is one. Include those native boxes conservatively.
                if(b.Skirt!=null)
                    for(int i=0;i<count;i++)foreach(var w in native[i])
                        if(w.Weight>0)b.Envelope.Native[w.Bone].Include(N(deformed[i]));
                // Restoration is registered before the first Unity mutation.
                _alBindings[renderer]=b;
                renderer.bones=b.Bones.Concat(Enumerable.Repeat(c.Pelvis,recipeCount+clothingCount)).ToArray();
                b.Mesh.bindposes=b.Palette;b.Mesh.boneWeights=packed;
                renderer.quality=SkinQuality.Bone4;
                renderer.updateWhenOffscreen=true;
                EnsureMonitor(maid);
                UpdateALBinding(b);
                if(IsDebugMeshLoggingEnabled())_log.LogInfo("[ALGrowth] virtual binding "+b.Mesh.name+" slots="+recipeCount+" vertices="+alpha.Count(a=>a>0));
            }
            catch(Exception e){ReleaseALBinding(renderer);_log.LogWarning("[ALGrowth] Binding failed, native binding restored: "+e.Message);}
        }
        static void UpdateALBinding(ALBinding b)
        {
            if(b.Navel!=null){UpdateNavelBinding(b);return;}
            var c=b.Context;
            if(c.Pelvis==null || c.Spine==null)throw new InvalidOperationException("Virtual axis bone destroyed.");
            var carrier=MatrixBridge.ToManaged(c.Pelvis.localToWorldMatrix);
            if(!NMatrix.Invert(carrier,out var inverseCarrier))throw new InvalidOperationException("Singular virtual carrier.");
            var virtualTransform=VirtualAxisMath.Evaluate(c.Axis,c.PelvisBind*carrier,c.SpineBind*MatrixBridge.ToManaged(c.Spine.localToWorldMatrix),Shape).Transform;
            var virtualMesh=b.ToReference*virtualTransform;
            var boundsRoot=b.Renderer.rootBone!=null?b.Renderer.rootBone:b.Renderer.transform;
            var rootInverse=MatrixBridge.ToManaged(boundsRoot.worldToLocalMatrix);
            if(b.Skirt!=null)
            {
                // Both game hooks can observe the same final pose. Cache actual
                // inputs, not frame numbers: late IK/physics changes in the same
                // frame must still refresh. Vertex/chain scans stay on rebuilds.
                bool dirty=!b.SkirtPoseReady || !SameMatrix(b.SkirtCarrier,carrier) ||
                    !SameMatrix(b.SkirtVirtual,virtualTransform) || !SameMatrix(b.SkirtBoundsRoot,rootInverse);
                for(int i=0;i<b.Bones.Length;i++)
                {
                    // Unity palettes may keep unresolved optional bone slots.
                    // The skirt envelope includes every positive native influence;
                    // slots outside it cannot affect this mesh or any recipe.
                    if(!b.Envelope.Native[i].Valid)continue;
                    if(b.Bones[i]==null)throw new InvalidOperationException("Native skin bone missing.");
                    var pose=b.Bones[i].localToWorldMatrix;
                    if(!SameMatrix(MatrixBridge.ToManaged(b.SkirtBonePose[i]),MatrixBridge.ToManaged(pose)))dirty=true;
                    b.SkirtBonePose[i]=pose;
                }
                if(!dirty)return;
                b.SkirtCarrier=carrier;b.SkirtVirtual=virtualTransform;b.SkirtBoundsRoot=rootInverse;
                b.SkirtPoseReady=false;
            }
            b.Tick++;
            NMatrix Native(int i)
            {
                if(i<0 || i>=b.Bones.Length || b.Bones[i]==null)throw new InvalidOperationException("Native skin bone missing.");
                if(b.NativeTicks[i]!=b.Tick)
                {
                    var matrix=MatrixBridge.ToManaged(b.Binds[i])*MatrixBridge.ToManaged(b.Skirt==null?b.Bones[i].localToWorldMatrix:b.SkirtBonePose[i]);
                    if(b.Skirt!=null && b.Skirt.Controlled[i])
                    {
                        // Offset is carried by the same torso frame as the belly;
                        // the linear part remains the unmodified native skirt pose.
                        var offset=NVector.TransformNormal(b.Skirt.Offsets[i],virtualTransform);
                        var freeMatrix=matrix;freeMatrix.M41+=offset.X;freeMatrix.M42+=offset.Y;freeMatrix.M43+=offset.Z;
                        b.SkirtFreeMatrices[i]=freeMatrix;
                        offset-=NVector.TransformNormal(b.Skirt.BindOffsets[i],matrix);
                        matrix.M41+=offset.X;matrix.M42+=offset.Y;matrix.M43+=offset.Z;
                    }
                    else if(b.Skirt!=null)b.SkirtFreeMatrices[i]=matrix;
                    b.NativeMatrices[i]=matrix;b.NativeTicks[i]=b.Tick;
                }
                return b.NativeMatrices[i];
            }
            bool changed=false;
            int recipeCount=b.Skirt==null?b.Recipes.Length:b.SkirtRecipes.Length;
            for(int i=0;i<recipeCount;i++)
            {
                NMatrix transform;
                if(b.Skirt!=null)
                {
                    var recipe=b.SkirtRecipes[i];
                    if(recipe.Bone<0)transform=virtualMesh;
                    else
                    {
                        var native=Native(recipe.Bone);
                        transform=NMatrix.Lerp(NMatrix.Lerp(native,virtualMesh,recipe.Alpha),b.SkirtFreeMatrices[recipe.Bone],recipe.Free);
                    }
                }
                else
                {
                    var recipe=b.Recipes[i];transform=recipe.Bone<0?virtualMesh:NMatrix.Lerp(Native(recipe.Bone),virtualMesh,recipe.VirtualShare);
                }
                var matrix=transform*inverseCarrier;
                if(!VirtualAxisMath.Finite(matrix))throw new InvalidOperationException("Nonfinite virtual matrix.");
                var u=MatrixBridge.ToUnity(matrix);int slot=b.Binds.Length+i;
                if(!SameMatrix(MatrixBridge.ToManaged(b.Palette[slot]),matrix))changed=true;
                b.Palette[slot]=u;
            }
            changed|=UpdateClothingMotion(b,virtualTransform,carrier,inverseCarrier,Native,recipeCount);
            if(changed)b.Mesh.bindposes=b.Palette;
            var box=b.Envelope.Evaluate(Native,virtualMesh);
            if(b.Skirt!=null)box.Include(b.Envelope.Evaluate(i=>{Native(i);return b.SkirtFreeMatrices[i];},virtualMesh));
            if(b.ClothingMotion!=null)
                for(int i=0;i<b.ClothingBoxes.Length;i++)box.Include(b.ClothingBoxes[i].Transform(b.ClothingMotion.Transforms[i]));
            if(!box.Valid)throw new InvalidOperationException("Empty virtual bounds.");
            // This Unity version exposes only localBounds for skinned renderers.
            // Convert the same AL world enclosure into the root-bone frame.
            var local=box.Transform(rootInverse);
            var size=U(local.Max-local.Min);var bounds=new Bounds(U((local.Min+local.Max)*.5f),size);
            bounds.Expand(Mathf.Max(.001f,size.magnitude*.01f));b.Renderer.localBounds=bounds;
            if(b.Skirt!=null)b.SkirtPoseReady=true;
        }
        static bool SameMatrix(NMatrix a,NMatrix b) =>
            a.M11==b.M11 && a.M12==b.M12 && a.M13==b.M13 && a.M14==b.M14 &&
            a.M21==b.M21 && a.M22==b.M22 && a.M23==b.M23 && a.M24==b.M24 &&
            a.M31==b.M31 && a.M32==b.M32 && a.M33==b.M33 && a.M34==b.M34 &&
            a.M41==b.M41 && a.M42==b.M42 && a.M43==b.M43 && a.M44==b.M44;
        static void UpdateALBindings(Maid maid)
        {
            foreach(var pair in _alBindings.ToArray())
            {
                var b=pair.Value;if(b.Maid!=maid)continue;
                if(b.Renderer==null || b.Mesh==null || b.Renderer.sharedMesh!=b.Mesh){ReleaseALBinding(pair.Key);continue;}
                try{UpdateALBinding(b);}
                catch(Exception e){ReleaseALBinding(pair.Key);_log.LogWarning("[ALGrowth] Pose binding restored: "+e.Message);}
            }
        }
    }
}
