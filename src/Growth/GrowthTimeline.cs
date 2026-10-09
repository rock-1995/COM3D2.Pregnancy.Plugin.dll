using System;

namespace COM3D2.Pregnancy.Plugin.Growth
{
    // Pregnancy progress remains owned by PregnancyManager. Only the shape's
    // position along its existing volume/binding curve is remapped here.
    internal static class GrowthTimeline
    {
        internal const float SubtleShapeStage=.4375f;
        internal const float VisibleShapeStage=.46f;
        internal const float MidShapeStage=5f/9;
        internal static bool Valid(VtxSettings p)
            => Scalar.IsFinite(p.SubtleStageProgress) && Scalar.IsFinite(p.VisibleStageProgress) && Scalar.IsFinite(p.MidStageProgress)
               && p.SubtleStageProgress>0 && p.VisibleStageProgress-p.SubtleStageProgress>=.0001f
               && p.MidStageProgress-p.VisibleStageProgress>=.0001f && p.MidStageProgress<1;

        internal static float Map(float progress,VtxSettings p)
        {
            if(progress<=0)return 0;if(progress>=1)return 1;
            bool valid=Valid(p);
            float subtle=valid?p.SubtleStageProgress:1f/3,visible=valid?p.VisibleStageProgress:4f/9,mid=valid?p.MidStageProgress:5f/9;
            if(progress<=subtle)return Segment(progress,0,subtle,0,SubtleShapeStage);
            if(progress<=visible)return Segment(progress,subtle,visible,SubtleShapeStage,VisibleShapeStage);
            if(progress<=mid)return Segment(progress,visible,mid,VisibleShapeStage,MidShapeStage);
            return Segment(progress,mid,1,MidShapeStage,1);
        }
        static float Segment(float value,float from,float to,float shapeFrom,float shapeTo)
            => from==shapeFrom && to==shapeTo ? value : shapeFrom+(shapeTo-shapeFrom)*((value-from)/(to-from));
    }
}
