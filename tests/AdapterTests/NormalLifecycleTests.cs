using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using System.Globalization;
using System.Text.Json;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static string[] NormalReplayInputs(string directory)
    {
        var files=Directory.GetFiles(directory,"*.csv").Select(p=>Path.GetFileName(p)).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        if(files.Length==0)throw new ArgumentException("Supply a directory containing matching ALGrowth body CSV exports.");
        return files;
    }
    static void RunNormalModelLifecycleTests(string[] args)
    {
        var reports=new List<object>();
        var inputs=NormalReplayInputs(args[2]);
        foreach(string input in inputs)
        foreach(float stage in new[]{.44444445f,.739f,1f})
        {
            Shape=new VtxSettings();var body=ReadModel(args[1],"body-normal-runtime");
            var rows=File.ReadAllLines(Path.Combine(args[2],input)).Skip(1).Where(s=>!string.IsNullOrWhiteSpace(s)).Select(s=>s.Split('\t')).ToArray();
            int end=Array.FindIndex(rows,1,a=>a[1]=="0");if(end<0)end=rows.Length;
            var original=rows.Take(end).Select(a=>new Vector3(float.Parse(a[2],CultureInfo.InvariantCulture),float.Parse(a[3],CultureInfo.InvariantCulture),float.Parse(a[4],CultureInfo.InvariantCulture))).ToArray();
            Check(original.Length==body.sharedMesh.vertexCount,"model lifecycle dump matches topology");
            var normals=body.sharedMesh.normals;var triangles=body.sharedMesh.triangles;
            body.sharedMesh.vertices=original;
            var maid=new Maid{Progress=stage};maid.body0=new TBody{maid=maid};maid.Renderers.Add(body);
            var nativeV=new Vector3[original.Length];var nativeN=new Vector3[original.Length];
            var morph=new TMorph{m_vOriVert=original,m_vOriNorm=normals,ExtraDelta=nativeV,ExtraNormalDelta=nativeN,m_nSubMeshOriTri=new[]{triangles}};
            morph.SetRenderer(body);var skin=new TBodySkin{morph=morph};morph.bodyskin=skin;maid.body0.goSlot.Add(skin);
            Check(TryBakeRuntimeMorphBase(maid,morph,stage),"actual model normal base bake");
            var baked=body.sharedMesh.vertices;
            int index=Enumerable.Range(0,original.Length).OrderByDescending(i=>(baked[i]-original[i]).sqrMagnitude).First();
            nativeV[index]=new Vector3(.003f,-.002f,.001f);nativeN[index]=new Vector3(.01f,.02f,.03f);
            morph.FixBlendValues();var current=body.sharedMesh.vertices;int writes=body.sharedMesh.VertexWrites;
            TryBakeRuntimeMorphBase(maid,morph,stage);
            Check(body.sharedMesh.VertexWrites==writes,"actual model normal refresh leaves vertices untouched");
            var clean=original.Select((v,i)=>v+nativeV[i]).ToArray();var cleanN=normals.Select((n,i)=>n+nativeN[i]).ToArray();
            var expected=DeformationNormals.Build(clean,current,cleanN,triangles,_morphBaseBakeStates[morph].NormalRegions);
            float firstError=body.sharedMesh.normals.Select((n,i)=>Vector3.Distance(n,expected[i])).Max();
            Console.WriteLine($"Model {input} stage {stage:R}: normal error {firstError:R}, clean vertex error {CleanVertices(maid,body).Select((v,i)=>Vector3.Distance(v,clean[i])).Max():R}, clean normal error {_morphBaseBakeStates[morph].NormalCache.CleanNormals.Select((n,i)=>Vector3.Distance(n,cleanN[i])).Max():R}");
            // Removing the float32 baked vertex delta can cross the existing
            // normal transport's 1e-7 dot-product no-rotation cutoff (~0.026 deg).
            // Bound that numerical effect to 0.057 deg, far below the old error.
            Check(firstError<1e-3f,"actual model final native morph normals");
            ApplyProgress(maid,stage);current=body.sharedMesh.vertices;
            expected=DeformationNormals.Build(clean,current,cleanN,triangles,FindRecord(maid,body).NormalRegions);
            float applyError=body.sharedMesh.normals.Select((n,i)=>Vector3.Distance(n,expected[i])).Max();
            Check(applyError<1e-3f,"actual model no second pregnancy normal rotation");
            for(int i=0;i<4;i++)ApplyProgress(maid,stage);
            Check(body.sharedMesh.normals.Select((n,i)=>Vector3.Distance(n,expected[i])).Max()<1e-3f,"actual model repeated Apply stable");
            reports.Add(new{input,stage,firstError,applyError,normalRefreshVertexWrites=0});
            Reset(maid);
            Same(body.sharedMesh.normals,cleanN,"actual model restore native shading",2e-5f);
        }
        File.WriteAllText(args[3],JsonSerializer.Serialize(new{assertions=tests,inGameVisualTest=false,scope="Production TMorph bake and normal refresh with native morph stubs; local model topology/authored normals plus supplied original vertex dumps.",cases=reports},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {reports.Count} actual model/stage lifecycle cases; {tests} assertions.");
    }
    static void RunNormalLifecycleTests()
    {
        Shape=new VtxSettings();
        var(maid,body,cloth)=LiveFixture();
        var original=body.sharedMesh.vertices;
        var normals=original.Select(v=>new Vector3(v.x/1.35f,0,(v.z+.15f)/.85f).normalized).ToArray();
        body.sharedMesh.normals=normals;
        var extra=new Vector3[original.Length];extra[17]=new Vector3(0,0,.0001f);
        var morph=new TMorph{m_vOriVert=original,m_vOriNorm=normals,ExtraDelta=extra};morph.SetRenderer(body);
        Check(TryBakeRuntimeMorphBase(maid,morph,1),"normal lifecycle bake succeeds");
        ApplyProgress(maid,1);
        var rec=FindRecord(maid,body);
        var expected=DeformationNormals.Build(rec.OrigVerts,body.sharedMesh.vertices,normals,body.sharedMesh.triangles);
        float error=body.sharedMesh.normals.Select((n,i)=>Vector3.Distance(n,expected[i])).Max();
        Console.WriteLine($"Post-TMorph normal error: {error:R}");
        Check(error<2e-5f,"post-TMorph Apply rotates pregnancy normals exactly once");

        var pregnantVertices=body.sharedMesh.vertices;
        int affectedIndex=Enumerable.Range(0,original.Length).OrderByDescending(i=>(pregnantVertices[i]-original[i]).sqrMagnitude).First();
        var nativeNormals=new Vector3[original.Length];nativeNormals[affectedIndex]=new Vector3(.03f,.02f,.01f);
        morph.ExtraNormalDelta=nativeNormals;
        extra[affectedIndex]=new Vector3(.02f,.01f,.04f);
        morph.FixBlendValues();
        var rawVertices=body.sharedMesh.vertices;int vertexWrites=body.sharedMesh.VertexWrites;
        Check(!TryBakeRuntimeMorphBase(maid,morph,1),"native slider change keeps pregnancy base bake");
        var clean=original.Select((v,i)=>v+extra[i]).ToArray();
        var cleanNormals=normals.Select((n,i)=>n+nativeNormals[i]).ToArray();
        expected=DeformationNormals.Build(clean,rawVertices,cleanNormals,body.sharedMesh.triangles);
        var unrefreshed=morph.m_vOriNorm.Select((n,i)=>n+nativeNormals[i]).ToArray();
        float skippedError=unrefreshed.Select((n,i)=>Vector3.Distance(n,expected[i])).Max();
        Console.WriteLine($"Skipping final native-morph refresh would leave normal error: {skippedError:R}");
        Check(skippedError>1e-3f,"native morph regression meaningfully distinguishes stale shading");
        Same(body.sharedMesh.normals,expected,"changed native morph normals match final shape",2e-5f);
        Check(body.sharedMesh.VertexWrites==vertexWrites,"post-morph normal refresh does not upload vertices");
        var cache=_morphBaseBakeStates[morph].NormalCache;int normalWrites=body.sharedMesh.NormalWrites;
        for(int i=0;i<12;i++)TryBakeRuntimeMorphBase(maid,morph,1);
        Check(_morphBaseBakeStates[morph].NormalCache==cache && body.sharedMesh.NormalWrites==normalWrites,"unchanged callbacks reuse normal result without writes");
        morph.FixBlendValues();TryBakeRuntimeMorphBase(maid,morph,1);
        Check(_morphBaseBakeStates[morph].NormalCache==cache,"identical native reupload reuses normal result");
        Same(body.sharedMesh.normals,expected,"identical native reupload restores corrected normals",2e-5f);

        nativeNormals[affectedIndex]+=new Vector3(.02f,.04f,.01f);
        morph.FixBlendValues();TryBakeRuntimeMorphBase(maid,morph,1);
        cleanNormals=normals.Select((n,i)=>n+nativeNormals[i]).ToArray();
        expected=DeformationNormals.Build(clean,rawVertices,cleanNormals,body.sharedMesh.triangles);
        Same(body.sharedMesh.normals,expected,"normal-only native morph survives correction",2e-5f);
        ApplyProgress(maid,1);
        expected=DeformationNormals.Build(clean,body.sharedMesh.vertices,cleanNormals,body.sharedMesh.triangles);
        Same(body.sharedMesh.normals,expected,"Apply after changed native morph does not double-correct",2e-5f);
        var applied=body.sharedMesh.normals;
        for(int i=0;i<8;i++)ApplyProgress(maid,1);
        Same(body.sharedMesh.normals,applied,"repeat Apply keeps shading",0);
        Reset(maid);
        Same(body.sharedMesh.normals,cleanNormals,"reset restores native morph normals",2e-5f);

        // Hiding alternate faces must not remove them from the normal stencil.
        Shape=new VtxSettings();var(hm,hb,hc)=LiveFixture();
        var hv=hb.sharedMesh.vertices;var hn=hv.Select(v=>new Vector3(v.x/1.35f,0,(v.z+.15f)/.85f).normalized).ToArray();
        var full=(int[])hb.sharedMesh.triangles.Clone();var hidden=(int[])full.Clone();
        for(int i=0;i+2<hidden.Length;i+=6)hidden[i]=hidden[i+1]=hidden[i+2]=0;
        hb.sharedMesh.triangles=hidden;hb.sharedMesh.normals=hn;
        var ht=new TMorph{m_vOriVert=hv,m_vOriNorm=hn,m_nSubMeshOriTri=new[]{full}};ht.SetRenderer(hb);
        var skin=new TBodySkin{morph=ht};ht.bodyskin=skin;hm.body0.goSlot.Add(skin);
        TryBakeRuntimeMorphBase(hm,ht,1);
        expected=DeformationNormals.Build(hv,hb.sharedMesh.vertices,hn,full);
        var hiddenOnly=DeformationNormals.Build(hv,hb.sharedMesh.vertices,hn,hidden);
        float hiddenError=hiddenOnly.Select((n,i)=>Vector3.Distance(n,expected[i])).Max();
        Console.WriteLine($"Using hidden render triangles would leave normal error: {hiddenError:R}");
        Check(hiddenError>1e-3f,"hidden topology regression covers a visible normal difference");
        Same(hb.sharedMesh.normals,expected,"hidden neighbor faces remain in normal calculation",2e-5f);
        Check(hb.sharedMesh.triangles.SequenceEqual(hidden),"normal repair keeps hidden render faces hidden");
        hb.sharedMesh.triangles=full;ApplyProgress(hm,1);
        Same(hb.sharedMesh.normals,expected,"showing skin keeps normal stencil stable",2e-5f);
        Console.WriteLine($"PASS: {tests} normal lifecycle assertions.");
    }
}
