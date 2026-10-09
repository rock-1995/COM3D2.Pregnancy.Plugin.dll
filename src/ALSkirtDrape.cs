using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NVector=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
using NMatrix=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        sealed class SkirtDrapePlan
        {
            public bool[] Controlled, Released;
            public NVector[] Offsets;
            public NVector[] BindOffsets;
            public float[][] Free;
            public Vector3[] VisualVertices;
        }
        readonly record struct SkirtRecipe(int Bone,float Alpha,float Free);

        static bool IsDrapeBone(Transform bone)
        {
            if(bone==null)return false;
            string name=bone.name.ToLowerInvariant();
            // Require actual skirt rig ownership, including tailcoat skirt panels.
            // A mesh filename alone must not turn tight skirts or trousers into drapes.
            return name.Contains("_yure_skirt_") || name.Contains("_yure_skirt_h_");
        }

        // Exact runtime model root from the user's vertex dump. Mesh.name may
        // be empty; similar model names and all other skirts stay unchanged.
        static bool UseOrdinaryClothing(SkinnedMeshRenderer renderer)
        {
            for(var node=renderer.transform;node!=null;node=node.parent)
                if(string.Equals(node.name,"_SM_dress652_onep",StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }

        static void PrepareSkirtDrape(MeshRecord record,Vector3[] deformed,float stage)
        {
            record.Skirt=null;
            if(UseOrdinaryClothing(record.SMR))return;
            if(stage<=0 || Shape.ClothOffset<=0)return;
            var renderer=record.SMR;var bones=NativeBones(renderer);var weights=NativeWeights(renderer);
            int count=bones.Length;
            if(weights.Length!=deformed.Length)return;
            var controlled=Array.ConvertAll(bones,IsDrapeBone);
            if(!controlled.Any(v=>v))return;
            var parents=new int[count];var indices=new Dictionary<Transform,int>();
            for(int i=0;i<count;i++)if(bones[i]!=null && !indices.ContainsKey(bones[i]))indices.Add(bones[i],i);
            for(int i=0;i<count;i++)
            {
                parents[i]=-1;
                if(!controlled[i])continue;
                for(var p=bones[i].parent;p!=null;p=p.parent)
                    if(indices.TryGetValue(p,out int j)){if(controlled[j])parents[i]=j;break;}
            }
            var deltas=new NVector[deformed.Length];var peak=new float[count];
            for(int i=0;i<deltas.Length;i++)
            {
                deltas[i]=NVector.TransformNormal(N(deformed[i]-record.OrigVerts[i]),record.ToReference);
                if(record.BreastExcluded[i] || record.TorsoOwnership[i]<=0)continue;
                foreach(var w in Influences(weights[i]))
                    if(w.Weight>0 && w.Bone>=0 && w.Bone<count && controlled[w.Bone])
                        peak[w.Bone]=Mathf.Max(peak[w.Bone],deltas[i].Length());
            }
            var sample=new NVector[count];var total=new float[count];var crest=new float[count];
            float tolerance=record.GrowthContext.Frame.Profile.Span*.001f;
            for(int i=0;i<deltas.Length;i++)
            {
                if(record.BreastExcluded[i] || record.TorsoOwnership[i]<=0)continue;
                foreach(var w in Influences(weights[i]))
                {
                    int b=w.Bone;
                    if(w.Weight<=0 || b<0 || b>=count || !controlled[b] || peak[b]<=tolerance)continue;
                    float score=deltas[i].Length();
                    // Average the supported crest, not the many untouched hem vertices.
                    // A smooth band avoids choosing one potentially noisy vertex.
                    float weight=w.Weight*BellyShape.Smooth((score/peak[b]-.75f)/.25f);
                    sample[b]+=deltas[i]*weight;total[b]+=weight;
                    crest[b]+=Vector3.Dot(U(NVector.Transform(N(record.OrigVerts[i]),record.ToReference))-record.GrowthContext.Frame.Center,record.GrowthContext.Frame.Up)*weight;
                }
            }
            for(int i=0;i<count;i++)if(total[i]>0){sample[i]/=total[i];crest[i]/=total[i];}
            // The waistband is commonly weighted to Pelvis/Spine, while the first
            // skirt bone starts below it. Include the cloth directly above each
            // chain; otherwise its supporting belly displacement is never sampled.
            var frame=record.GrowthContext.Frame;var torso=frame.Profile;
            var toTorso=record.ToReference*record.GrowthContext.InverseFrame;
            var points=Array.ConvertAll(record.OrigVerts,v=>NVector.Transform(N(v),toTorso));
            var binds=NativeBinds(renderer);
            for(int b=0;b<count && b<binds.Length;b++)
            {
                if(!controlled[b] || parents[b]>=0)continue;
                var root=NVector.Transform(N(binds[b].inverse.MultiplyPoint3x4(Vector3.zero)),toTorso);
                var radial=new NVector(root.X,0,root.Z-torso.AxisAt(root.Y));
                if(radial.LengthSquared()<1e-10f)continue;
                radial=NVector.Normalize(radial);
                float Support(int i)
                {
                    var p=points[i];
                    if(record.BreastExcluded[i] || record.TorsoOwnership[i]<=0 || p.Y<root.Y-torso.Span*.1f || p.Y>torso.Ribs)return 0;
                    var r=new NVector(p.X,0,p.Z-torso.AxisAt(p.Y));
                    if(r.LengthSquared()<1e-10f)return 0;
                    return BellyShape.Smooth((NVector.Dot(NVector.Normalize(r),radial)-.94f)/.06f)*record.TorsoOwnership[i];
                }
                float high=0;
                for(int i=0;i<points.Length;i++)if(Support(i)>.1f)high=Mathf.Max(high,deltas[i].Length());
                if(high<=sample[b].Length()+tolerance)continue;
                var sum=NVector.Zero;float mass=0,height=0;
                for(int i=0;i<points.Length;i++)
                {
                    float w=Support(i)*BellyShape.Smooth((deltas[i].Length()/high-.85f)/.15f);
                    sum+=deltas[i]*w;height+=points[i].Y*w;mass+=w;
                }
                if(mass>0){sample[b]=sum/mass;crest[b]=height/mass;total[b]=mass;}
            }
            SkirtDrape.Resolve(parents,sample,controlled,tolerance,out var offsets,out var released);
            if(!released.Any(v=>v))return;
            if(!NMatrix.Invert(record.ToReference,out var inverse))return;
            var plan=new SkirtDrapePlan{Controlled=controlled,Released=released,Offsets=offsets,
                BindOffsets=Array.ConvertAll(offsets,v=>NVector.TransformNormal(v,inverse)),
                Free=new float[deformed.Length][],VisualVertices=new Vector3[deformed.Length]};
            var mixed=new bool[deformed.Length];var visualDeltas=new NVector[deformed.Length];var boneDeltas=new NVector[deformed.Length];
            for(int i=0;i<deformed.Length;i++)
            {
                float free=0,skirtWeight=0;var carried=NVector.Zero;
                float y=Vector3.Dot(U(NVector.Transform(N(record.OrigVerts[i]),record.ToReference))-record.GrowthContext.Frame.Center,record.GrowthContext.Frame.Up);
                var influences=Influences(weights[i]);plan.Free[i]=new float[influences.Length];
                if(record.BreastExcluded[i] || record.TorsoOwnership[i]<=0)
                {
                    deformed[i]=plan.VisualVertices[i]=record.OrigVerts[i];
                    continue;
                }
                for(int k=0;k<influences.Length;k++)
                {
                    var w=influences[k];
                    int b=w.Bone;if(w.Weight<=0 || b<0 || b>=count || !controlled[b])continue;
                    skirtWeight+=w.Weight;
                    // Coarse rigs often put the first decreasing bone below the
                    // whole belly. Release the fabric within that long segment too.
                    float share=released[b]?1:total[b]>0?BellyShape.Smooth((crest[b]-y)/(record.GrowthContext.Frame.Profile.Span*.20f)):0;
                    plan.Free[i][k]=share;free+=w.Weight*share;carried+=offsets[b]*(w.Weight*share);
                }
                // Released influences get only the inherited bone displacement. The
                // remaining fabric keeps its original belly result. Upstream bind
                // compensation is per influence in UpdateALBinding, so differently
                // rotated bones do not rotate a blended vertex correction twice.
                // Seed material carry without attenuating it a second time by
                // skirt ownership. Fully skirt-owned fabric keeps this result;
                // mixed torso/skirt seams are interpolated below in rest geometry.
                var visual=skirtWeight>0?deltas[i]*(1-Mathf.Clamp01(free/skirtWeight))+carried/skirtWeight:deltas[i];
                visualDeltas[i]=visual;boneDeltas[i]=carried;
                mixed[i]=skirtWeight>0 && influences.Any(w=>w.Weight>0 && !controlled[w.Bone]);
            }
            // Tiny stray skirt weights must not copy a distant chain's full
            // displacement onto a torso-controlled seam. Interpolate only these
            // mixed material points between the unchanged torso and full-skirt
            // boundaries; retain the native animation weights and free rotations.
            SkirtTransition.Solve(Array.ConvertAll(record.OrigVerts,v=>NVector.Transform(N(v),record.ToReference)),
                deltas,visualDeltas,mixed,record.Mesh.triangles,torso.Span);
            for(int i=0;i<deformed.Length;i++)
            {
                if(record.BreastExcluded[i] || record.TorsoOwnership[i]<=0)continue;
                var visual=visualDeltas[i];var residual=visual-boneDeltas[i];
                deformed[i]=record.OrigVerts[i]+U(NVector.TransformNormal(residual,inverse));
                plan.VisualVertices[i]=record.OrigVerts[i]+U(NVector.TransformNormal(visual,inverse));
            }
            record.Skirt=plan;
        }

        static VirtualWeights.Influence[] BlendSkirtWeights(VirtualWeights.Influence[] native,float alpha,
            float[] free,Func<SkirtRecipe,int> slot)
        {
            if(alpha<0 || alpha>1)
                return native.Select((w,k)=>new VirtualWeights.Influence(w.Weight>0?slot(new SkirtRecipe(w.Bone,alpha,free[k])):w.Bone,w.Weight)).Where(w=>w.Weight>0).ToArray();
            // Factor out the shared virtual matrix before packing. Previously each
            // native influence stored the same virtual contribution in a separate
            // recipe, creating thousands of equivalent palette slots per skirt.
            var bones=new List<SkirtRecipe>();var masses=new List<float>();float virtualMass=0;
            for(int k=0;k<native.Length;k++)
            {
                var w=native[k];if(w.Weight<=0)continue;
                float f=free[k],upper=(1-f)*(1-alpha),mass=upper+f;
                virtualMass+=w.Weight*(1-f)*alpha;
                if(mass<=0)continue;
                var recipe=new SkirtRecipe(w.Bone,0,f/mass);
                int existing=bones.IndexOf(recipe);
                if(existing>=0)masses[existing]+=w.Weight*mass;
                else {bones.Add(recipe);masses.Add(w.Weight*mass);}
            }
            var result=new List<VirtualWeights.Influence>();
            // When all four native slots are occupied, fuse just the smallest
            // component with the virtual contribution. This is the same linear
            // skin equation, without rounding or discarding any positive weight.
            int fuse=-1;
            if(virtualMass>0 && bones.Count==4)
            {fuse=0;for(int k=1;k<masses.Count;k++)if(masses[k]<masses[fuse])fuse=k;}
            for(int k=0;k<bones.Count;k++)
            {
                var r=bones[k];float mass=masses[k];
                if(k==fuse)
                {
                    float upperMass=mass*(1-r.Free);float sum=mass+virtualMass;
                    r=new SkirtRecipe(r.Bone,virtualMass/(upperMass+virtualMass),mass*r.Free/sum);mass=sum;
                }
                result.Add(new VirtualWeights.Influence(slot(r),mass));
            }
            if(virtualMass>0 && fuse<0)result.Add(new VirtualWeights.Influence(slot(new SkirtRecipe(-1,1,0)),virtualMass));
            return result.ToArray();
        }

        internal static void RefreshSkirtPose(TBody body)
        {
            if(body==null || body.maid==null)return;
            // TBody calls both old and new skirt solvers inside SkinMeshUpdate. This
            // hook also covers late IK, independent of MonoBehaviour LateUpdate order.
            foreach(var pair in _alBindings.ToArray())
            {
                var b=pair.Value;
                if(b.Skirt==null || b.Maid!=body.maid)continue;
                if(b.Renderer==null || b.Mesh==null || b.Renderer.sharedMesh!=b.Mesh)
                {ReleaseALBinding(pair.Key);continue;}
                try{UpdateALBinding(b);}
                catch(Exception e){ReleaseALBinding(pair.Key);_log.LogWarning("[ALGrowth] Skirt pose: "+e.Message);}
            }
        }
    }

    [HarmonyPatch(typeof(TBody),"SkinMeshUpdate")]
    class Patch_SkirtDrapePose
    {
        static void Postfix(TBody __instance)=>BellyMorphController.RefreshSkirtPose(__instance);
    }
}
