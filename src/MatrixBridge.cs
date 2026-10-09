using NMatrix=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;
using UMatrix=UnityEngine.Matrix4x4;
namespace COM3D2.Pregnancy.Plugin
{
    internal static class MatrixBridge
    {
        internal static NMatrix ToManaged(UMatrix u) => new NMatrix(u.m00,u.m10,u.m20,u.m30,u.m01,u.m11,u.m21,u.m31,u.m02,u.m12,u.m22,u.m32,u.m03,u.m13,u.m23,u.m33);
        internal static UMatrix ToUnity(NMatrix n)
        {
            var u=new UMatrix();
            u.m00=n.M11;u.m10=n.M12;u.m20=n.M13;u.m30=n.M14;
            u.m01=n.M21;u.m11=n.M22;u.m21=n.M23;u.m31=n.M24;
            u.m02=n.M31;u.m12=n.M32;u.m22=n.M33;u.m32=n.M34;
            u.m03=n.M41;u.m13=n.M42;u.m23=n.M43;u.m33=n.M44;
            return u;
        }
    }
}
