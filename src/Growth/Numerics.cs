using System;

// Small managed primitives for this game's legacy Mono profile. Row-vector convention
// matches AL's System.Numerics; no external Numerics DLL is needed in the game.
namespace COM3D2.Pregnancy.Plugin.Growth
{
    internal static class Scalar
    {
        internal const float PI = (float)Math.PI;
        internal static float Clamp(float v,float a,float b) => Math.Min(Math.Max(v,a),b);
        internal static int Clamp(int v,int a,int b) => Math.Min(Math.Max(v,a),b);
        internal static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        internal static float Abs(float v) => Math.Abs(v);
        internal static float Min(float a,float b) => Math.Min(a,b);
        internal static float Max(float a,float b) => Math.Max(a,b);
        internal static float Sqrt(float v) => (float)Math.Sqrt(v);
        internal static float Sin(float v) => (float)Math.Sin(v);
        internal static float Cos(float v) => (float)Math.Cos(v);
        internal static float Acos(float v) => (float)Math.Acos(v);
        internal static float Atan2(float y,float x) => (float)Math.Atan2(y,x);
        internal static float Pow(float a,float b) => (float)Math.Pow(a,b);
        internal static float Floor(float v) => (float)Math.Floor(v);
    }
}
namespace COM3D2.Pregnancy.Plugin.Growth.Numerics
{
    internal struct Vector3
    {
        public float X,Y,Z;
        internal Vector3(float x,float y,float z) { X=x;Y=y;Z=z; }
        internal static Vector3 Zero => new Vector3();
        internal static Vector3 UnitX => new Vector3(1,0,0);
        internal static Vector3 UnitY => new Vector3(0,1,0);
        internal static Vector3 UnitZ => new Vector3(0,0,1);
        internal float LengthSquared() => X*X+Y*Y+Z*Z;
        internal float Length() => Scalar.Sqrt(LengthSquared());
        public static Vector3 operator +(Vector3 a,Vector3 b) => new Vector3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static Vector3 operator -(Vector3 a,Vector3 b) => new Vector3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.X,-a.Y,-a.Z);
        public static Vector3 operator *(Vector3 a,float b) => new Vector3(a.X*b,a.Y*b,a.Z*b);
        public static Vector3 operator *(float b,Vector3 a) => a*b;
        public static Vector3 operator /(Vector3 a,float b) => new Vector3(a.X/b,a.Y/b,a.Z/b);
        internal static float Dot(Vector3 a,Vector3 b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        internal static Vector3 Cross(Vector3 a,Vector3 b) => new Vector3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        internal static Vector3 Normalize(Vector3 a) => a/a.Length();
        internal static float DistanceSquared(Vector3 a,Vector3 b) => (a-b).LengthSquared();
        internal static float Distance(Vector3 a,Vector3 b) => (a-b).Length();
        internal static Vector3 Min(Vector3 a,Vector3 b) => new Vector3(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Min(a.Z,b.Z));
        internal static Vector3 Max(Vector3 a,Vector3 b) => new Vector3(Math.Max(a.X,b.X),Math.Max(a.Y,b.Y),Math.Max(a.Z,b.Z));
        internal static Vector3 Transform(Vector3 v,Matrix4x4 m) => new Vector3(v.X*m.M11+v.Y*m.M21+v.Z*m.M31+m.M41,v.X*m.M12+v.Y*m.M22+v.Z*m.M32+m.M42,v.X*m.M13+v.Y*m.M23+v.Z*m.M33+m.M43);
        internal static Vector3 TransformNormal(Vector3 v,Matrix4x4 m) => new Vector3(v.X*m.M11+v.Y*m.M21+v.Z*m.M31,v.X*m.M12+v.Y*m.M22+v.Z*m.M32,v.X*m.M13+v.Y*m.M23+v.Z*m.M33);
        internal static Vector3 Transform(Vector3 v,Quaternion q) => TransformNormal(v,Matrix4x4.CreateFromQuaternion(q));
        public override string ToString() => string.Format("({0:F5}, {1:F5}, {2:F5})",X,Y,Z);
    }
    internal struct Quaternion
    {
        public float X,Y,Z,W;
        internal Quaternion(float x,float y,float z,float w) {X=x;Y=y;Z=z;W=w;}
        internal Quaternion(Vector3 xyz,float w):this(xyz.X,xyz.Y,xyz.Z,w) {}
        internal static Quaternion Normalize(Quaternion q)
        { float s=Scalar.Sqrt(q.X*q.X+q.Y*q.Y+q.Z*q.Z+q.W*q.W);return new Quaternion(q.X/s,q.Y/s,q.Z/s,q.W/s); }
        internal static Quaternion CreateFromAxisAngle(Vector3 axis,float angle)
        { float s=Scalar.Sin(angle*.5f);return new Quaternion(axis*s,Scalar.Cos(angle*.5f)); }
    }
    internal struct Matrix4x4
    {
        public float M11,M12,M13,M14,M21,M22,M23,M24,M31,M32,M33,M34,M41,M42,M43,M44;
        internal Matrix4x4(float a,float b,float c,float d,float e,float f,float g,float h,float i,float j,float k,float l,float m,float n,float o,float p)
        {M11=a;M12=b;M13=c;M14=d;M21=e;M22=f;M23=g;M24=h;M31=i;M32=j;M33=k;M34=l;M41=m;M42=n;M43=o;M44=p;}
        internal static Matrix4x4 Identity => new Matrix4x4(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1);
        internal Vector3 Translation => new Vector3(M41,M42,M43);
        internal static Matrix4x4 CreateTranslation(Vector3 v) {var m=Identity;m.M41=v.X;m.M42=v.Y;m.M43=v.Z;return m;}
        internal static Matrix4x4 CreateScale(float s) {var m=Identity;m.M11=m.M22=m.M33=s;return m;}
        internal static Matrix4x4 CreateFromQuaternion(Quaternion q)
        {
            float xx=q.X*q.X,yy=q.Y*q.Y,zz=q.Z*q.Z,xy=q.X*q.Y,xz=q.X*q.Z,yz=q.Y*q.Z,wx=q.W*q.X,wy=q.W*q.Y,wz=q.W*q.Z;
            return new Matrix4x4(1-2*(yy+zz),2*(xy+wz),2*(xz-wy),0,2*(xy-wz),1-2*(xx+zz),2*(yz+wx),0,2*(xz+wy),2*(yz-wx),1-2*(xx+yy),0,0,0,0,1);
        }
        internal static Matrix4x4 CreateFromAxisAngle(Vector3 a,float angle)
        {
            float c=Scalar.Cos(angle),s=Scalar.Sin(angle),t=1-c;
            return new Matrix4x4(a.X*a.X*t+c,a.X*a.Y*t+a.Z*s,a.X*a.Z*t-a.Y*s,0,a.X*a.Y*t-a.Z*s,a.Y*a.Y*t+c,a.Y*a.Z*t+a.X*s,0,a.X*a.Z*t+a.Y*s,a.Y*a.Z*t-a.X*s,a.Z*a.Z*t+c,0,0,0,0,1);
        }
        internal float this[int r,int c]
        {
            get { switch(r*4+c) {case 0:return M11;case 1:return M12;case 2:return M13;case 3:return M14;case 4:return M21;case 5:return M22;case 6:return M23;case 7:return M24;case 8:return M31;case 9:return M32;case 10:return M33;case 11:return M34;case 12:return M41;case 13:return M42;case 14:return M43;default:return M44;} }
            set { switch(r*4+c) {case 0:M11=value;break;case 1:M12=value;break;case 2:M13=value;break;case 3:M14=value;break;case 4:M21=value;break;case 5:M22=value;break;case 6:M23=value;break;case 7:M24=value;break;case 8:M31=value;break;case 9:M32=value;break;case 10:M33=value;break;case 11:M34=value;break;case 12:M41=value;break;case 13:M42=value;break;case 14:M43=value;break;default:M44=value;break;} }
        }
        public static Matrix4x4 operator *(Matrix4x4 a,Matrix4x4 b)
        {
            var m=new Matrix4x4();
            for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r,c]=a[r,0]*b[0,c]+a[r,1]*b[1,c]+a[r,2]*b[2,c]+a[r,3]*b[3,c];
            return m;
        }
        internal static Matrix4x4 Lerp(Matrix4x4 a,Matrix4x4 b,float t)
        {var m=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r,c]=a[r,c]+(b[r,c]-a[r,c])*t;return m;}
        internal static bool Invert(Matrix4x4 input,out Matrix4x4 inverse)
        {
            // Pivoted elimination also supports nonuniform scale and shear in imported bind poses.
            var a=new double[4,8];inverse=default;
            for(int r=0;r<4;r++){for(int c=0;c<4;c++)a[r,c]=input[r,c];a[r,r+4]=1;}
            for(int k=0;k<4;k++)
            {
                int pivot=k;for(int r=k+1;r<4;r++)if(Math.Abs(a[r,k])>Math.Abs(a[pivot,k]))pivot=r;
                if(double.IsNaN(a[pivot,k]) || Math.Abs(a[pivot,k])<1e-30)return false;
                if(pivot!=k)for(int c=0;c<8;c++){double tmp=a[k,c];a[k,c]=a[pivot,c];a[pivot,c]=tmp;}
                double scale=a[k,k];for(int c=0;c<8;c++)a[k,c]/=scale;
                for(int r=0;r<4;r++)if(r!=k){double factor=a[r,k];for(int c=0;c<8;c++)a[r,c]-=factor*a[k,c];}
            }
            for(int r=0;r<4;r++)for(int c=0;c<4;c++){inverse[r,c]=(float)a[r,c+4];if(!Scalar.IsFinite(inverse[r,c]))return false;}
            return true;
        }
    }
}

namespace System.Runtime.CompilerServices { internal static class IsExternalInit {} }
