using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void TestGrowthTimeline()
    {
        Shape=new VtxSettings();var p=Shape;Check(GrowthTimeline.Valid(p),"valid defaults");
        Check(GrowthTimeline.Map(0,p)==0 && GrowthTimeline.Map(1,p)==1,"zero and full-term anchors");
        Check(Math.Abs(GrowthTimeline.Map(p.SubtleStageProgress,p)-GrowthTimeline.SubtleShapeStage)<1e-7f,"configured subtle anchor");
        Check(Math.Abs(GrowthTimeline.Map(p.VisibleStageProgress,p)-GrowthTimeline.VisibleShapeStage)<1e-7f,"configured visible anchor");
        Check(Math.Abs(GrowthTimeline.Map(p.MidStageProgress,p)-GrowthTimeline.MidShapeStage)<1e-7f,"configured middle anchor");
        var legacy=p.Copy();legacy.SubtleStageProgress=1f/3;legacy.VisibleStageProgress=4f/9;legacy.MidStageProgress=5f/9;
        float previous=-1;
        for(int i=0;i<=10000;i++)
        {
            float progress=i/10000f,mapped=GrowthTimeline.Map(progress,p);
            Check(mapped>=previous && float.IsFinite(mapped),"monotone finite progress");previous=mapped;
            if(progress>=5f/9)Check(GrowthTimeline.Map(progress,legacy)==progress,"legacy late-stage identity preserved");
        }
        foreach(var knot in new[]{p.SubtleStageProgress,p.VisibleStageProgress,p.MidStageProgress})
            Check(Math.Abs(GrowthTimeline.Map(knot-1e-6f,p)-GrowthTimeline.Map(knot+1e-6f,p))<1e-5f,"continuous across timing controls");
        var (maid,body,cloth)=Fixture();PrepareALContext(maid,new(){body,cloth},.4f);var profile=_alWorking.Frame.Profile;
        foreach(string name in new[]{"SubtleStageProgress","VisibleStageProgress","MidStageProgress"})
        {
            Shape=p.Copy();var field=typeof(VtxSettings).GetField(name);int hash=ComputeMorphBakeSignature(.4f,MeshMorphClass.Body);
            var before=BellyShape.Growth(profile,.4f,Shape);field.SetValue(Shape,(float)field.GetValue(Shape)+.03f);
            Check(hash!=ComputeMorphBakeSignature(.4f,MeshMorphClass.Body),"timing invalidates morph cache "+name);
            // Test each control near its own timeline segment, not an unaffected segment.
            float at=name=="MidStageProgress"?(p.VisibleStageProgress+p.MidStageProgress)*.5f:(p.SubtleStageProgress+p.VisibleStageProgress)*.5f;
            Check(BellyShape.Growth(profile,at,Shape)!=BellyShape.Growth(profile,at,p),"timing has effect "+name);
            var axis=VirtualAxisMath.Reference(profile,at,Shape);
            var mappedEgg=BellyShape.GrowthAtStage(profile,Math.Max(GrowthTimeline.Map(at,Shape),1f/3),Shape);
            float y=(mappedEgg.Bottom+mappedEgg.Top)*.5f+(mappedEgg.Top-mappedEgg.Bottom)*.1875f;
            Check(Math.Abs(axis.Upper.Y-y)<1e-6f,"binding uses same mapped egg "+name);
        }
        var invalid=p.Copy();invalid.SubtleStageProgress=2;invalid.VisibleStageProgress=-1;
        Check(!GrowthTimeline.Valid(invalid),"out-of-order/out-of-range timing detected");
        Check(GrowthTimeline.Map(.4f,invalid)==GrowthTimeline.Map(.4f,legacy),"invalid timing retains the existing fixed fallback");
        invalid.MidStageProgress=float.NaN;Check(float.IsFinite(GrowthTimeline.Map(.8f,invalid)),"nonfinite programmatic settings guarded");
        var saved=new VtxSettings{SubtleStageProgress=0,VisibleStageProgress=0,MidStageProgress=0};
        saved.CompleteGrowthDefaults("{}");Check(GrowthTimeline.Valid(saved),"missing legacy fields receive default times");
        saved.SubtleStageProgress=.28f;saved.CompleteGrowthDefaults("{\"SubtleStageProgress\":0.28}");Check(saved.SubtleStageProgress==.28f,"saved timing retained");
        Shape=p;Console.WriteLine($"PASS: {tests} timeline, continuity, cache, migration and matching binding-volume assertions.");
    }
}
