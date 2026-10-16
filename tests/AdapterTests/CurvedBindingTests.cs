using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using GV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
using GM=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;
using NM=System.Numerics.Matrix4x4;

namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunCurvedBindingTests(string[] args)
    {
        int startTests=tests;
        var (maid,body,cloth)=Fixture();
        PrepareALContext(maid,new(){body,cloth},1);
        var t=_alWorking.Frame.Profile;
        var settings=Shape.Copy();
        settings.LowerTransitionStart=.02f;settings.LowerTransitionWidth=.60f;
        settings.UpperTransitionStart=.15f;settings.UpperTransitionWidth=.55f;
        settings.LowerTransitionBias=0;settings.VirtualAxisStrength=1;
        float span=t.Span, lower=t.PelvicFloor+.02f*span, upper=VirtualAxisMath.UpperStart(t,settings);
        GV Point(float y,float angle,float radial=1) => new(span*.3f*radial*MathF.Sin(angle),y,t.AxisAt(y)+span*.3f*radial*MathF.Cos(angle));
        float rise0=VirtualAxisMath.TransitionArc(Point(t.Navel,0),t);
        Check(Math.Abs(rise0/span-.10f)<1e-6f,"front centre arc height is ten percent of span");
        Check(VirtualAxisMath.TransitionArc(new GV(span,t.Navel,t.AxisAt(t.Navel)),t)==0,"arc meets original height at side");
        Check(VirtualAxisMath.TransitionArc(new GV(0,t.Navel,t.AxisAt(t.Navel)-span),t)==0,"arc does not wrap around the back");
        foreach(float arc in new[]{0f,.05f,.1f,.25f,-.1f,.7f})
        {
            var tuned=settings.Copy();tuned.LowerBoundaryArc=arc;tuned.UpperBoundaryArc=.23f;
            foreach(float angle in new[]{0f,.4f,.8f})
            {
                float shift=VirtualAxisMath.TransitionArc(Point(t.Navel,angle),t,arc);
                Check(Math.Abs(shift/span-arc*MathF.Cos(angle)*MathF.Cos(angle))<1e-6f,"configured arc scales without an extra range clamp");
                float mid=lower-shift+.5f*settings.LowerTransitionWidth*span;
                Check(Math.Abs(VirtualAxisMath.MaterialSurfaceWeight(span*.4f,Point(mid,angle),t,1,tuned)-.5f)<2e-5f,"configured lower contour reaches midpoint");
                tuned.UpperBoundaryArc=.08f;
                Check(Math.Abs(VirtualAxisMath.MaterialSurfaceWeight(span*.4f,Point(mid,angle),t,1,tuned)-.5f)<2e-5f,"upper arc does not alter lower contour");
            }
        }
        var flat=settings.Copy();flat.LowerBoundaryArc=flat.UpperBoundaryArc=0;
        Check(VirtualAxisMath.TransitionArc(Point(t.Navel,0),t,0)==0,"zero arc restores flat boundary");
        for(int k=0;k<10;k++)
        {
            float y=lower+k*settings.LowerTransitionWidth*span/9;
            Check(Math.Abs(VirtualAxisMath.MaterialSurfaceWeight(span*.4f,Point(y,.4f),t,1,flat)-VirtualAxisMath.SurfaceWeight(span*.4f,y,t,flat))<1e-6f,"zero lower arc matches original horizontal rule");
        }
        var flatUpper=settings.Copy();flatUpper.UpperBoundaryArc=0;
        var raisedUpper=settings.Copy();raisedUpper.UpperBoundaryArc=.3f;
        float probe=upper+.2f*span;
        Check(VirtualAxisMath.MaterialSurfaceWeight(span*.4f,Point(probe,0),t,1,raisedUpper)>
            VirtualAxisMath.MaterialSurfaceWeight(span*.4f,Point(probe,0),t,1,flatUpper)+.01f,"upper arc setting moves actual release upward");
        float previous=rise0;
        for(int k=0;k<=100;k++)
        {
            float angle=k*MathF.PI/200;
            float rise=VirtualAxisMath.TransitionArc(Point(t.Navel,angle),t);
            Check(rise<=previous+1e-6f&&rise>=0,"arc decreases smoothly toward side");previous=rise;
            Check(Math.Abs(rise-VirtualAxisMath.TransitionArc(Point(t.Navel,-angle),t))<1e-6f,"left/right arcs symmetric");
            Check(Math.Abs(rise-VirtualAxisMath.TransitionArc(Point(t.Navel+span*.3f,angle,1.4f),t))<1e-6f,"one meridian shares offset at transported height/radius");
            Check(upper+rise>=upper&&lower-rise<=lower,"upper bows up and lower bows down");
            float lowStart=lower-rise, lowEnd=lowStart+settings.LowerTransitionWidth*span;
            float highStart=upper+rise, highEnd=highStart+settings.UpperTransitionWidth*span;
            Check(Math.Abs((lowEnd-lowStart)/span-settings.LowerTransitionWidth)<1e-6f,"lower band width preserved");
            Check(Math.Abs((highEnd-highStart)/span-settings.UpperTransitionWidth)<1e-6f,"upper band width preserved");
            foreach(float u in new[]{0f,.1f,.25f,.5f,.75f,.9f,1f})
            {
                var original=Point(lowStart+u*settings.LowerTransitionWidth*span,angle);
                float weight=VirtualAxisMath.MaterialSurfaceWeight(span*.4f,original,t,1,settings);
                Check(Math.Abs(weight-VirtualAxisMath.LowerAttachment(u,0))<2e-5f,"runtime lower weights follow curved contour");
            }
            float eps=span*1e-4f;
            float Value(float y)=>VirtualAxisMath.MaterialSurfaceWeight(span*.4f,Point(y,angle),t,1,settings);
            // At exactly 90 degrees the existing front-only guard excludes the
            // surface; a synthetic nonzero displacement there is not physical.
            if(k<100)Check(Math.Abs(Value(highStart-eps)-Value(highStart+eps))<.002f,$"upper curved entrance is continuous (meridian {k})");
            Check(Math.Abs(Value(highEnd-eps))<.002f&&Value(highEnd+eps)==0,"upper curved exit meets native attachment continuously");
            for(int y=0;y<=100;y++)
            {
                float h=lower-span*.2f+y*(highEnd-lower+span*.4f)/100;
                float w=Value(h);
                Check(float.IsFinite(w)&&w>=0&&w<=1.00001f,"curved attachment stays finite and bounded");
                Check(Math.Abs(w-VirtualAxisMath.MaterialSurfaceWeight(span*.4f,Point(h,-angle),t,1,settings))<2e-5f,"full runtime attachment is symmetric");
            }
        }
        // Curvature does not introduce pose/world axes into the skin weights.
        ApplySMR(maid,body,1,MeshMorphClass.Body,false);ApplySMR(maid,cloth,1,MeshMorphClass.OuterCloth,false);
        foreach(var renderer in new[]{body,cloth})
        {
            var record=FindRecord(maid,renderer);var b=_alBindings[renderer];
            int writes=renderer.sharedMesh.VertexWrites;var originalBones=b.Bones;
            var rest=originalBones.Select(x=>x.localToWorldMatrix).ToArray();
            foreach(float angle in new[]{-.8f,0,.6f,1.1f})
            {
                var pose=new Matrix4x4{Rows=NM.CreateTranslation(0,-1.4f,0)*NM.CreateRotationX(angle)*NM.CreateTranslation(0,1.4f,0)};
                for(int j=1;j<4;j++)originalBones[j].localToWorldMatrix=pose*rest[j];
                UpdateALBindings(maid);
                var c=b.Context;
                var virtualMatrix=b.ToReference*VirtualAxisMath.Evaluate(c.Axis,c.PelvisBind*MatrixBridge.ToManaged(c.Pelvis.localToWorldMatrix),c.SpineBind*MatrixBridge.ToManaged(c.Spine.localToWorldMatrix),Shape).Transform;
                for(int i=0;i<record.LastNewV.Length;i+=17)
                {
                    // Clothes can additionally sample the body's material field.
                    float alpha=Attachment(record,i,1);
                    if(record.BellyInfluence==null&&alpha>0)alpha=c.Material.Sample(GV.Transform(N(record.OrigVerts[i]),record.ToReference),alpha);
                    if(record.ThighGuardRestore!=null&&record.BellyInfluence==null)alpha*=1-record.ThighGuardRestore[i];
                    if(record.BreastExcluded[i])alpha=0;
                    GV expected=GV.Zero;
                    foreach(var w in Influences(b.Weights[i]))if(w.Weight>0)expected+=GV.Transform(N(record.LastNewV[i]),MatrixBridge.ToManaged(b.Binds[w.Bone])*MatrixBridge.ToManaged(b.Bones[w.Bone].localToWorldMatrix))*w.Weight*(1-alpha);
                    expected+=GV.Transform(N(record.LastNewV[i]),virtualMatrix)*alpha;
                    Check(GV.Distance(N(SkinPoint(renderer,record.LastNewV[i],i)),expected)<2e-5f,"curved body/clothing binding matches skin equation in motion");
                }
                Check(renderer.sharedMesh.VertexWrites==writes,"pose updates do not rewrite vertices");
            }
            for(int j=0;j<originalBones.Length;j++)originalBones[j].localToWorldMatrix=rest[j];
        }
        ReleaseALBindings(maid);
        var savedShape=Shape;
        Shape=new();var (arcMaid,arcBody,arcCloth)=LiveFixture();
        try
        {
            ApplyProgress(arcMaid,1);var oldContext=_alContexts[arcMaid.GetHashCode()];
            Shape=Shape.Copy();Shape.LowerBoundaryArc=.2f;ApplyProgress(arcMaid,1);
            Check(_alContexts[arcMaid.GetHashCode()]!=oldContext&&_lastRefreshStats.Rebuilt==2,"lower arc edit invalidates body and clothing binding cache");
            oldContext=_alContexts[arcMaid.GetHashCode()];
            Shape=Shape.Copy();Shape.UpperBoundaryArc=.24f;ApplyProgress(arcMaid,1);
            Check(_alContexts[arcMaid.GetHashCode()]!=oldContext&&_lastRefreshStats.Rebuilt==2,"upper arc edit invalidates body and clothing binding cache");
            var applied=GetAppliedShape(arcMaid);
            Check(applied.LowerBoundaryArc==.2f&&applied.UpperBoundaryArc==.24f,"maid retains independently applied arc values");
            ApplyProgress(arcMaid,1);
            Check(_lastRefreshStats.Reused==2&&_lastRefreshStats.Rebuilt==0,"unchanged arc settings reuse rebuilt meshes");
        }
        finally{ReleaseALBindings(arcMaid);Shape=savedShape;}
        object real=null;
        if(args.Length>2)
        {
            body=ReadModel(args[2],"curved-binding-real-body");maid=new Maid();
            var vertices=body.sharedMesh.vertices;var native=body.sharedMesh.boneWeights;
            PrepareALContext(maid,new(){body},1);var c=_alWorking;
            Check(c!=null,"real body calibrated");
            ApplySMR(maid,body,1,MeshMorphClass.Body,false);var record=FindRecord(maid,body);
            Check(_alBindings.ContainsKey(body),"real body curved binding installed");
            float lo=float.PositiveInfinity,hi=0;int active=0;
            for(int i=0;i<vertices.Length;i++)
            {
                float alpha=Attachment(record,i,1);
                Check(float.IsFinite(alpha)&&alpha>=0&&alpha<=1.00001f,"real body weights finite and bounded");
                if(alpha>0){active++;lo=Math.Min(lo,alpha);hi=Math.Max(hi,alpha);}
            }
            Check(active>0,"real body retains virtual attachment");
            real=new{vertices=vertices.Length,active,minimum=lo,maximum=hi,span=c.Frame.Profile.Span,arcHeight=c.Frame.Profile.Span*.10f};
            ReleaseALBindings(maid);
            Check(native.SequenceEqual(body.sharedMesh.boneWeights),"real body original weights restored");
        }
        var report=new{assertions=tests-startTests,arcSpanRatio=.10,realModel=real,inGameVisualTest=false};
        if(args.Length>1)File.WriteAllText(args[1],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(report));
    }
}
