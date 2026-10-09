using System;
using System.Collections.Generic;
using System.Linq;
using COM3D2.Pregnancy.Plugin.Growth.Numerics;

namespace COM3D2.Pregnancy.Plugin.Growth;

// Row-vector matrices throughout. No Unity pose or mesh access in this kernel.
internal static class VirtualAxisMath
{
    internal readonly record struct Axis(Vector3 Anchor, Vector3 Upper);
    internal readonly record struct Result(Matrix4x4 Transform, float AngleDegrees, float Pull, Vector3 Anchor, Vector3 FreeAxis, Vector3 TargetAxis);

    internal static Axis Reference(TorsoProfile torso,float stage,VtxSettings p)
    {
        var seed=BellyShape.GrowthAtStage(torso,1f/3,p);
        var egg=BellyShape.GrowthAtStage(torso,Scalar.Max(GrowthTimeline.Map(stage,p),1f/3),p);
        // The initial occupied centre is a material anchor, not the growing egg's pole.
        var anchor=torso.HasCervix ? torso.Cervix : new Vector3(0,(seed.Bottom+seed.Top)*.5f,seed.AnteriorOffset);
        anchor+=new Vector3(0,p.AxisAnchorY*torso.Span,p.AxisAnchorZ*torso.Span);
        // Centroid of the upper half of a solid ellipsoid: 3/8 of its vertical semiaxis.
        float upperY=(egg.Bottom+egg.Top)*.5f+(egg.Top-egg.Bottom)*.1875f;
        var upper=new Vector3(0,upperY,egg.AnteriorAt(upperY));
        if(Vector3.DistanceSquared(anchor,upper)<torso.Span*torso.Span*.0025f)upper=anchor+Vector3.UnitY*torso.Span*.05f;
        return new Axis(anchor,upper);
    }

    internal static Result Evaluate(Axis rest,Matrix4x4 pelvis,Matrix4x4 spine,VtxSettings p)
    {
        var anchor=Vector3.Transform(rest.Anchor,pelvis);
        var free=Unit(Vector3.TransformNormal(rest.Upper-rest.Anchor,pelvis),Vector3.UnitY);
        var target=Unit(Vector3.Transform(rest.Upper,spine)-anchor,free);
        float cosine=Scalar.Clamp(Vector3.Dot(free,target),-1,1);
        float angle=Scalar.Acos(cosine),degrees=angle*(180/Scalar.PI);
        float low=p.AxisPullLow,high=p.AxisPullHigh;
        float pull=low+(high-low)*BellyShape.Smooth(degrees/Scalar.Max(1e-6f,Scalar.Abs(p.AxisPullAngle)));
        var turnAxis=Vector3.Cross(free,target);
        if(turnAxis.LengthSquared()<1e-10f)
        {
            if(cosine>0)return new Result(pelvis,degrees,pull,anchor,free,target);
            // Antiparallel fallback rotates with the character, not with world axes.
            var right=Vector3.TransformNormal(Vector3.UnitX,pelvis);
            turnAxis=right-free*Vector3.Dot(right,free);
        }
        var turn=Matrix4x4.CreateFromAxisAngle(Unit(turnAxis,Vector3.UnitX),angle*pull);
        var matrix=pelvis*Matrix4x4.CreateTranslation(-anchor)*turn*Matrix4x4.CreateTranslation(anchor);
        return new Result(matrix,degrees,pull,anchor,free,target);
    }

    internal static float Weight(float displacement,float span,VtxSettings p)
    {
        float start=p.AxisBlendStart,end=p.AxisBlendFull;
        return p.VirtualAxisStrength*BellyShape.Smooth((displacement/Scalar.Max(span,1e-5f)-start)/NonZero(end-start));
    }

    // A displacement threshold alone can switch the entire lower wall to the
    // pelvis carrier within a few tightly spaced rows. Retain native attachment
    // across a continuous anatomical length, reaching full virtual support near
    // the navel. This is a rest/material coordinate, shared with clothing, and
    // does not depend on pose angle, mesh density, or world orientation.
    internal static float SurfaceWeight(float displacement, float materialY, TorsoProfile torso, VtxSettings p)
    {
        float start = torso.PelvicFloor + Parameter(p.LowerTransitionStart, .02f, -.50f, .75f) * torso.Span;
        float u = Scalar.Clamp((materialY - start) / Scalar.Max(torso.Span * Parameter(p.LowerTransitionWidth, .60f, .02f, 1.50f), 1e-5f), 0, 1);
        float attachment = LowerAttachment(u, p.LowerTransitionBias);
        return Weight(displacement, torso.Span, p) * attachment;
    }

    internal static float NonZero(float value) => Scalar.Abs(value)<1e-6f ? (value<0 ? -1e-6f : 1e-6f) : value;

    internal static float Parameter(float value, float fallback, float min, float max)
        => Scalar.IsFinite(value) ? value : fallback;

    internal static float UpperStart(TorsoProfile torso, VtxSettings p)
        => torso.Navel + Parameter(p.UpperTransitionStart, .15f, -.50f, 1f) * torso.Span;

    internal static float UpperRelease(float u, float bias) => 1 - LowerAttachment(u, -bias);

