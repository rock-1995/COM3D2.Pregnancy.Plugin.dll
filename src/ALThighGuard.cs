using System;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NVector=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        // The original COM guard curves, measured in the calibrated rest torso
        // instead of the old user-defined world-space deformation ellipsoid.
        // Called only when rebuilding a shape, never from the pose updater.
        static float GuardSmooth01(float value)
        {
            float t=Mathf.Clamp01(value);
            return t*t*(3f-2f*t);
        }
        static float AccelerateThighGuard(float value,float speed)
            => 1f-Mathf.Pow(1f-Mathf.Clamp01(value),speed);

        static float LowerBodyRestoreMask(NVector original,TorsoProfile torso,float speed)
        {
            if(speed<=0f || original.Y>=torso.Navel)return 0f;
            float epsilon=torso.Span*.0001f;
            float down=Mathf.Max(torso.Navel-torso.PelvicFloor,epsilon);
            float sideRadius=Mathf.Max(torso.WidthAt(torso.Navel),epsilon);
            float lower=AccelerateThighGuard(GuardSmooth01((torso.Navel-original.Y)/down),speed);
            float side=AccelerateThighGuard(GuardSmooth01((Mathf.Abs(original.X)/sideRadius-.18f)/.62f),speed);
            float back=torso.BackAt(original.Y),front=torso.FrontAt(original.Y);
            float forward=GuardSmooth01((original.Z-back)/Mathf.Max(front-back,epsilon));
            return Mathf.Clamp01(lower*side*Mathf.Lerp(1f,.35f,forward));
        }

        static bool IsInnerThighGuardBone(string name)
            => name.Contains("momoniku") || name.Contains("momotwist");

        static float[] BuildThighGuard(MeshRecord record,BoneWeight[] weights,Transform[] bones,ALContext context)
        {
            float speed=Shape.ThighGuardSpeed,strength=Shape.InnerThighGuardStrength;
            if(speed<=0f && strength<=0f)return null;
            var restore=new float[record.OrigVerts.Length];
            var toTorso=record.ToReference*context.InverseFrame;
            for(int i=0;i<restore.Length;i++)
            {
                float lower=LowerBodyRestoreMask(NVector.Transform(N(record.OrigVerts[i]),toTorso),context.Frame.Profile,speed);
                float inner=strength>0f && i<weights.Length
                    ? Mathf.Clamp01(BoneSum(weights[i],bones,IsInnerThighGuardBone)*4f*strength) : 0f;
                // The old two restore operations were sequential lerps.
                restore[i]=1f-(1f-lower)*(1f-inner);
            }
            return restore;
        }

        static void ApplyThighGuard(MeshRecord record,Vector3[] original,Vector3[] changed)
        {
            var restore=record.ThighGuardRestore;
            if(restore==null)return;
            for(int i=0;i<changed.Length;i++)
                if(restore[i]>0f)changed[i]=Vector3.Lerp(changed[i],original[i],restore[i]);
            float smooth=Mathf.Clamp01(Shape.ThighGuardSmoothStrength);
            if(smooth<=0f)return;
            record.Neighbors=record.Neighbors??BuildMeshNeighbors(record.Mesh,changed.Length);
            for(int pass=0;pass<2;pass++)
            {
                var next=(Vector3[])changed.Clone();
                for(int i=0;i<changed.Length;i++)
                {
                    if(restore[i]<=0f || record.BreastExcluded[i] || record.TorsoOwnership[i]<=0f)continue;
                    var delta=(changed[i]-original[i])*2f;float total=2f;
                    foreach(int j in record.Neighbors[i])
                    {
                        if(j<0 || j>=changed.Length)continue;
                        float weight=Mathf.Lerp(.35f,1f,restore[j]);
                        delta+=(changed[j]-original[j])*weight;total+=weight;
                    }
                    next[i]=Vector3.Lerp(changed[i],original[i]+delta/total,smooth*restore[i]);
                }
                Array.Copy(next,changed,changed.Length);
            }
        }
    }
}
