using System;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        // Classify the native rig, not the current posed height. A bent leg must
        // not change which vertices can be deformed on the next Apply.
        static bool[] BuildLowerLegClothExclusion(BoneWeight[] weights,Transform[] bones,int count)
        {
            // Inspect each bone hierarchy once per rebuild, not per vertex.
            var flags=new byte[bones.Length];
            for(int i=0;i<bones.Length;i++)
            {
                for(var bone=bones[i];bone!=null;bone=bone.parent)
                {
                    if(IsDrapeBone(bone))flags[i]|=2;
                    string name=bone.name.ToLowerInvariant();
                    if(name.Contains("calf") || name.Contains("knee") || name.Contains("foot") || name.Contains("toe"))flags[i]|=1;
                }
            }
            int Flag(int index,float value)=>value>0 && index>=0 && index<flags.Length?flags[index]:0;
            var excluded=new bool[count];
            for(int i=0;i<count && i<weights.Length;i++)
            {
                var w=weights[i];
                int mask=Flag(w.boneIndex0,w.weight0)|Flag(w.boneIndex1,w.weight1)|Flag(w.boneIndex2,w.weight2)|Flag(w.boneIndex3,w.weight3);
                excluded[i]=mask==1;
            }
            return excluded;
        }
    }
}
