using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunSensitivity(string[] args)
    {
        string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        var body=ReadModel(args[2],"body-sensitivity");var maid=new Maid();
        PrepareALContext(maid,new(){body},1);var c=_alWorking;var t=c.Frame.Profile;
        var v=body.sharedMesh.vertices.Where((_,i)=>i%4==0).Select(v=>NV.Transform(N(v),c.Maps[body]*c.InverseFrame)).ToArray();
        var cases=new Dictionary<string,float[]>{
            {"GrowthFullness",new[]{.8f,1f,1.12f,1.6f,1.8f}}, {"GrowthWidth",new[]{.8f,1.2f,1.6f,2f,2.4f}},
            {"VerticalRange",new[]{.8f,1f,1.2f,1.4f}}, {"UpperReach",new[]{.8f,1f,1.2f,1.4f}},
            {"SkinClearance",new[]{0f,.03f,.08f,.19f,.3f}}, {"WallSmoothing",new[]{0f,.35f,1f,2f,3f}},
            {"SagStrength",new[]{0f,.25f,1f,2f,3f}}, {"MidVolume",new[]{.7f,1f,1.15f,1.5f,1.8f}},
            {"LowerPoleLift",new[]{-.5f,0f,.5f,1f,2.5f}}, {"LateSettle",new[]{-.5f,0f,.5f,1f,1.5f}}
        };
        if(args.Contains("--growth-controls"))cases=new(){
            {"GrowthAxisTilt",new[]{-1f,0f,.01f,.1f,.25f,.5f,1f,1.5f}},
            {"LateForwardShift",new[]{-.05f,0f,.04f,.05f,.06f,.1f,.25f}},
            {"LateHeightScale",new[]{.8f,1f,1.14f,1.15f,1.16f,1.3f,1.5f}},
            {"LateDepthScale",new[]{.8f,1f,1.09f,1.1f,1.11f,1.3f,1.5f}},
            {"LateWidthScale",new[]{.8f,1f,1.09f,1.1f,1.11f,1.3f,1.5f}}
        };
        var report=new List<object>();
        foreach(float stage in new[]{1f/3,4f/9,5f/9,1f})
        {
            var baseline=Array.ConvertAll(v,p=>BellyShape.Deform(p,t,stage,Shape));
            foreach(var entry in cases)foreach(float value in entry.Value)
            {
                var p=Shape.Copy();typeof(VtxSettings).GetField(entry.Key).SetValue(p,value);
                var egg=BellyShape.Growth(t,stage,p);float max=0,sum=0,peak=0;int moved=0;
                for(int i=0;i<v.Length;i++)
                {
                    var changed=BellyShape.Deform(v[i],t,stage,p);float d=NV.Distance(changed,baseline[i]);
                    Check(Scalar.IsFinite(d),"finite sensitivity "+entry.Key);max=Math.Max(max,d);sum+=d*d;
                    float displacement=NV.Distance(changed,v[i]);peak=Math.Max(peak,displacement);if(displacement>1e-6f)moved++;
                }
                report.Add(new{stage,parameter=entry.Key,value,maxChange=max,rmsChange=MathF.Sqrt(sum/v.Length),peakDisplacement=peak,moved,
                    bottom=egg.Bottom,top=egg.Top,width=egg.HalfWidth*2,depth=egg.Depth*2,centerZ=egg.AnteriorOffset,axisSlope=egg.AxisSlope});
            }
            Console.WriteLine("Sensitivity stage "+stage+" complete");
        }
        File.WriteAllText(Path.Combine(output,"sensitivity.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        foreach(var name in new[]{"VirtualAxisStrength","AxisBlendStart","AxisBlendFull","LowerTransitionWidth","UpperTransitionWidth","AxisPullLow","AxisPullHigh","AxisPullAngle"})
        {
            var values=new[]{-.5f,0f,.5f,1f,2f,3f};if(name=="AxisPullAngle")values=new[]{5f,30f,60f,120f,180f};
            foreach(float value in values)
            {
                var p=Shape.Copy();typeof(VtxSettings).GetField(name).SetValue(p,value);
                var axis=VirtualAxisMath.Reference(t,1,p);
                var pose=Growth.Numerics.Matrix4x4.CreateFromAxisAngle(NV.UnitX,1.04719755f);
                var response=VirtualAxisMath.Evaluate(axis,Growth.Numerics.Matrix4x4.Identity,pose,p);
                var weight=VirtualAxisMath.SurfaceWeight(t.Span*.2f,t.Navel,t,p);
                Console.WriteLine($"{name}={value}: weight={weight}, pull={response.Pull}, angle={response.AngleDegrees}");
            }
        }
    }
    static void RunVolumeSimulation(string[] args)
    {
        string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        var body=ReadModel(args[2],"body-volume");var maid=new Maid();
        var original=body.sharedMesh.vertices;var bones=body.bones;var weights=body.sharedMesh.boneWeights;
        var cloth=Reexpress(body,System.Numerics.Matrix4x4.Identity,"wear-volume");
        PrepareALContext(maid,new(){body,cloth},1);Check(_alWorking!=null,"volume calibration");
        var context=_alWorking;var profile=context.Frame.Profile;
        var map=context.Maps[body]*context.InverseFrame;
        var basic=Shape.Copy();basic.LateForwardShift=0;basic.LateHeightScale=basic.LateDepthScale=basic.LateWidthScale=1;
        foreach(float s in new[]{0f,1f/3,4f/9,5f/9})Check(BellyShape.Growth(profile,s,Shape)==BellyShape.Growth(profile,s,basic),"late controls do not alter early geometry");
        var boundary=BellyShape.Growth(profile,5f/9,Shape);var adjacent=BellyShape.Growth(profile,5f/9+1e-5f,Shape);
        Check(Math.Abs(boundary.Top-adjacent.Top)<1e-5f && Math.Abs(boundary.AnteriorOffset-adjacent.AnteriorOffset)<1e-5f,"late growth starts continuously");
        float lastFront=float.MinValue;
        foreach(float s in new[]{1f/3,4f/9,5f/9,.56f,.65f,.75f,.85f,.95f,1f})
        {
            var egg=BellyShape.Growth(profile,s,Shape);
            Check(egg.AnteriorOffset>=lastFront,"forward travel monotone");lastFront=egg.AnteriorOffset;
            Check(Math.Abs(egg.AxisSlope)<1e-7f,"default long axis upright at every stage");
            var axis=VirtualAxisMath.Reference(profile,s,Shape);
            foreach(float angle in new[]{-.8f,0f,.8f})
            {
                var rotation=Growth.Numerics.Matrix4x4.CreateFromAxisAngle(NV.UnitX,angle);
                var result=VirtualAxisMath.Evaluate(axis,Growth.Numerics.Matrix4x4.Identity,rotation,Shape);
                Check(VirtualAxisMath.Finite(result.Transform),"finite all-stage posed binding");
            }
        }
        foreach(string field in new[]{"LateForwardShift","GrowthAxisTilt","LateHeightScale","LateDepthScale","LateWidthScale"})
        {
            var p=Shape.Copy();typeof(VtxSettings).GetField(field).SetValue(p,2.1f);
            Check(BellyShape.Growth(profile,1,p)!=BellyShape.Growth(profile,1,Shape),"parameter beyond slider changes geometry "+field);
        }
        var stages=new List<object>();
        foreach(float stage in new[]{1f/3,4f/9,5f/9,1f})
        {
            PrepareALContext(maid,new(){body,cloth},stage);context=_alWorking;profile=context.Frame.Profile;
            var record=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=original};
            Check(TryDeformAL(body,record,MeshMorphClass.Body,stage,out var result,out _),"volume shape");
            var cr=new MeshRecord{SMR=cloth,Mesh=cloth.sharedMesh,OrigVerts=original};
            Check(TryDeformAL(cloth,cr,MeshMorphClass.OuterCloth,stage,out var clothed,out _),"volume cloth");
            var egg=BellyShape.Growth(profile,stage,Shape);
            int rawArms=0,rawMidThigh=0,midThighSamples=0,midThighField=0,midThighCloth=0;float maxRawArm=0;
            var hips=new NV[2];var knees=new NV[2];
            for(int j=0;j<2;j++)
            {
                string side=j==0?"L":"R";
                int hip=Array.FindIndex(bones,b=>b.name=="Bip01 "+side+" Thigh"),knee=Array.FindIndex(bones,b=>b.name=="Bip01 "+side+" Calf");
                hips[j]=NV.Transform(MatrixBridge.ToManaged(body.sharedMesh.bindposes[hip].inverse).Translation,map);
                knees[j]=NV.Transform(MatrixBridge.ToManaged(body.sharedMesh.bindposes[knee].inverse).Translation,map);
            }
            using(var csv=new StreamWriter(Path.Combine(output,$"stage-{stage:F4}.csv")))
            {
                csv.WriteLine("x,y,z,dx,dy,dz,rawx,rawy,rawz,arm,leg,belly");
                for(int i=0;i<original.Length;i++)
                {
                    var o=NV.Transform(N(original[i]),map);var n=NV.Transform(N(result[i]),map);
                    float arm=BoneSum(weights[i],bones,ArmBone),leg=BoneSum(weights[i],bones,IsALLegBone),belly=BoneSum(weights[i],bones,IsALBellyBone);
                    var raw=(BellyShape.Deform(o,profile,stage,Shape)-o)*BellyShape.DeformationInfluence(BellyShape.BoneInfluence(belly,leg),o.Y,profile,Shape);
                    int sideIndex=o.X<0?0:1;var axis=knees[sideIndex]-hips[sideIndex];float along=NV.Dot(o-hips[sideIndex],axis)/axis.LengthSquared();
                    if(leg>.1f && along>=.30f && along<=.80f)
                    {
                        midThighSamples++;
                        if(NV.Distance(BellyShape.Deform(o,profile,stage,Shape),o)>1e-6f)midThighField++;
                        if(NV.Distance(NV.Transform(N(clothed[i]),map),o)>1e-6f)midThighCloth++;
                    }
                    if(arm>.999f && raw.Length()>1e-7f){rawArms++;maxRawArm=Math.Max(maxRawArm,raw.Length());}
                    if(leg>.9f && raw.Length()>1e-7f)rawMidThigh++;
                    csv.WriteLine(FormattableString.Invariant($"{o.X:R},{o.Y:R},{o.Z:R},{n.X-o.X:R},{n.Y-o.Y:R},{n.Z-o.Z:R},{raw.X:R},{raw.Y:R},{raw.Z:R},{arm:R},{leg:R},{belly:R}"));
                }
            }
            var info=new{stage,egg.Bottom,egg.Top,egg.HalfWidth,egg.Depth,egg.AnteriorOffset,egg.AxisSlope,rawArms,maxRawArm,rawMidThigh,midThighSamples,midThighField,midThighCloth};
            stages.Add(info);Console.WriteLine(JsonSerializer.Serialize(info));
            Check(profile.HasCervix,"actual model cervix detected");
            Check(Math.Abs(egg.Bottom-profile.Cervix.Y)<1e-7f,"lower pole retains cervical height");
            if(stage<=5f/9)Check(Math.Abs(egg.AnteriorAt(egg.Bottom)-profile.Cervix.Z)<1e-7f,"early lower pole remains on opening");
            else Check(Math.Abs(egg.AxisSlope)<1e-7f && egg.AnteriorAt(egg.Bottom)>profile.Cervix.Z,"late volume upright and forward");
            Check(rawArms==0,"geometry field leaves arms alone without ownership mask");
            Check(midThighSamples>500 && midThighField==0 && midThighCloth==0,"middle thigh outside body and cloth fields");
            if(stage==1f/3)
            {
                float peak=result.Select((v,i)=>(v-original[i]).magnitude).Max();
                Check(peak>0 && peak<profile.Span*.005f,"three-month stage barely contacts skin");
            }
        }
        var meta=new{profile.PelvicFloor,profile.Pubis,profile.Navel,profile.Ribs,profile.Span,profile.SkinNavelY,profile.SkinNavelZ,profile.SampleMin,profile.SampleMax,profile.Front,profile.Back,profile.Width,profile.HasCervix,cervix=new[]{profile.Cervix.X,profile.Cervix.Y,profile.Cervix.Z},stages};
        File.WriteAllText(Path.Combine(output,"volume.json"),JsonSerializer.Serialize(meta,new JsonSerializerOptions{WriteIndented=true,NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals}));
    }
}

