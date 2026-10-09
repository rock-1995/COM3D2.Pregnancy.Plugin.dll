using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    // Update authored shading by the change of the adjacent surface, rather than
    // replacing it with raw polygon normals inside a binary affected-region mask.
    // This runs only when the existing deformation bake already rebuilds normals.
    internal static class DeformationNormals
    {
        readonly struct PointKey : IEquatable<PointKey>
        {
            readonly int x,y,z;
            internal PointKey(int x,int y,int z){this.x=x;this.y=y;this.z=z;}
            public bool Equals(PointKey b)=>x==b.x && y==b.y && z==b.z;
            public override bool Equals(object b)=>b is PointKey && Equals((PointKey)b);
            public override int GetHashCode(){unchecked{return (x*73856093)^(y*19349663)^(z*83492791);}}
        }
        static Vector3 Unit(Vector3 v)
        {float length=v.magnitude;return length>1e-20f?v/length:Vector3.zero;}
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x+v.y+v.z) && !float.IsInfinity(v.x+v.y+v.z);

        internal static Vector3[] Build(Vector3[] original,Vector3[] deformed,Vector3[] authored,int[] triangles)
        {
            int count=original.Length;
            if(deformed.Length!=count || authored.Length!=count)throw new ArgumentException("Normal surface vertex counts differ.");
            var result=(Vector3[])authored.Clone();if(count==0)return result;
            var lo=original[0];var hi=lo;
            for(int i=1;i<count;i++)
            {
                var v=original[i];lo=new Vector3(Math.Min(lo.x,v.x),Math.Min(lo.y,v.y),Math.Min(lo.z,v.z));
                hi=new Vector3(Math.Max(hi.x,v.x),Math.Max(hi.y,v.y),Math.Max(hi.z,v.z));
            }
            // Scale-relative tolerance only reunites duplicated seam vertices.
            float epsilon=Math.Max((hi-lo).magnitude*1e-6f,1e-12f),epsilonSq=epsilon*epsilon;
            var groups=new int[count];var bins=new Dictionary<PointKey,List<int>>();
            for(int i=0;i<count;i++)
            {
                var v=(original[i]-lo)/epsilon;
                int x=(int)Math.Floor(v.x),y=(int)Math.Floor(v.y),z=(int)Math.Floor(v.z),rep=-1;
                for(int dx=-1;dx<=1 && rep<0;dx++)for(int dy=-1;dy<=1 && rep<0;dy++)for(int dz=-1;dz<=1 && rep<0;dz++)
                {
                    List<int> candidates;if(!bins.TryGetValue(new PointKey(x+dx,y+dy,z+dz),out candidates))continue;
                    foreach(int j in candidates)
                    {
                        // Distinct authored hard edges and separated surfaces remain separate.
                        if((original[i]-original[j]).sqrMagnitude<=epsilonSq &&
                           (deformed[i]-deformed[j]).sqrMagnitude<=epsilonSq &&
                           (authored[i]-authored[j]).sqrMagnitude<=1e-8f){rep=j;break;}
                    }
                }
                groups[i]=rep<0?i:rep;
                if(rep>=0)continue;
                var key=new PointKey(x,y,z);List<int> bucket;
                if(!bins.TryGetValue(key,out bucket)){bucket=new List<int>();bins.Add(key,bucket);}bucket.Add(i);
            }
            var before=new Vector3[count];var after=new Vector3[count];var changed=new bool[count];
            for(int k=0;k+2<triangles.Length;k+=3)
            {
                int a=triangles[k],b=triangles[k+1],c=triangles[k+2];
                if(a<0 || b<0 || c<0 || a>=count || b>=count || c>=count)continue;
                var n0=Vector3.Cross(original[b]-original[a],original[c]-original[a]);
                var n1=Vector3.Cross(deformed[b]-deformed[a],deformed[c]-deformed[a]);
                if(!Finite(n0) || !Finite(n1))continue;
                bool moved=(original[a]-deformed[a]).sqrMagnitude>0 || (original[b]-deformed[b]).sqrMagnitude>0 || (original[c]-deformed[c]).sqrMagnitude>0;
                int ga=groups[a],gb=groups[b],gc=groups[c];
                before[ga]+=n0;before[gb]+=n0;before[gc]+=n0;after[ga]+=n1;after[gb]+=n1;after[gc]+=n1;
                if(moved){changed[ga]=true;changed[gb]=true;changed[gc]=true;}
            }
            for(int i=0;i<count;i++)
            {
                int g=groups[i];if(!changed[g])continue;
                var from=Unit(before[g]);var to=Unit(after[g]);
                if(from.sqrMagnitude<.5f || to.sqrMagnitude<.5f)continue; // Degenerate fan: keep authored fallback.
                float dot=Math.Max(-1,Math.Min(1,Vector3.Dot(from,to)));
                if(dot>1-1e-7f)continue;
                var axis=Vector3.Cross(from,to);Vector3 rotated;
                if(dot< -1+1e-6f)
                {
                    axis=Unit(Vector3.Cross(from,Math.Abs(from.x)<.8f?Vector3.right:Vector3.up));
                    rotated=axis*(2*Vector3.Dot(axis,authored[i]))-authored[i];
                }
                else rotated=authored[i]+Vector3.Cross(axis,authored[i])+Vector3.Cross(axis,Vector3.Cross(axis,authored[i]))/(1+dot);
                rotated=Unit(rotated);if(Finite(rotated) && rotated.sqrMagnitude>.5f)result[i]=rotated;
            }
            return result;
        }
    }
}
