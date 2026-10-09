using System.Diagnostics;
using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NM=System.Numerics.Matrix4x4;
using NV=System.Numerics.Vector3;
using GM=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;
using GV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;

namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunBindingStudy(string[] args)
    {
        var folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);
        // Explicit pre-study controls make the experiment reproducible after adopting the candidate defaults.
        var originalSettings=new VtxSettings{LowerTransitionStart=.02f,LowerTransitionWidth=.60f,UpperTransitionStart=.10542166f,UpperTransitionWidth=.5965462f,AxisPullLow=.15f,AxisPullHigh=.8f,AxisPullAngle=60};var reports=new List<object>();
        foreach(string variant in new[]{"baseline","bands","response","combined","strong"})
        {
            Shape=originalSettings.Copy();
            if(variant=="bands" || variant=="combined" || variant=="strong")
            {
                Shape.LowerTransitionStart=.08f;Shape.LowerTransitionWidth=.34f;
                Shape.UpperTransitionStart=.28f;Shape.UpperTransitionWidth=.35f;
            }
            if(variant=="response" || variant=="combined" || variant=="strong")
            {Shape.AxisPullLow=.60f;Shape.AxisPullHigh=.90f;Shape.AxisPullAngle=35;}
            if(variant=="strong") {Shape.AxisBlendFull=.15f;Shape.AxisPullLow=.85f;Shape.AxisPullHigh=1;}
            foreach(float stage in new[]{4f/9,5f/9,1f})
            {
                var body=ReadModel(args[2],"body-real");var maid=new Maid();
                var original=body.sharedMesh.vertices;var weights=body.sharedMesh.boneWeights;var bones=body.bones;
                var rest=bones.Select(b=>b.localToWorldMatrix.Rows).ToArray();
                var sw=Stopwatch.StartNew();long alloc=GC.GetAllocatedBytesForCurrentThread();
                PrepareALContext(maid,new(){body},stage);var c=_alWorking;Check(c!=null,"study calibration");
                double prepareMs=sw.Elapsed.TotalMilliseconds;
                var r=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=original};
                sw.Restart();Check(TryDeformAL(body,r,MeshMorphClass.Body,stage,out var changed,out _),"study deform");
                double deformMs=sw.Elapsed.TotalMilliseconds;
                sw.Restart();InstallALBinding(maid,r,changed,stage);double installMs=sw.Elapsed.TotalMilliseconds;
                long rebuildBytes=GC.GetAllocatedBytesForCurrentThread()-alloc;
                var b=_alBindings.TryGetValue(body,out var binding)?binding:new ALBinding{Bones=bones,Binds=body.sharedMesh.bindposes,Recipes=Array.Empty<VirtualWeights.Recipe>()};var p=c.Frame.Profile;var toLocal=r.ToReference*c.InverseFrame;
                var material=original.Select(v=>GV.Transform(N(v),toLocal)).ToArray();
                var alpha=Enumerable.Range(0,original.Length).Select(i=>Attachment(r,i,stage)).ToArray();
                var torsoWorld=MatrixBridge.ToUnity(c.FrameMatrix*MatrixBridge.ToManaged(c.Pelvis.localToWorldMatrix)).Rows;
                NM.Invert(torsoWorld,out var worldTorso);
                float[] restY=rest.Select(m=>NV.Transform(m.Translation,worldTorso).Y).ToArray();
                void Pose(float common,float relative)
                {
                    var rotation=NM.CreateRotationX(common*MathF.PI/180);
                    for(int i=0;i<bones.Length;i++)
                    {
                        // Reproducible hinge fixture: pelvis rigid motion plus an upper-torso hinge at the waist.
                        float t=Math.Clamp((restY[i]-p.Navel)/(.08f*p.Span),0,1);
                        if(bones[i].name.Contains("Spine",StringComparison.OrdinalIgnoreCase))t=1;
                        var extra=NM.CreateTranslation(0,-p.Navel,0)*NM.CreateRotationX(relative*t*MathF.PI/180)*NM.CreateTranslation(0,p.Navel,0);
                        bones[i].localToWorldMatrix=new(){Rows=rest[i]*worldTorso*extra*rotation*torsoWorld};
                    }
                }
                var poseReports=new List<object>();
                foreach(float relative in new[]{0f,20f,40f,60f})
                {
                    Pose(25,relative);UpdateALBindings(maid);
                    var ev=VirtualAxisMath.Evaluate(c.Axis,c.PelvisBind*MatrixBridge.ToManaged(c.Pelvis.localToWorldMatrix),c.SpineBind*MatrixBridge.ToManaged(c.Spine.localToWorldMatrix),Shape);
                    var positions=new GV[original.Length];float error=0,armMove=0;
                    for(int i=0;i<original.Length;i++)
                    {
                        var actual=SkinPoint(body,changed[i],i);var native=Vector3.zero;
                        foreach(var w in Influences(weights[i]))if(w.Weight>0)native+=(bones[w.Bone].localToWorldMatrix*b.Binds[w.Bone]).MultiplyPoint3x4(changed[i])*w.Weight;
                        var virtualP=GV.Transform(N(changed[i]),r.ToReference*ev.Transform);
                        var expected=N(native)*(1-alpha[i])+virtualP*alpha[i];
                        error=Math.Max(error,(N(actual)-expected).Length());
                        positions[i]=N((Vector3)NV.Transform(actual,worldTorso));
                        if(BoneSum(weights[i],bones,ArmBone)>.999f)armMove=Math.Max(armMove,(actual-native).magnitude);
                    }
                    Check(error<2e-5f,"study palette matches full equation");Check(armMove<1e-6f,"study arms remain native");
                    if(stage==1)
                    {
                        using var csv=new StreamWriter(Path.Combine(folder,$"{variant}-pose-{relative:0}.csv"));
                        csv.WriteLine("x,y,z,px,py,pz,alpha,arm");
                        for(int i=0;i<original.Length;i++)csv.WriteLine(FormattableString.Invariant($"{material[i].X:R},{material[i].Y:R},{material[i].Z:R},{positions[i].X:R},{positions[i].Y:R},{positions[i].Z:R},{alpha[i]:R},{BoneSum(weights[i],bones,ArmBone):R}"));
                    }
                    poseReports.Add(new{relativeSpineDegrees=relative,targetAxisDegrees=ev.AngleDegrees,virtualTurnDegrees=ev.AngleDegrees*ev.Pull,maxSkinEquationError=error,maxArmError=armMove});
                }
                var skinIds=body.sharedMesh.GetTriangles(0).ToHashSet();
                var front=Enumerable.Range(0,original.Length).Where(i=>skinIds.Contains(i) && Math.Abs(material[i].X)<.025f && material[i].Y>.04f && material[i].Y<.32f && material[i].Z>p.AxisAt(material[i].Y) && alpha[i]>0).ToArray();
                var bands=Enumerable.Range(0,7).Select(k=>{float lo=.04f+k*.04f;var ids=front.Where(i=>material[i].Y>=lo&&material[i].Y<lo+.04f).ToArray();return new{lo,hi=lo+.04f,count=ids.Length,meanAlpha=ids.Length==0?0:ids.Average(i=>alpha[i]),minAlpha=ids.Length==0?0:ids.Min(i=>alpha[i]),maxAlpha=ids.Length==0?0:ids.Max(i=>alpha[i])};}).ToArray();
                var perf=new List<object>();
                foreach(bool moving in new[]{false,true})
                {
                    for(int i=0;i<150;i++){Pose(25,moving?30+10*MathF.Sin(i*.03f):40);UpdateALBindings(maid);}
                    const int n=1200;var times=new double[n];long bytes=0;
                    for(int i=0;i<n;i++)
                    {
                        Pose(25,moving?30+10*MathF.Sin(i*.03f):40);
                        long startBytes=GC.GetAllocatedBytesForCurrentThread();long stamp=Stopwatch.GetTimestamp();
                        UpdateALBindings(maid);
                        times[i]=Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;bytes+=GC.GetAllocatedBytesForCurrentThread()-startBytes;
                    }
                    Array.Sort(times);perf.Add(new{moving,frames=n,medianMs=times[n/2],p95Ms=times[(int)(n*.95)],maxMs=times[n-1],bytesPerFrame=bytes/(double)n});
                }
                reports.Add(new{variant,stage,p.Span,p.Navel,p.Ribs,spine=c.Spine.name,vertices=original.Length,nativeBones=b.Bones.Length,virtualSlots=b.Recipes.Length,fusedSlots=b.Recipes.Count(q=>q.Bone>=0),alphaPositive=alpha.Count(a=>a>0),alphaFull=alpha.Count(a=>a>=.999f),lowerEnd=(Shape.LowerTransitionStart+Shape.LowerTransitionWidth)*p.Span,upperStart=VirtualAxisMath.UpperStart(p,Shape),prepareMs,deformMs,installMs,rebuildBytes,bands,poses=poseReports,perf});
                Console.WriteLine(JsonSerializer.Serialize(reports.Last()));
                File.WriteAllText(Path.Combine(folder,variant+"-settings.json"),JsonSerializer.Serialize(Shape,new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
                ReleaseALBindings(maid);_alContexts.Remove(maid.GetHashCode());
            }
        }
        File.WriteAllText(Path.Combine(folder,"study.json"),JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
        Shape=originalSettings;
    }
}
