using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using GV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;

namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunSagTests(string[] args)
    {
        string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        var body=ReadModel(args[2],"body-sag");var maid=new Maid();
        if(args.Length>3)body.sharedMesh.vertices=ReadPoints(args[3]);
        PrepareALContext(maid,new(){body},1);var context=_alWorking;var profile=context.Frame.Profile;
        var original=body.sharedMesh.vertices;
        var material=original.Select(v=>GV.Transform(N(v),context.Maps[body]*context.InverseFrame)).ToArray();
        // Isolate sag from the separately updated default navel timeline.
        var saved=Shape.Copy();saved.NavelPreviewFull=true;saved.BellySag=0;var reports=new List<object>();
        Check(Scalar.IsFinite(profile.SkinNavelZ),"actual model navel detected");
        int exactComparisons=0;float maxNavelTranslationSpread=0,maxPlaneDifference=0;
        foreach(float legacy in new[]{saved.SagStrength,0f,.6f,2f})
        foreach(float stage in new[]{0f,saved.SubtleStageProgress,saved.VisibleStageProgress,saved.MidStageProgress,.812f,1f})
        {
            var settings=saved.Copy();settings.SagStrength=legacy;settings.BellySag=0;
            // Batch each implementation: their private cache types intentionally differ.
            var baseline=material.Select(v=>V9BellyShape.Deform(v,profile,stage,settings)).ToArray();
            var baselineOuter=material.Select(v=>V9BellyShape.DeformOuterCloth(v,profile,stage,settings)).ToArray();
            var current=material.Select(v=>BellyShape.Deform(v,profile,stage,settings)).ToArray();
            for(int i=0;i<material.Length;i++)
            {
                Check(current[i].Equals(baseline[i]),"sag zero: body exactly equals v9");
                Check(BellyShape.DeformOuterCloth(material[i],profile,stage,settings).Equals(baselineOuter[i]),"sag zero: outer field exactly equals v9");
                exactComparisons+=2;
            }
            float previous=0;
            foreach(float sag in new[]{.5f,1f,2f})
            {
                settings.BellySag=sag;float peakDrop=0,maxXZ=0;int moved=0;
                var result=material.Select(v=>BellyShape.Deform(v,profile,stage,settings)).ToArray();
                for(int i=0;i<material.Length;i++)
                {
                    var delta=result[i]-baseline[i];
                    Check(Scalar.IsFinite(result[i].X+result[i].Y+result[i].Z),"finite sag vertex");
                    Check(delta.X==0 && delta.Z==0,"new sag changes vertical position only");
                    Check(delta.Y<=1e-7f,"positive sag only moves down");
                    peakDrop=Math.Max(peakDrop,-delta.Y);maxXZ=Math.Max(maxXZ,Math.Abs(delta.X)+Math.Abs(delta.Z));
                    if(delta.Y< -1e-7f)moved++;
                    if(stage==0)Check(delta.Equals(GV.Zero),"stage zero identity with sag enabled");
                    var outer=BellyShape.DeformOuterCloth(material[i],profile,stage,settings)-baselineOuter[i];
                    Check(outer.X==0 && outer.Z==0 && outer.Y<=1e-7f,"outer fallback receives only downward translation");
                }
                Check(peakDrop>=previous,"sag strength increases drop");previous=peakDrop;
                if(stage==1)Check(peakDrop>profile.Span*.005f,"full belly sag visible");

                float rad=Math.Max(Math.Abs(settings.NavelRadius)*profile.Span,profile.Span*.005f);
                float cy=profile.SkinNavelY+settings.NavelVerticalOffset*profile.Span;
                var ring=new GV[24];var after=new GV[24];float minDrop=float.MaxValue,maxDrop=float.MinValue;
                var off=settings.Copy();off.BellySag=0;
                for(int k=0;k<ring.Length;k++)
                {
                    float angle=k*MathF.Tau/ring.Length;
                    float x=rad*.95f*MathF.Cos(angle),y=cy+1.3f*rad*.95f*MathF.Sin(angle);
                    var point=new GV(x,y,profile.WallAt(x,y));
                    ring[k]=BellyShape.Deform(point,profile,stage,off);after[k]=BellyShape.Deform(point,profile,stage,settings);
                    float drop=ring[k].Y-after[k].Y;minDrop=Math.Min(minDrop,drop);maxDrop=Math.Max(maxDrop,drop);
                }
                float spread=maxDrop-minDrop;maxNavelTranslationSpread=Math.Max(maxNavelTranslationSpread,spread);
                Check(spread<profile.Span*2e-6f,"entire navel rim translates by the same amount");
                Check(NavelPlane.Fit(ring,1,out var beforePlane) && NavelPlane.Fit(after,1,out _),"navel planes fit");
                NavelPlane.Fit(after,1,out var afterPlane);
                float planeDifference=GV.Distance(new GV(beforePlane.M31,beforePlane.M32,beforePlane.M33),new GV(afterPlane.M31,afterPlane.M32,afterPlane.M33));
                maxPlaneDifference=Math.Max(maxPlaneDifference,planeDifference);
                Check(planeDifference<2e-5f,"navel direction preserved");

                int newReversals=0;float previousBefore=0,previousAfter=0;
                for(int k=0;k<=800;k++)
                {
                    float y=profile.PelvicFloor+(profile.Ribs-profile.PelvicFloor)*k/800;
                    var point=new GV(0,y,profile.FrontAt(y));
                    float before=BellyShape.Deform(point,profile,stage,off).Y,shifted=BellyShape.Deform(point,profile,stage,settings).Y;
                    if(k>0 && before>previousBefore+1e-8f && shifted<=previousAfter)newReversals++;
                    previousBefore=before;previousAfter=shifted;
                }
                Check(newReversals==0,$"sag introduces no front-meridian row reversals: legacy={legacy}, stage={stage}, sag={sag}");
                reports.Add(new{legacy,stage,sag,peakDrop,moved,maxXZ,navelTranslationSpread=spread,navelNormalDifference=planeDifference,newReversals});
                if(legacy==saved.SagStrength && stage==1)
                    File.WriteAllText(Path.Combine(output,$"body-sag-{sag:0.0}.json"),JsonSerializer.Serialize(new{
                        span=profile.Span,navelY=cy,navelRadius=rad,
                        original=material.Select(v=>new[]{v.X,v.Y,v.Z}),
                        before=baseline.Select(v=>new[]{v.X,v.Y,v.Z}),
                        after=result.Select(v=>new[]{v.X,v.Y,v.Z}),triangles=body.sharedMesh.GetTriangles(0)}));
            }
        }
        // Forward extension, not rest-wall height or navel micro-detail, drives drop.
        var simple=new TorsoProfile{PelvicFloor=0,Pubis=.1f,Navel=1.1f,Ribs=2.7f,SampleMin=0,SampleMax=3,
            Front=Enumerable.Repeat(.5f,21).ToArray(),Back=Enumerable.Repeat(-.5f,21).ToArray(),Width=Enumerable.Repeat(.9f,21).ToArray()};
        var plain=saved.Copy();plain.SagStrength=plain.BellySag=0;plain.NavelEversion=plain.NavelProportion=0;
        var drops=new List<(float extension,float drop)>();
        for(int i=0;i<200;i++)
        {
            float y=.1f+i*.013f;var point=new GV(0,y,.5f);var before=BellyShape.Deform(point,simple,1,plain);
            var on=plain.Copy();on.BellySag=1;var after=BellyShape.Deform(point,simple,1,on);
            float extension=before.Z-point.Z,drop=before.Y-after.Y;
            Check(Math.Abs(drop-.075f*extension)<2e-6f,"more forward growth means proportionally more sag");
            drops.Add((extension,drop));
        }
        var ordered=drops.OrderBy(d=>d.extension).ToArray();
        for(int i=1;i<ordered.Length;i++)Check(ordered[i].drop>=ordered[i-1].drop-2e-7f,"sag monotone with extension");
        var restored=new VtxSettings{SagStrength=-.71f,BellySag=8};restored.CompleteGrowthDefaults("{\"SagStrength\":-0.71}");
        Check(restored.SagStrength==-.71f && restored.BellySag==new VtxSettings().BellySag,"old save retains legacy value, missing new sag receives current default");
        restored.BellySag=.6f;restored.CompleteGrowthDefaults("{\"SagStrength\":-0.71,\"BellySag\":0.6}");
        Check(restored.BellySag==.6f,"saved new sag retained");
        restored.BellySag=0;restored.CompleteGrowthDefaults("{\"BellySag\":0}");Check(restored.BellySag==0,"explicit sag zero retained");
        Shape=saved.Copy();int hash=ComputeMorphBakeSignature(1,MeshMorphClass.Body);Shape.BellySag+=1e-6f;
        Check(hash!=ComputeMorphBakeSignature(1,MeshMorphClass.Body),"new sag invalidates bake signature");Shape=saved;
        var actualNavel=new List<object>();
        foreach(float stage in new[]{saved.VisibleStageProgress,.812f,1f})
        {
            var navelPlanes=new List<PreparationPlane>();
            foreach(float sag in new[]{0f,1f,2f})
            {
                Shape=saved.Copy();Shape.BellySag=sag;
                PrepareALContext(maid,new(){body},stage);var c=_alWorking;var t=c.Frame.Profile;
                var record=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=original};
                Check(TryDeformAL(body,record,MeshMorphClass.Body,stage,out _,out _),"sag complete body adapter");
                float radius=Math.Max(Math.Abs(Shape.NavelRadius)*t.Span,t.Span*.005f),center=t.SkinNavelY+Shape.NavelVerticalOffset*t.Span;
                Check(TryNavelBoundary(record,radius,center,out var attachment),"actual mesh navel triangle boundary");
                var frame=attachment.BakedFrame;
                var normal=new GV(frame.M31,frame.M32,frame.M33);
                var points=attachment.MorphedRing;
                navelPlanes.Add(new PreparationPlane{Normal=normal,Points=points});
                float difference=GV.Distance(normal,navelPlanes[0].Normal);
                var delta=points[0]-navelPlanes[0].Points[0];
                float spread=points.Select((p,i)=>GV.Distance(p-navelPlanes[0].Points[i],delta)).Max();
                Check(difference<2e-5f,"actual navel triangle boundary keeps its orientation");
                Check(spread<t.Span*2e-6f,"actual navel triangle boundary translates rigidly");
                Console.WriteLine($"Actual navel stage={stage} sag={sag}: normal difference={difference:R}, translation spread={spread:R}");
                actualNavel.Add(new{stage,sag,normalDifference=difference,translationSpread=spread});
            }
        }
        Shape=saved;
        File.WriteAllText(Path.Combine(output,"sag-metrics.json"),JsonSerializer.Serialize(new{assertions=tests,exactComparisons,
            maxNavelTranslationSpread,maxPlaneDifference,span=profile.Span,actualNavel,rows=reports},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {tests} sag assertions; {exactComparisons} exact v9 comparisons; navel translation spread {maxNavelTranslationSpread:R}; navel normal difference {maxPlaneDifference:R}");
    }
    sealed class PreparationPlane { internal GV Normal; internal GV[] Points; }
}
