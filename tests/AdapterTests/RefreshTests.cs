using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static (Maid m,SkinnedMeshRenderer b,SkinnedMeshRenderer c) LiveFixture()
    {
        var(m,b,c)=Fixture();m.body0=new TBody{maid=m};m.Renderers.AddRange(new[]{b,c});return(m,b,c);
    }
    static void Same(Vector3[] a,Vector3[] b,string label,float tolerance=2e-6f)
    {Check(a.Length==b.Length,label+" length");for(int i=0;i<a.Length;i++)Check(Vector3.Distance(a[i],b[i])<=tolerance,label+" vertex "+i);}
    static Vector3[] Rendered(SkinnedMeshRenderer r)=>r.sharedMesh.vertices.Select((v,i)=>SkinPoint(r,v,i)).ToArray();
    static void Tick(Maid m,int times)
    {var mon=EnsureMonitor(m);for(int i=0;i<times;i++)typeof(BellyMonitor).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mon,null);}
    static void RunRefreshTests(string[] args)
    {
        Shape=new();var (a,ab,ac)=LiveFixture();var originals=ab.sharedMesh.vertices;
        ApplyProgress(a,1);var context=_alContexts[a.GetHashCode()];var binding=ActiveBinding(ab);var vertices=ab.sharedMesh.vertices;
        int writes=ab.sharedMesh.VertexWrites;
        ApplyProgress(a,1);
        Check(_lastRefreshStats.Reused==2 && _lastRefreshStats.Rebuilt==0,"repeat apply reuses every mesh");
        Check(_lastRefreshStats.ContextHits==1 && _lastRefreshStats.ContextBuilds==0,"repeat context hit");
        Check(ab.sharedMesh.VertexWrites==writes && ActiveBinding(ab)==binding,"repeat avoids upload and palette rebuild");

        // Parameter editing and applying to another maid cannot change A's pose.
        var (b,bb,bc)=LiveFixture();var ui=new PregnancyUI();ui._maids.AddRange(new[]{a,b});ui.Select(0);ui.Select(1);
        Check(GetActiveProgress(a)==1 && ActiveBinding(ab)==binding,"UI selection keeps prior maid active");Same(vertices,ab.sharedMesh.vertices,"UI selection retains belly");
        var pose=new Matrix4x4{Rows=System.Numerics.Matrix4x4.CreateRotationX(.45f)};
        ab.bones[1].localToWorldMatrix=pose*ab.bones[1].localToWorldMatrix;UpdateALBindings(a);var renderedA=Rendered(ab);
        Shape=new VtxSettings{BellySag=.2f,GrowthFullness=.4f,AxisPullLow=.05f,AxisPullHigh=.1f,VirtualAxisStrength=.25f};
        PregnancyUI.TriggerApplyBelly(b,.67f);UpdateALBindings(a);
        Same(renderedA,Rendered(ab),"B apply does not alter A pose");Same(vertices,ab.sharedMesh.vertices,"B apply keeps A geometry");
        Check(_alContexts[a.GetHashCode()]==context,"separate maid contexts");
        var bSettings=Shape.Copy();ui.Select(0);Check(ui._shape.BellySag==2 && ui._shape.VirtualAxisStrength==context.Settings.VirtualAxisStrength,"selection loads A settings");
        ui.Select(1);Check(ui._shape.BellySag==bSettings.BellySag,"selection loads B settings");

        // Actual debounced monitor callbacks, with the editor still holding B.
        ac.enabled=false;EnsureMonitor(a).RequestVisibilityApply(a.GetHashCode());Tick(a,2);
        Check(ab.sharedMesh.VertexWrites==writes,"visibility waits for stable frames");Tick(a,1);
        Check(_lastRefreshStats.Rebuilt==0 && _lastRefreshStats.Reused==1,"hidden clothing skipped");
        Check(_alContexts[a.GetHashCode()]==context && ActiveBinding(ab)==binding,"clothing visibility keeps body cache");
        Check(Shape.BellySag==bSettings.BellySag,"automatic refresh restores editor settings");
        ac.enabled=true;EnsureMonitor(a).RequestVisibilityApply(a.GetHashCode());Tick(a,3);
        Check(_lastRefreshStats.Reused==2 && _lastRefreshStats.Rebuilt==0,"show reuses clothing");
        EnsureMonitor(a).TriggerFullRefresh();Tick(a,3);Check(_lastRefreshStats.Reused==2,"full refresh keeps unchanged results");

        var (_,_,newCloth)=Fixture();a.Renderers.Add(newCloth);RefreshProgress(a,1);
        Check(_lastRefreshStats.ContextHits==1 && _lastRefreshStats.Reused==2 && _lastRefreshStats.Rebuilt==1,"new garment rebuilds only itself");
        Check(_alContexts[a.GetHashCode()]==context,"new clothing keeps body context");
        a.Renderers.Remove(newCloth);RefreshProgress(a,1);Check(!context.Maps.ContainsKey(newCloth),"removed garment map pruned");
        Check(ActiveBinding(newCloth)==null,"removed garment binding released");
        a.Renderers.Add(newCloth);RefreshProgress(a,1);
        Check(_lastRefreshStats.Rebuilt==1 && _lastRefreshStats.Reused==2,"reattach rebuilds only released garment");
        Same(newCloth.sharedMesh.vertices,ac.sharedMesh.vertices,"reattach has no accumulated deformation");
        a.Renderers.Remove(newCloth);RefreshProgress(a,1);
        // Changing topology invalidates only this garment, even with identical positions.
        ac.sharedMesh.triangles=ac.sharedMesh.triangles.Skip(3).ToArray();RefreshProgress(a,1);
        Check(_lastRefreshStats.ContextHits==1 && _lastRefreshStats.Rebuilt==1 && _lastRefreshStats.Reused==1,"clothing topology invalidates clothing");
        var cleanBody=(Vector3[])FindRecord(a,ab).OrigVerts.Clone();cleanBody[17].z+=.00001f;ab.sharedMesh.vertices=cleanBody;RefreshProgress(a,1);
        Check(_lastRefreshStats.ContextBuilds==1 && _lastRefreshStats.Rebuilt==2,"small unsampled body edit invalidates body and clothes");
        context=_alContexts[a.GetHashCode()];ab.sharedMesh.triangles=ab.sharedMesh.triangles.Skip(3).ToArray();RefreshProgress(a,1);
        Check(_alContexts[a.GetHashCode()]!=context,"body topology invalidates context");

        // Parameter/progress updates must equal a clean application, with no accumulation.
        var(c,cb,cc)=LiveFixture();var(d,db,dc)=LiveFixture();
        Shape=new();ApplyProgress(c,1);Shape.BellySag=.6f;Shape.GrowthFullness=.78f;ApplyProgress(c,.72f);ApplyProgress(d,.72f);
        Same(cb.sharedMesh.vertices,db.sharedMesh.vertices,"changed settings equal cold body");Same(cc.sharedMesh.vertices,dc.sharedMesh.vertices,"changed settings equal cold clothing");
        Shape.AxisPullHigh=.11f;ApplyProgress(c,.93f);Reset(d);ApplyProgress(d,.93f);
        Same(cb.sharedMesh.vertices,db.sharedMesh.vertices,"later stage equal cold body");Same(cc.sharedMesh.vertices,dc.sharedMesh.vertices,"later stage equal cold clothing");
        ApplyProgress(c,0);Same(cb.sharedMesh.vertices,originals,"zero progress restores base");
        Check(GetActiveProgress(c)==0 && !_maidShapes.ContainsKey(c.GetHashCode()),"zero clears own saved state");
        var aBeforeReset=ab.sharedMesh.vertices;Reset(b);Same(aBeforeReset,ab.sharedMesh.vertices,"reset B preserves A");Check(ActiveBinding(ab)!=null,"reset B preserves A binding");

        // TMorph produces the same geometry, and its final Apply can reuse the bake.
        Shape=new();var(e,eb,ec)=LiveFixture();var morph=new TMorph{m_vOriVert=eb.sharedMesh.vertices,m_vOriNorm=eb.sharedMesh.normals};morph.SetRenderer(eb);
        Check(TryBakeRuntimeMorphBase(e,morph,1),"engine morph bake succeeds");var baked=eb.sharedMesh.vertices;var bakedBinding=ActiveBinding(eb);
        Check(_maidShapes.ContainsKey(e.GetHashCode()),"first engine bake latches target settings");ApplyProgress(e,1);
        Same(baked,eb.sharedMesh.vertices,"post-bake apply identity");Check(ActiveBinding(eb)==bakedBinding && _lastRefreshStats.Reused==1,"post-bake body reused");
        Check(!TryBakeRuntimeMorphBase(e,morph,1),"identical baked morph skipped");Reset(e);Same(eb.sharedMesh.vertices,originals,"baked reset clean");
        var(f,fb,fc)=LiveFixture();var extra=new Vector3[fb.sharedMesh.vertexCount];extra[17]=new Vector3(0,0,.0001f);
        var morphExtra=new TMorph{m_vOriVert=fb.sharedMesh.vertices,m_vOriNorm=fb.sharedMesh.normals,ExtraDelta=extra};morphExtra.SetRenderer(fb);
        Check(TryBakeRuntimeMorphBase(f,morphExtra,1),"engine bake with external morph");ApplyProgress(f,1);
        Check(_lastRefreshStats.Rebuilt==2,"native extra morph cannot reuse stale bake");
        Check(Vector3.Distance(FindRecord(f,fb).OrigVerts[17],originals[17]+extra[17])<2e-6f,"native extra morph preserved");
        var(innerM,innerB,innerC)=LiveFixture();PrepareALContext(innerM,innerM.Renderers,1);
        using(new RefreshTrace(innerM,"inner-cache-test"))ApplySMR(innerM,innerC,1,MeshMorphClass.InnerCloth,false);
        Check(_lastRefreshStats.RestQueries>0 && _lastRefreshStats.RestHits>0,"inner shape and motion share rest-surface hits");
        File.WriteAllText(args[1],JsonSerializer.Serialize(new{assertions=tests,scope="Production lifecycle + selection slices; Unity scene/engine shims",cases=new[]{"repeat apply","two maid selection/apply/pose/reset isolation","visibility/full-refresh debounce","new/hidden/removed garments","body/clothing topology","small native edit","settings/stage cold equivalence","zero reset","TMorph final reuse and extra native delta"}},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {tests} refresh and multi-maid assertions.");
    }

    static void RunRefreshBenchmark(string[] args)
    {
        var body=ReadModel(args[2],"body");var cloth=ReadModel(args[3],Path.GetFileNameWithoutExtension(args[3]));var maid=new Maid();maid.body0=new TBody{maid=maid};maid.Renderers.AddRange(new[]{body,cloth});
        Shape=new();var cold=new List<double>();var warm=new List<double>();var toggle=new List<double>();
        for(int run=0;run<6;run++)
        {
            Reset(maid);var timer=Stopwatch.StartNew();ApplyProgress(maid,1);timer.Stop();if(run>0)cold.Add(timer.Elapsed.TotalMilliseconds);
            timer.Restart();ApplyProgress(maid,1);timer.Stop();if(run>0)warm.Add(timer.Elapsed.TotalMilliseconds);
            cloth.enabled=false;RefreshProgress(maid,1);cloth.enabled=true;
            timer.Restart();RefreshProgress(maid,1);timer.Stop();if(run>0)toggle.Add(timer.Elapsed.TotalMilliseconds);
            Check(_lastRefreshStats.Rebuilt==0 && _lastRefreshStats.Reused==2,"benchmark repeat has no rebuild");
        }
        var report=new{environment="Offline .NET 10 + Unity shims; does not measure game loading or FPS",bodyVertices=body.sharedMesh.vertexCount,clothingVertices=cloth.sharedMesh.vertexCount,runs=5,coldResetApplyMs=cold,unchangedApplyMs=warm,showExistingClothingMs=toggle,meanCold=cold.Average(),meanWarm=warm.Average(),meanShow=toggle.Average()};
        File.WriteAllText(args[1],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report));
    }
}
