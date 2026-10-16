using System.Diagnostics;
using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NM=System.Numerics.Matrix4x4;
using NV=System.Numerics.Vector3;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void TestProtectedNormalBoundary()
    {
        // Two individually flat patches connected by a strip. Translating the
        // growing patch past the protected one folds only the connecting strip:
        // neither patch's own lighting should tilt toward that hidden fold.
        var a=new[]{new Vector3(-2,-1,0),new Vector3(-1,-1,0),new Vector3(-2,1,0),new Vector3(-1,1,0),new Vector3(0,-1,0),new Vector3(1,-1,0),new Vector3(0,1,0),new Vector3(1,1,0)};
        var t=new[]{0,1,2,2,1,3,1,4,3,3,4,6,4,5,6,6,5,7};var regions=new[]{0,0,0,0,1,1,1,1};
        var normals=Enumerable.Repeat(Vector3.forward,a.Length).ToArray();
        var d=a.Select((p,i)=>i<4?p+new Vector3(2.2f,0,.3f):p).ToArray();
        var old=DeformationNormals.Build(a,d,normals,t);
        Check(old.Where((n,i)=>Vector3.Distance(n,normals[i])>.3f).Any(),"regression detects fold-biased normal fan");
        Same(DeformationNormals.Build(a,d,normals,t,regions),normals,"parallel protected panels keep their authored lighting",0);
        Same(DeformationNormals.Build(a,a,normals,t,regions),normals,"identity preserves original smooth boundary",0);
        Same(DeformationNormals.Build(a,d,normals,t,new int[a.Length]),old,"no region boundary preserves prior transport",0);
        foreach(float angle in new[]{-.6f,.4f,1.2f})
        {
            var rotation=NM.CreateRotationY(angle);var expected=normals.Select((n,i)=>i<4?(Vector3)NV.TransformNormal(n,rotation):n).ToArray();
            d=a.Select((p,i)=>i<4?(Vector3)NV.Transform(p,rotation)+new Vector3(2.2f,0,.3f):p).ToArray();
            Same(DeformationNormals.Build(a,d,normals,t,regions),expected,"growing side retains its actual surface tilt",2e-6f);
        }
        // Vertex 3 belongs to the body but has only a mixed connecting triangle.
        // It must inherit the body's ROTATION while preserving its own authored
        // direction, rather than keeping an uncorrected normal or copying a donor.
        a=new[]{Vector3.zero,Vector3.right,Vector3.up,new Vector3(2,0,0),new Vector3(2,1,0)};
        t=new[]{0,1,2,1,3,4};regions=new[]{0,0,0,0,2};
        normals=Enumerable.Repeat(Vector3.forward,a.Length).ToArray();normals[3]=new Vector3(.3f,0,1).normalized;
        var turn=NM.CreateRotationY(.7f);d=a.Select((p,i)=>i<4?(Vector3)NV.Transform(p,turn):p).ToArray();
        var complete=DeformationNormals.Build(a,d,normals,t,regions);
        Check(Vector3.Distance(complete[3],NV.TransformNormal(normals[3],turn))<2e-6f,"thin boundary receives same-side transport");
        Check(Vector3.Distance(complete[3],complete[1])>.1f,"thin boundary retains authored normal detail");
        // Duplicate UV corner receives the same completed normal on both pieces.
        var seamA=a.Concat(new[]{a[3]}).ToArray();var seamD=d.Concat(new[]{d[3]}).ToArray();var seamN=normals.Concat(new[]{normals[3]}).ToArray();
        var seam=DeformationNormals.Build(seamA,seamD,seamN,new[]{0,1,2,1,3,4,1,5,4},regions.Concat(new[]{0}).ToArray());
        Check(seam[3].Equals(seam[5]),"thin boundary weld is seam consistent");
        foreach(float scale in new[]{.01f,1f,100f})
        {
            var rotate=NM.CreateRotationX(.4f)*NM.CreateRotationZ(.5f);
            Vector3 Point(Vector3 v)=>(Vector3)NV.Transform(v*scale,rotate)+new Vector3(.5f,-.3f,.2f)*scale;
            Vector3 Normal(Vector3 n)=>NV.TransformNormal(n,rotate);
            var transformed=DeformationNormals.Build(a.Select(Point).ToArray(),d.Select(Point).ToArray(),normals.Select(Normal).ToArray(),t,regions);
            Same(transformed,complete.Select(Normal).ToArray(),"region normal units and rotation covariance",3e-5f);
        }
        Console.WriteLine("Protected boundary and thin-fan regressions passed.");
    }
    static void RunBakeNormalTests(string[] args)
    {
        TestNormalTransport();TestProtectedNormalBoundary();
        var reports=new List<object>();
        foreach(string input in NormalReplayInputs(args[2]))
        {
            var body=ReadModel(args[1],"body-bake-region-test");var maid=new Maid();maid.body0=new TBody{maid=maid};maid.Renderers.Add(body);Shape=new VtxSettings();
            var rows=File.ReadAllLines(Path.Combine(args[2],input)).Skip(1).Take(body.sharedMesh.vertexCount).Select(s=>s.Split('\t')).ToArray();
            float Number(string s)=>float.Parse(s,System.Globalization.CultureInfo.InvariantCulture);
            var original=rows.Select(a=>new Vector3(Number(a[2]),Number(a[3]),Number(a[4]))).ToArray();
            var dumped=rows.Select(a=>new Vector3(Number(a[5]),Number(a[6]),Number(a[7]))).ToArray();
            var authored=body.sharedMesh.normals;body.sharedMesh.vertices=original;var topology=(int[])body.sharedMesh.triangles.Clone();
            foreach(float stage in new[]{.44444445f,.739f,1f})
            {
                PrepareALContext(maid,new(){body},stage);
                var r=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=original,OrigNormals=authored};
                Check(TryDeformAL(body,r,MeshMorphClass.Body,stage,out var result,out _),"actual region test deform");
                foreach(var final in stage==1?new[]{result,dumped}:new[]{result})
                {
                    var old=DeformationNormals.Build(original,final,authored,topology);
                    var fixedN=DeformationNormals.Build(original,final,authored,topology,r.NormalRegions);
                    float oldChest=0,newChest=0;int changed=0;
                    for(int i=0;i<original.Length;i++)
                    {
                        Check(float.IsFinite(fixedN[i].x+fixedN[i].y+fixedN[i].z),"finite region normal");
                        if(r.BreastExcluded[i]){oldChest=Math.Max(oldChest,Vector3.Distance(old[i],authored[i]));newChest=Math.Max(newChest,Vector3.Distance(fixedN[i],authored[i]));}
                        if(Vector3.Distance(old[i],fixedN[i])>1e-5f)changed++;
                    }
                    int movingBreast=Enumerable.Range(0,original.Length).Count(i=>r.BreastExcluded[i]&&Vector3.Distance(original[i],final[i])>1e-7f);
                    if(movingBreast==0)Check(newChest<1e-5f,"stationary breast retains authored normals");
                    Check(changed<original.Length/5,"normal change remains local to protection boundary");
                    reports.Add(new{input,stage,dump=ReferenceEquals(final,dumped),changed,movingBreast,oldChest,newChest});
                }
            }
            ApplyProgress(maid,1);var record=FindRecord(maid,body);var geometry=body.sharedMesh.vertices;var bakedNormals=body.sharedMesh.normals;
            int normalWrites=body.sharedMesh.NormalWrites,vertexWrites=body.sharedMesh.VertexWrites;
            for(int i=0;i<30;i++){body.bones[1].localToWorldMatrix=Translation(0,.001f,0)*body.bones[1].localToWorldMatrix;Tick(maid,1);}
            Check(body.sharedMesh.NormalWrites==normalWrites,"pose updates do not recompute or upload baked normals");
            Check(body.sharedMesh.VertexWrites==vertexWrites,"pose updates do not rewrite body geometry");
            Same(body.sharedMesh.normals,bakedNormals,"baked normals stable through moving poses",0);
            Check(body.sharedMesh.triangles.SequenceEqual(topology),"normal regions never change rendered faces");
            var oldTimes=new List<double>();var newTimes=new List<double>();
            for(int j=0;j<30;j++)
            {
                var sw=Stopwatch.StartNew();DeformationNormals.Build(original,geometry,authored,topology);sw.Stop();if(j>=10)oldTimes.Add(sw.Elapsed.TotalMilliseconds);
                sw.Restart();DeformationNormals.Build(original,geometry,authored,topology,record.NormalRegions);sw.Stop();if(j>=10)newTimes.Add(sw.Elapsed.TotalMilliseconds);
            }
            oldTimes.Sort();newTimes.Sort();reports.Add(new{input,bakeMedianBeforeMs=oldTimes[oldTimes.Count/2],bakeMedianAfterMs=newTimes[newTimes.Count/2],poseNormalWrites=0});
            Reset(maid);
        }
        File.WriteAllText(args[3],JsonSerializer.Serialize(new{assertions=tests,inGameVisualTest=false,cases=reports},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {tests} bake-only normal assertions.");
    }
}
