using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        static int[] BuildBodyNormalRegions(MeshRecord record,BoneWeight[] weights,Transform[] bones)
        {
            var regions=new int[record.OrigVerts.Length];
            for(int i=0;i<regions.Length;i++)
            {
                if(record.BreastExcluded[i])regions[i]=1;
                // Include the partially restored fringe of the native inner-
                // thigh guard. The separate smooth lower-body fade stays in the
                // abdominal region; it is not a hard protection boundary.
                else if(Shape.InnerThighGuardStrength>0 && i<weights.Length && BoneSum(weights[i],bones,IsInnerThighGuardBone)>0)regions[i]=2;
            }
            return regions;
        }
    }
}
