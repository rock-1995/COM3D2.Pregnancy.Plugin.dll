using System;
using COM3D2.Pregnancy.Plugin.Growth.Numerics;

namespace COM3D2.Pregnancy.Plugin.Growth
{
    internal static class NavelPlane
    {
        // Least-squares plane of a material boundary ring. Covariance eigenvectors
        // use stack value types; animation fitting allocates no arrays or objects.
        internal static bool Fit(Vector3[] points,float scale,out Matrix4x4 frame)
        {
            frame=Matrix4x4.Identity;
            if(points==null || points.Length<4 || !Scalar.IsFinite(scale) || scale<=0)return false;
            var center=Vector3.Zero;
            foreach(var p in points){if(!Scalar.IsFinite(p.X)||!Scalar.IsFinite(p.Y)||!Scalar.IsFinite(p.Z))return false;center+=p;}
            center/=points.Length;
            var covariance=new Matrix4x4();var right=Vector3.Zero;var up=Vector3.Zero;
            for(int k=0;k<points.Length;k++)
            {
                var d=points[k]-center;float angle=2*Scalar.PI*k/points.Length;
                right+=d*Scalar.Cos(angle);up+=d*Scalar.Sin(angle);
                covariance.M11+=d.X*d.X;covariance.M12+=d.X*d.Y;covariance.M13+=d.X*d.Z;
                covariance.M22+=d.Y*d.Y;covariance.M23+=d.Y*d.Z;covariance.M33+=d.Z*d.Z;
            }
            covariance.M21=covariance.M12;covariance.M31=covariance.M13;covariance.M32=covariance.M23;
            float trace=covariance.M11+covariance.M22+covariance.M33;
            if(!(trace>1e-16f))return false;
            var vectors=Matrix4x4.Identity;
            for(int pass=0;pass<16;pass++)
            {
                int p=0,q=1;float off=Scalar.Abs(covariance[0,1]);
                if(Scalar.Abs(covariance[0,2])>off){p=0;q=2;off=Scalar.Abs(covariance[0,2]);}
                if(Scalar.Abs(covariance[1,2])>off){p=1;q=2;off=Scalar.Abs(covariance[1,2]);}
                if(off<trace*1e-7f)break;
                float angle=.5f*Scalar.Atan2(2*covariance[p,q],covariance[q,q]-covariance[p,p]);
                float c=Scalar.Cos(angle),s=Scalar.Sin(angle),pp=covariance[p,p],qq=covariance[q,q],pq=covariance[p,q];
                covariance[p,p]=c*c*pp-2*s*c*pq+s*s*qq;
                covariance[q,q]=s*s*pp+2*s*c*pq+c*c*qq;covariance[p,q]=covariance[q,p]=0;
                for(int k=0;k<3;k++)
                {
                    if(k!=p && k!=q)
                    {float kp=covariance[k,p],kq=covariance[k,q];covariance[k,p]=covariance[p,k]=c*kp-s*kq;covariance[k,q]=covariance[q,k]=s*kp+c*kq;}
                    float vp=vectors[k,p],vq=vectors[k,q];vectors[k,p]=c*vp-s*vq;vectors[k,q]=s*vp+c*vq;
                }
            }
            int min=0;if(covariance[1,1]<covariance[min,min])min=1;if(covariance[2,2]<covariance[min,min])min=2;
            for(int k=0;k<3;k++)if(k!=min && covariance[k,k]<trace*1e-8f)return false;
            var normal=Vector3.Normalize(new Vector3(vectors[0,min],vectors[1,min],vectors[2,min]));
            if(Vector3.Dot(normal,Vector3.Cross(right,up))<0)normal=-normal;
            right-=normal*Vector3.Dot(right,normal);
            if(right.LengthSquared()<trace*1e-8f)return false;
            right=Vector3.Normalize(right);up=Vector3.Cross(normal,right);
            frame=new Matrix4x4(right.X*scale,right.Y*scale,right.Z*scale,0,up.X*scale,up.Y*scale,up.Z*scale,0,
                normal.X*scale,normal.Y*scale,normal.Z*scale,0,center.X,center.Y,center.Z,1);
            return VirtualAxisMath.Finite(frame);
        }
    }
}
