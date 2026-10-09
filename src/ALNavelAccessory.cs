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
        const int NavelSamples=12;
        sealed class NavelAttachment
        {
            public MeshRecord Source;
            public ALContext Context;
            public int[] Indices,SampleIndices,Triangles;
            public V[] Barycentric,OriginalRing,MorphedRing,PosedRing,PosedVertices,Morphed;
            public BoneWeight[] Weights;
            public float[] Alpha;
            public Transform[] Bones;
            public Matrix4x4[] Binds;
            public M[] NativePalette;
            public int[] NativeTicks;
            public int Tick;
            public M ToBody,OriginalFrame,BakedFrame,BakedFrameInverse,LastFrame;
            public float Radius,CenterY;
        }
        static V NavelCenter(V a,V b,V c,V bary)=>a*bary.X+b*bary.Y+c*bary.Z;
        static void FillNavelRing(NavelAttachment a,V[] vertices,V[] ring)
        {
            for(int k=0;k<NavelSamples;k++)ring[k]=NavelCenter(vertices[a.SampleIndices[k*3]],vertices[a.SampleIndices[k*3+1]],vertices[a.SampleIndices[k*3+2]],a.Barycentric[k]);
        }
        static void SetNavelSource(NavelAttachment a,MeshRecord source)
        {
            a.Source=source;a.Context=source.GrowthContext;a.Bones=NativeBones(source.SMR);a.Binds=NativeBinds(source.SMR);
            if(!M.Invert(source.ToReference,out a.ToBody))throw new InvalidOperationException("Singular navel body mapping.");
            var weights=NativeWeights(source.SMR);
            if(weights.Length!=source.OrigVerts.Length)throw new InvalidOperationException("Navel body weights missing.");
            int count=a.Indices.Length;
            a.Morphed=new V[count];a.Weights=new BoneWeight[count];a.Alpha=new float[count];a.PosedVertices=new V[count];
            a.NativePalette=new M[a.Bones.Length];a.NativeTicks=new int[a.Bones.Length];a.Tick=0;
            for(int k=0;k<count;k++)
            {
                int i=a.Indices[k];a.Morphed[k]=V.Transform(N(source.LastNewV[i]),source.ToReference);
                a.Weights[k]=weights[i];a.Alpha[k]=Attachment(source,i,a.Context.Stage);
            }
            FillNavelRing(a,a.Morphed,a.MorphedRing);
        }
        static bool TryNavelBoundary(MeshRecord body,float radius,float centerY,out NavelAttachment attachment)
        {
            attachment=null;var c=body.GrowthContext;var toLocal=body.ToReference*c.InverseFrame;
            var local=Array.ConvertAll(body.OrigVerts,v=>V.Transform(N(v),toLocal));var triangles=body.Mesh.triangles;
            var indices=new List<int>();var sampleIndices=new int[NavelSamples*3];var triangleIds=new int[NavelSamples];var bary=new V[NavelSamples];
            int Slot(int index){int found=indices.IndexOf(index);if(found>=0)return found;indices.Add(index);return indices.Count-1;}
            for(int sample=0;sample<NavelSamples;sample++)
            {
                float angle=2*Scalar.PI*sample/NavelSamples,x=radius*Scalar.Cos(angle),y=centerY+radius*1.3f*Scalar.Sin(angle);
                float bestZ=float.NegativeInfinity;int selected=-1;V selectedBary=V.Zero;
                for(int t=0;t+2<triangles.Length;t+=3)
                {
                    int i=triangles[t],j=triangles[t+1],k=triangles[t+2];
                    if(i<0||j<0||k<0||i>=local.Length||j>=local.Length||k>=local.Length)continue;
                    if(body.TorsoOwnership[i]<.9f||body.TorsoOwnership[j]<.9f||body.TorsoOwnership[k]<.9f)continue;
                    var a=local[i];var b=local[j];var d=local[k];
                    float det=(b.Y-d.Y)*(a.X-d.X)+(d.X-b.X)*(a.Y-d.Y);
                    if(Math.Abs(det)<c.Frame.Profile.Span*c.Frame.Profile.Span*1e-10f)continue;
                    float u=((b.Y-d.Y)*(x-d.X)+(d.X-b.X)*(y-d.Y))/det;
                    float v=((d.Y-a.Y)*(x-d.X)+(a.X-d.X)*(y-d.Y))/det,w=1-u-v;
                    if(u<-.00001f||v<-.00001f||w<-.00001f)continue;
                    float z=a.Z*u+b.Z*v+d.Z*w;
                    if(z<=bestZ || z<c.Frame.Profile.AxisAt(y))continue;
                    bestZ=z;selected=t;selectedBary=new V(u,v,w);
                }
                if(selected<0)return false;
                for(int k=0;k<3;k++)sampleIndices[sample*3+k]=Slot(triangles[selected+k]);
                triangleIds[sample]=selected/3;bary[sample]=selectedBary;
            }
            var a2=new NavelAttachment {Indices=indices.ToArray(),SampleIndices=sampleIndices,Triangles=triangleIds,Barycentric=bary,
                OriginalRing=new V[NavelSamples],MorphedRing=new V[NavelSamples],PosedRing=new V[NavelSamples],Radius=radius,CenterY=centerY};
            var original=indices.Select(i=>V.Transform(N(body.OrigVerts[i]),body.ToReference)).ToArray();
            FillNavelRing(a2,original,a2.OriginalRing);SetNavelSource(a2,body);
            if(!NavelPlane.Fit(a2.OriginalRing,1,out a2.OriginalFrame) || !NavelPlane.Fit(a2.MorphedRing,1,out a2.BakedFrame) || !M.Invert(a2.BakedFrame,out a2.BakedFrameInverse))return false;
            a2.LastFrame=a2.BakedFrame;attachment=a2;return true;
        }
        static bool TryDeformNavelAccessory(SkinnedMeshRenderer smr,MeshRecord rec,float stage,out Vector3[] result,out DeformStats stats)
        {
            result=null;stats=new DeformStats();rec.Navel=null;var context=_alWorking;
            if(context==null||rec.OrigVerts==null||rec.OrigVerts.Length==0||!context.Maps.TryGetValue(smr,out var map)||!M.Invert(map,out var inverse))return false;
            var profile=context.Frame.Profile;
            if(!Scalar.IsFinite(profile.SkinNavelY)||!Scalar.IsFinite(profile.SkinNavelZ))
            {LogMorphSkip(context.Maid,smr,MeshMorphClass.NavelAccessory,"no-original-navel-for-plane");return false;}
            float radius=Math.Max(Math.Abs(Shape.NavelRadius)*profile.Span,profile.Span*.005f);
            float centerY=profile.SkinNavelY+Shape.NavelVerticalOffset*profile.Span;
            if(!Scalar.IsFinite(radius)||!Scalar.IsFinite(centerY))return false;
            NavelAttachment attachment=null;
            foreach(var body in context.NavelBodies)
                if(TryNavelBoundary(body,radius,centerY,out attachment))break;
            if(attachment==null){LogMorphSkip(context.Maid,smr,MeshMorphClass.NavelAccessory,"navel-boundary-plane-unavailable");return false;}
            if(!M.Invert(attachment.OriginalFrame,out var originalInverse))return false;
            var transform=map*originalInverse*attachment.BakedFrame*inverse;
            result=(Vector3[])rec.OrigVerts.Clone();
            if(stage>0)for(int i=0;i<result.Length;i++)result[i]=U(V.Transform(N(rec.OrigVerts[i]),transform));
            rec.Navel=attachment;rec.GrowthContext=context;rec.ToReference=map;rec.LastNewV=result;
            stats.MaskedVerts=result.Length;stats.EllipsoidVerts=NavelSamples;stats.MaxStrength=1;
            for(int i=0;i<result.Length;i++){float distance=V.Distance(V.Transform(N(result[i]),map),V.Transform(N(rec.OrigVerts[i]),map));if(distance>1e-7f)stats.NonZeroVerts++;stats.MaxDelta=Math.Max(stats.MaxDelta,distance);}
            if(IsDebugMeshLoggingEnabled())_log.LogInfo("[NavelAccessory] plane mesh="+smr.sharedMesh.name+" body="+attachment.Source.Mesh.name+" samples="+NavelSamples+" radius="+radius+" centerY="+centerY);
            return true;
        }
        static void InstallNavelBinding(Maid maid,MeshRecord record,Vector3[] deformed,float stage)
        {
            var a=record.Navel;var renderer=record.SMR;if(stage<=0||renderer==null||a==null)return;
            try
            {
                var b=new ALBinding {Maid=maid,Mesh=record.Mesh,Renderer=renderer,Bones=renderer.bones,Weights=record.Mesh.boneWeights,
                    Binds=record.Mesh.bindposes,Context=a.Context,ToReference=record.ToReference,Bounds=renderer.localBounds,
                    Quality=renderer.quality,UpdateWhenOffscreen=renderer.updateWhenOffscreen,Navel=a};
                if(b.Weights.Length!=deformed.Length||b.Bones.Length!=b.Binds.Length)return;
                int slot=b.Bones.Length;var weights=new BoneWeight[deformed.Length];
                for(int i=0;i<weights.Length;i++){weights[i]=new BoneWeight {boneIndex0=slot,weight0=1};b.NavelBox.Include(N(deformed[i]));}
                b.Palette=b.Binds.Concat(new[]{Matrix4x4.identity}).ToArray();_alBindings[renderer]=b;
                renderer.bones=b.Bones.Concat(new[]{a.Context.Pelvis}).ToArray();b.Mesh.bindposes=b.Palette;b.Mesh.boneWeights=weights;
                renderer.quality=SkinQuality.Bone4;renderer.updateWhenOffscreen=true;EnsureMonitor(maid);UpdateNavelBinding(b);
            }
            catch(Exception e){ReleaseALBinding(renderer);_log.LogWarning("[NavelAccessory] Binding restored: "+e.Message);}
        }
        static V PoseNavelVertex(NavelAttachment a,int k,M virtualTransform)
        {
            var point=a.Morphed[k];var w=a.Weights[k];var native=V.Zero;
            void Add(int i,float weight)
            {
                if(weight<=0)return;
                if(i<0||i>=a.Bones.Length||i>=a.Binds.Length||a.Bones[i]==null)throw new InvalidOperationException("Navel support bone missing.");
                if(a.NativeTicks[i]!=a.Tick){a.NativePalette[i]=a.ToBody*MatrixBridge.ToManaged(a.Binds[i])*MatrixBridge.ToManaged(a.Bones[i].localToWorldMatrix);a.NativeTicks[i]=a.Tick;}
                native+=V.Transform(point,a.NativePalette[i])*weight;
            }
            Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
            return native*(1-a.Alpha[k])+V.Transform(point,virtualTransform)*a.Alpha[k];
        }
        static void UpdateNavelBinding(ALBinding b)
        {
            var a=b.Navel;
            // Body-only TMorph refresh also updates the attachment stencil. Clothing
            // refresh scope is not changed; no searching or stencil allocations on steady poses.
            if(_alContexts.TryGetValue(b.Maid.GetHashCode(),out var latest)&&latest!=a.Context)
            {
                MeshRecord replacement=null;
                foreach(var r in latest.NavelBodies)if(r.SMR==a.Source.SMR&&r.Mesh==a.Source.Mesh&&r.OrigVerts.Length==a.Source.OrigVerts.Length){replacement=r;break;}
                if(replacement==null)throw new InvalidOperationException("Navel support replaced; awaiting mesh refresh.");
                SetNavelSource(a,replacement);
            }
            if(a.Source.SMR==null||a.Source.SMR.sharedMesh!=a.Source.Mesh||a.Context.Pelvis==null||a.Context.Spine==null)throw new InvalidOperationException("Navel support destroyed.");
            var c=a.Context;var carrier=MatrixBridge.ToManaged(c.Pelvis.localToWorldMatrix);
            if(!M.Invert(carrier,out var invCarrier))throw new InvalidOperationException("Singular navel carrier.");
            var pelvis=c.PelvisBind*carrier;
            var virtualTransform=VirtualAxisMath.Evaluate(c.Axis,pelvis,c.SpineBind*MatrixBridge.ToManaged(c.Spine.localToWorldMatrix),Shape).Transform;
            a.Tick++;for(int i=0;i<a.Indices.Length;i++)a.PosedVertices[i]=PoseNavelVertex(a,i,virtualTransform);
            FillNavelRing(a,a.PosedVertices,a.PosedRing);
            var px=V.TransformNormal(V.UnitX,pelvis);var py=V.TransformNormal(V.UnitY,pelvis);var pz=V.TransformNormal(V.UnitZ,pelvis);
            float scale=(float)Math.Pow(Math.Abs(V.Dot(px,V.Cross(py,pz))),1.0/3.0);
            if(!NavelPlane.Fit(a.PosedRing,scale,out var frame))
            {frame=a.LastFrame;var center=V.Zero;foreach(var p in a.PosedRing)center+=p;center/=NavelSamples;frame.M41=center.X;frame.M42=center.Y;frame.M43=center.Z;}
            if(!VirtualAxisMath.Finite(frame))throw new InvalidOperationException("Nonfinite navel plane.");
            a.LastFrame=frame;var transform=b.ToReference*a.BakedFrameInverse*frame;
            var bind=MatrixBridge.ToUnity(transform*invCarrier);int slot=b.Binds.Length;bool changed=false;
            for(int r=0;r<4;r++)for(int col=0;col<4;col++)if(b.Palette[slot][r,col]!=bind[r,col])changed=true;
            b.Palette[slot]=bind;if(changed)b.Mesh.bindposes=b.Palette;
            var root=b.Renderer.rootBone!=null?b.Renderer.rootBone:b.Renderer.transform;
            var box=b.NavelBox.Transform(transform*MatrixBridge.ToManaged(root.worldToLocalMatrix));
            var size=U(box.Max-box.Min);var bounds=new Bounds(U((box.Min+box.Max)*.5f),size);bounds.Expand(Mathf.Max(.001f,size.magnitude*.01f));b.Renderer.localBounds=bounds;
        }
    }
}
