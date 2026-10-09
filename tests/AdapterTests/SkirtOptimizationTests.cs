using COM3D2.Pregnancy.Plugin.Growth;
using NV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
using NX=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void TestSkirtOptimization()
    {
        var original=new[]{new NV(0,0,0),new NV(1,0,0),new NV(2,0,0),new NV(3,0,0),new NV(9,0,0)};
        var direct=new NV[5];var carried=new[]{NV.Zero,new NV(0,0,100),new NV(0,0,100),new NV(0,0,3),new NV(0,0,7)};
        SkirtTransition.Solve(original,direct,carried,new[]{false,true,true,false,true},new[]{0,1,1,1,2,2,2,3,3},1);
        Check(NV.Distance(carried[1],new NV(0,0,1))<2e-5f && NV.Distance(carried[2],new NV(0,0,2))<2e-5f,"stray skirt donor no longer creates a jump across mixed material");
        Check(carried[0].LengthSquared()==0 && carried[3].Z==3 && carried[4].Z==7,"zero skirt, full skirt and disconnected ornament boundaries stay fixed");
        var random=new Random(427);
        for(int trial=0;trial<200;trial++)
        {
            int count=1+trial%4;float alpha=new[]{0f,.0001f,.31f,.87f,1f,-.3f,1.4f}[trial%7];
            var native=new VirtualWeights.Influence[count];var free=new float[count];float total=0;
            for(int k=0;k<count;k++){float w=k==0?.0001f:(float)random.NextDouble();native[k]=new(k,w);total+=w;free[k]=new[]{0f,.001f,.4f,1f}[(trial+k)%4];}
            for(int k=0;k<count;k++)native[k]=new(k,native[k].Weight/total);
            var recipes=new List<SkirtRecipe>();int Slot(SkirtRecipe r){recipes.Add(r);return recipes.Count-1;}
            var packed=BlendSkirtWeights(native,alpha,free,Slot);Check(packed.Length<=4,"exact skirt packing never exceeds four GPU influences");
            var upper=Enumerable.Range(0,4).Select(k=>NX.CreateFromAxisAngle(NV.UnitY,k*.27f)*NX.CreateTranslation(new NV(k*.1f,k*.03f,-k*.2f))).ToArray();
            var released=upper.Select((m,k)=>m*NX.CreateTranslation(new NV(k*.21f,.04f,.17f))).ToArray();
            var virtualMatrix=NX.CreateFromAxisAngle(NV.UnitX,.67f)*NX.CreateTranslation(new NV(.04f,.31f,.12f));
            foreach(var point in new[]{NV.Zero,NV.UnitX,NV.UnitY,NV.UnitZ})
            {
                var before=NV.Zero;var after=NV.Zero;
                for(int k=0;k<count;k++)before+=NV.Transform(point,NX.Lerp(NX.Lerp(upper[k],virtualMatrix,alpha),released[k],free[k]))*native[k].Weight;
                foreach(var w in packed){var r=recipes[w.Bone];var m=r.Bone<0?virtualMatrix:NX.Lerp(NX.Lerp(upper[r.Bone],virtualMatrix,r.Alpha),released[r.Bone],r.Free);after+=NV.Transform(point,m)*w.Weight;}
                Check(NV.Distance(before,after)<2e-6f,"factored palette equals unfactored skin equation including tiny/free/extreme influences");
            }
        }
    }
}
