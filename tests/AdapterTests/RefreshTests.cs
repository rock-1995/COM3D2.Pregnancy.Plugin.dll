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
        Shape=new();
        var (maid,body,cloth,bodyMorph,clothMorph)=NativeFixture();
        var originalBody=body.sharedMesh.vertices;
        var originalCloth=cloth.sharedMesh.vertices;
        var originalBodyNormals=body.sharedMesh.normals;
        var originalBodyTopology=(int[])bodyMorph.m_nSubMeshOriTri[0].Clone();
        var originalClothTopology=(int[])clothMorph.m_nSubMeshOriTri[0].Clone();
        cloth.enabled=false;
        ApplyNativeAppearance(maid,1);
        Check(_lastRefreshStats.Rebuilt==2,"initial native build includes hidden loaded clothing");
        var context=_alContexts[maid.GetHashCode()];
        var bodyBinding=ActiveBinding(body);
        var clothBinding=ActiveBinding(cloth);
        var expectedBody=body.sharedMesh.vertices;
        var expectedCloth=cloth.sharedMesh.vertices;
        var expectedBodyNormals=body.sharedMesh.normals;
        int bodyWrites=body.sharedMesh.VertexWrites,clothWrites=cloth.sharedMesh.VertexWrites;
        int normalWrites=body.sharedMesh.NormalWrites+cloth.sharedMesh.NormalWrites;
        ApplyNativeAppearance(maid,1);
        Check(_lastRefreshStats.ContextHits==1 && _lastRefreshStats.ContextBuilds==0,"repeat native build keeps context");
        Check(_lastRefreshStats.Rebuilt==0 && _lastRefreshStats.Reused==2,"repeat native build reuses both results");
        Check(body.sharedMesh.VertexWrites==bodyWrites && cloth.sharedMesh.VertexWrites==clothWrites,"cache hit does not upload vertices");

        // Render masks are temporary indices. They must not enter the shape,
        // contact, normal, connectivity or context signatures.
        var hiddenBody=HiddenFaces(originalBodyTopology,originalBody);
        var hiddenCloth=HiddenFaces(originalClothTopology,originalCloth);
        Check(!hiddenBody.SequenceEqual(originalBodyTopology),"body mask actually removes faces");
        Check(!hiddenCloth.SequenceEqual(originalClothTopology),"clothing mask actually removes faces");
        body.sharedMesh.triangles=hiddenBody;
        cloth.sharedMesh.triangles=hiddenCloth;
        cloth.enabled=true;
        ApplyNativeAppearance(maid,1);
        Check(_alContexts[maid.GetHashCode()]==context,"mask and clothing visibility keep the body context");
        Check(_lastRefreshStats.ContextHits==1 && _lastRefreshStats.ContextBuilds==0 && _lastRefreshStats.Rebuilt==0 && _lastRefreshStats.Reused==2,"mask change is a pure geometry-cache hit");
        Check(body.sharedMesh.VertexWrites==bodyWrites && cloth.sharedMesh.VertexWrites==clothWrites,"mask change does not upload vertices");
        Check(body.sharedMesh.NormalWrites+cloth.sharedMesh.NormalWrites==normalWrites,"mask change does not upload normals");
        Check(ActiveBinding(body)==bodyBinding && ActiveBinding(cloth)==clothBinding,"mask change keeps both palettes");
        Check(ReferenceEquals(body.sharedMesh.triangles,hiddenBody) && ReferenceEquals(cloth.sharedMesh.triangles,hiddenCloth),"shape application never restores hidden rendered faces");
        Check(NativeOriginalTopology(body).SequenceEqual(originalBodyTopology),"original topology remains available through native registration");
        Check(NativeOriginalSubmesh(cloth,0).SequenceEqual(originalClothTopology),"native submesh lookup ignores rendered mask");
        Same(originalBody,bodyMorph.m_vOriVert,"native original body array stays unchanged");
        Same(originalBodyNormals,bodyMorph.m_vOriNorm,"native original authored normals stay unchanged");
        var originalNeighbors=BuildMeshNeighbors(body.sharedMesh,originalBody.Length,NativeOriginalTopology(body));
        var visibleNeighbors=BuildMeshNeighbors(body.sharedMesh,originalBody.Length);
        int upperVertex=Array.FindIndex(originalBody,v=>v.y>2.5f);
        Check(originalNeighbors[upperVertex].Count>0 && visibleNeighbors[upperVertex].Count==0,"original-topology neighbor path retains masked-region connectivity");

        // The real integration writes the two private TMorph temporary buffers.
        // This shim merely exposes those buffers as the native restore does.
        body.sharedMesh.vertices=new Vector3[originalBody.Length];
        body.sharedMesh.normals=new Vector3[originalBody.Length];
        bodyMorph.RestoreTemporaryBuffers();
        Same(expectedBody,body.sharedMesh.vertices,"native temporary vertex buffer contains final shape");
        Same(expectedBodyNormals,body.sharedMesh.normals,"native temporary normal buffer contains final normals");
        Check(ReferenceEquals(body.sharedMesh.triangles,hiddenBody),"buffer restoration leaves face mask untouched");

        // Native boVisible is available before the GameObject activation at
        // the end of loading. Contributors keep the original visibility rule;
        // a later explicit build may change context when that rule changes.
        var (owners,primary,garment,primaryMorph,garmentMorph)=NativeFixture();
        primary.gameObject.activeInHierarchy=false;
        Check(NativeBodyVisible(primary),"native body flag takes precedence over delayed object activation");
        ApplyNativeAppearance(owners,.8f);
        var ownerContext=_alContexts[owners.GetHashCode()];
        var (_,secondary,_)=Fixture();
        secondary.sharedMesh.name="body_overlay";
        secondary.sharedMesh.vertices=secondary.sharedMesh.vertices.Select(v=>new Vector3(v.x*1.3f,v.y,v.z*1.4f)).ToArray();
        owners.Renderers.Add(secondary);
        var secondaryMorph=RegisterNativeMesh(owners,secondary);
        secondaryMorph.bodyskin.boVisible=false;
        Check(!NativeBodyVisible(secondary),"hidden body contributor remains excluded");
        ApplyNativeAppearance(owners,.8f);
        Check(_alContexts[owners.GetHashCode()]!=ownerContext,"body contributor inventory updates context signature");
        ownerContext=_alContexts[owners.GetHashCode()];
        Check(ownerContext.Surface.Meshes.Count==1,"hidden secondary body does not enter body contact surface");
        secondaryMorph.bodyskin.boVisible=true;
        ApplyNativeAppearance(owners,.8f);
        Check(_alContexts[owners.GetHashCode()]!=ownerContext,"explicit build after contributor visibility change recalibrates context");
        Check(_alContexts[owners.GetHashCode()].Surface.Meshes.Count==2,"visible secondary body enters contact surface");
        primary.enabled=false;
        Check(!NativeBodyVisible(primary),"native visibility still respects renderer enabled");
        var (_,unregistered,_)=Fixture();unregistered.gameObject.activeInHierarchy=false;
        Check(!NativeBodyVisible(unregistered),"unregistered renderers retain hierarchy fallback");

        // The first application while masked must equal a cold unmasked build.
        var (masked,mb,mc,mbm,mcm)=NativeFixture();
        var (full,fb,fc,fbm,fcm)=NativeFixture();
        var maskedBodyFaces=HiddenFaces(mbm.m_nSubMeshOriTri[0],mb.sharedMesh.vertices);
        var maskedClothFaces=HiddenFaces(mcm.m_nSubMeshOriTri[0],mc.sharedMesh.vertices);
        mb.sharedMesh.triangles=maskedBodyFaces;
        mc.sharedMesh.triangles=maskedClothFaces;
        ApplyNativeAppearance(masked,.73f);ApplyNativeAppearance(full,.73f);
        Same(fb.sharedMesh.vertices,mb.sharedMesh.vertices,"cold mask-independent body geometry");
        Same(fc.sharedMesh.vertices,mc.sharedMesh.vertices,"cold mask-independent clothing contact and repair");
        Same(fb.sharedMesh.normals,mb.sharedMesh.normals,"cold mask-independent body normals");
        Same(fc.sharedMesh.normals,mc.sharedMesh.normals,"cold mask-independent clothing normals");
        Check(ReferenceEquals(mb.sharedMesh.triangles,maskedBodyFaces) && ReferenceEquals(mc.sharedMesh.triangles,maskedClothFaces),"cold build preserves rendered masks");

        // Genuine original topology changes still invalidate exactly the
        // dependent meshes; this is different from temporary visibility masks.
        clothMorph.m_nSubMeshOriTri[0]=originalClothTopology.Skip(3).ToArray();
        ApplyNativeAppearance(maid,1);
        Check(_alContexts[maid.GetHashCode()]==context && _lastRefreshStats.ContextHits==1,"garment original topology does not invalidate body context");
        Check(_lastRefreshStats.Rebuilt==1 && _lastRefreshStats.Reused==1,"garment original topology rebuilds only that garment");
        bodyMorph.m_nSubMeshOriTri[0]=originalBodyTopology.Skip(3).ToArray();
        ApplyNativeAppearance(maid,1);
        Check(_alContexts[maid.GetHashCode()]!=context && _lastRefreshStats.ContextBuilds==1,"body original topology invalidates contact context");
        Check(_lastRefreshStats.Rebuilt==2,"body original topology rebuilds body and dependent clothing");
        Check(ReferenceEquals(body.sharedMesh.triangles,hiddenBody),"original topology invalidation still leaves rendered mask unchanged");

        // Stage/shape edits remain equivalent to a clean build and isolated.
        var originalOther=mb.sharedMesh.vertices;
        Shape.BellySag=.6f;Shape.GrowthFullness=.78f;
        CaptureShapeForMaid(full);ApplyNativeAppearance(full,.91f);
        var (cold,cb,cc,cbm,ccm)=NativeFixture();CaptureShapeForMaid(cold);ApplyNativeAppearance(cold,.91f);
        Same(cb.sharedMesh.vertices,fb.sharedMesh.vertices,"stage and shape edit equals cold body");
        Same(cc.sharedMesh.vertices,fc.sharedMesh.vertices,"stage and shape edit equals cold clothing");
        Same(originalOther,mb.sharedMesh.vertices,"one maid edit leaves another maid unchanged");
        ApplyNativeAppearance(maid,0);
        Same(originalBody,body.sharedMesh.vertices,"zero belly restores native body base");
        Same(originalCloth,cloth.sharedMesh.vertices,"zero belly restores native clothing base");
        body.sharedMesh.vertices=new Vector3[originalBody.Length];bodyMorph.RestoreTemporaryBuffers();
        Same(originalBody,body.sharedMesh.vertices,"zero belly also updates native restore buffer");

        File.WriteAllText(args[1],JsonSerializer.Serialize(new {
            assertions=tests,
            scope="Offline geometry/cache tests. NativeMorphIntegration, ALRefresh, ALGrowthAdapter, clothing/skirt/normal algorithms are linked production source; Unity/TMorph, renderer discovery, ApplySMR and lifecycle helpers use explicit test shims or historical slices. Does not validate game scheduling, loading time, graphics or UI.",
            cases=new[]{"hidden initial garment","unchanged reuse","body/clothing mask-independent context","native original submesh","original neighbor connectivity","native temporary buffers","native boVisible contributor semantics","cold masked/unmasked equivalence","genuine topology invalidation","stage/shape equivalence","maid isolation","zero reset"}
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {tests} linked-source native topology, geometry and cache assertions.");
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