    // Built into skin weights on a shape rebuild, never evaluated per pose.
    // Carry the deformation at the upper band's entrance up the same material
    // meridian, then release it by HEIGHT. Do not multiply a second fade into
    // the current vertex's already-falling displacement weight.
    internal static float MaterialSurfaceWeight(float displacement, Vector3 original,
        TorsoProfile torso, float stage, VtxSettings p, float influence = 1f)
    {
        if (stage <= 0 || !Scalar.IsFinite(displacement) || displacement <= 0 || influence <= 0) return 0;
        float baseline = SurfaceWeight(displacement, original.Y, torso, p);
        float start = UpperStart(torso, p);
        if (original.Y <= start) return baseline;
        float width = Parameter(p.UpperTransitionWidth, .55f, .02f, 1.50f) * torso.Span;
        float u = (original.Y - start) / Scalar.Max(width, 1e-5f);
        if (u >= 1 || displacement <= 0 || stage <= 0 || p.VirtualAxisStrength <= 0) return 0;
        float angle = Scalar.Atan2(original.X, original.Z - torso.AxisAt(original.Y));
        if (Scalar.Abs(angle) >= Scalar.PI * .5f) return 0;
        float sn = Scalar.Sin(angle), cs = Scalar.Cos(angle);
        float Radius(float y)
        {
            float w = Scalar.Max(torso.Span * .01f, torso.WidthAt(y));
            float d = Scalar.Max(torso.Span * .01f, (torso.FrontAt(y) - torso.BackAt(y)) * .5f);
            return 1 / Scalar.Sqrt(sn * sn / (w * w) + cs * cs / (d * d));
        }
        // Preserve the original surface's radial detail while transporting it
        // to the anchor height; identical material points need identical rules.
        float radial = Scalar.Sqrt(original.X * original.X + Scalar.Pow(original.Z - torso.AxisAt(original.Y), 2));
        float radius = Scalar.Max(0, Radius(start) + radial - Radius(original.Y));
        var anchor = new Vector3(radius * sn, start, torso.AxisAt(start) + radius * cs);
        float anchorDelta = Vector3.Distance(anchor, BellyShape.Deform(anchor, torso, stage, p)) * Parameter(influence, 1, 0, 1);
        float entry = SurfaceWeight(anchorDelta, start, torso, p);
        float target = entry * UpperRelease(u, p.UpperTransitionBias);
        float activation = BellyShape.Smooth(displacement / Scalar.Max(torso.Span * Parameter(p.UpperTransitionActivation, .02f, .001f, .15f), 1e-5f));
        target *= activation;
        // A short zero-slope join preserves the lower formula exactly below
        // the chosen start, even on detailed skin or overlapping user bands.
        float join = BellyShape.Smooth(u / Parameter(p.UpperTransitionJoin, .20f, .02f, 1f));
        return baseline + (target - baseline) * join;
    }

    // Bias the interior of the same material interval, preserving both endpoint
    // values and zero endpoint slopes. The explicit zero branch keeps old
    // settings bit-for-bit identical, including configs without the new field.
    internal static float LowerAttachment(float u, float bias)
    {
        u = Scalar.Clamp(u, 0, 1);
        bias = Scalar.IsFinite(bias) ? bias : 0;
        if (bias != 0)
        {
            if(u<=0 || u>=1)return u;
            if(bias>=0){float inverse=Scalar.Pow(2,-bias);u=u*inverse/(1-u+u*inverse);}
            else u/=u+(1-u)*Scalar.Pow(2,bias);
        }
        return u * u * (3 - 2 * u);
    }

    // Historical replay helper only. Runtime uses MaterialSurfaceWeight.
    // Upper skin can fold when the displacement-based blend falls sharply
    // between adjacent rows. Bias that existing blend, rather than forcing it
    // to zero at the ribs (which can collapse the expanded upper wall).
    // Fade this control in above BOTH the navel and the lower transition.
    internal static float UpperAttachment(float weight, float materialY, TorsoProfile torso, float bias, float ceiling = 1f)
    {
        bias = Scalar.IsFinite(bias) ? bias : 0;
        if (bias == 0 || weight <= 0 || weight >= ceiling) return weight;
        float start = Scalar.Max(torso.Navel + .15f * torso.Span, torso.PelvicFloor + .62f * torso.Span);
        float extent = torso.Ribs - start;
        if (extent <= 1e-5f || materialY <= start) return weight;
        float u = Scalar.Clamp((materialY - start) / extent, 0, 1);
        float mask = u * u * (3 - 2 * u);
        float remapped = ceiling * weight / (weight + (ceiling - weight) * Scalar.Pow(2, bias));
        return weight + (remapped - weight) * mask;
    }

    // Reuse an existing pelvis Transform; its extra bind matrix supplies the
    // independent virtual transform. No Transform or hierarchy bone is created.
    internal static bool PaletteBind(Matrix4x4 meshToReference,Matrix4x4 virtualToWorld,Matrix4x4 carrierToWorld,out Matrix4x4 bind)
    {
        bind=default;
        if(!Matrix4x4.Invert(carrierToWorld,out var inverse))return false;
        bind=meshToReference*virtualToWorld*inverse;
        return Finite(bind);
    }
    internal static Vector3 Unit(Vector3 v,Vector3 fallback)=>v.LengthSquared()>1e-12f?Vector3.Normalize(v):fallback;
    internal static bool Finite(Matrix4x4 m)=>Scalar.IsFinite(m.M11+m.M12+m.M13+m.M14+m.M21+m.M22+m.M23+m.M24+m.M31+m.M32+m.M33+m.M34+m.M41+m.M42+m.M43+m.M44);
}
