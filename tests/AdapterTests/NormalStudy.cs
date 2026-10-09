using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunNormalStudy(string[] args)
    {
        TestNormalTransport();
        var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        var body=ReadModel(args[2],"body-real");var maid=new Maid();
        var original=body.sharedMesh.vertices;var normals=body.sharedMesh.normals;
        float[] Flatten(Vector3[] values)=>values.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray();
        Shape=new VtxSettings();
        foreach(float stage in new[]{0f,.44444445f,.739f,1f})
        {
            PrepareALContext(maid,new(){body},stage);
            var rec=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=original,OrigNormals=normals};
            Check(TryDeformAL(body,rec,MeshMorphClass.Body,stage,out var result,out _),"normal study deformation");
            var fixedNormals=DeformationNormals.Build(original,result,normals,body.sharedMesh.triangles);
            File.WriteAllText(Path.Combine(output,$"model-{stage:F3}.json"),JsonSerializer.Serialize(new{
                stage,original=Flatten(original),deformed=Flatten(result),normals=Flatten(normals),fixedNormals=Flatten(fixedNormals),triangles=body.sharedMesh.triangles,skin=body.sharedMesh.GetTriangles(0),
                local=Flatten(original.Select(v=>U(Growth.Numerics.Vector3.Transform(N(v),rec.ToReference*rec.GrowthContext.InverseFrame))).ToArray())
            }));
        }
        Console.WriteLine($"PASS: {tests} normal transport assertions; actual model, authored normals and four stages exported.");
    }
    static void TestNormalTransport()
    {
        var v=new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(0,1,0),new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(0,1,0)};
        var n=Enumerable.Repeat(Vector3.forward,v.Length).ToArray();var t=new[]{0,1,2,3,4,5};
        var same=DeformationNormals.Build(v,v,n,t);Check(same.SequenceEqual(n),"no deformation preserves every authored normal bit");
        var d=(Vector3[])v.Clone();d[4].z=.5f;
        var result=DeformationNormals.Build(v,d,n,t);
        Check((result[1]-result[3]).sqrMagnitude==0 && (result[2]-result[5]).sqrMagnitude==0,"split UV seam receives identical normals from both fans");
        var hard=(Vector3[])n.Clone();hard[3]=hard[4]=hard[5]=Vector3.up;
        var h=DeformationNormals.Build(v,d,hard,t);Check((h[1]-h[3]).magnitude>.5f,"authored hard edge is not welded");
        var thin=(Vector3[])d.Clone();thin[3].z+=.001f;thin[5].z+=.001f;
        var split=DeformationNormals.Build(v,thin,n,t);Check((split[1]-split[3]).magnitude>.01f,"separated surfaces are not welded");
        var degenerates=DeformationNormals.Build(v,d,n,new[]{0,0,0,-1,0,1,100,0,1});Check(degenerates.SequenceEqual(n),"degenerate and invalid triangles preserve fallback normals");
        foreach(float scale in new[]{.01f,1f,100f})
        {
            var r=System.Numerics.Matrix4x4.CreateRotationY(.7f)*System.Numerics.Matrix4x4.CreateRotationX(-.5f);
            Vector3 Transform(Vector3 p)=>(Vector3)System.Numerics.Vector3.Transform(p,r);
            var a=v.Select(p=>Transform(p*scale)+new Vector3(.3f,-.5f,.7f)*scale).ToArray();
            var b=d.Select(p=>Transform(p*scale)+new Vector3(.3f,-.5f,.7f)*scale).ToArray();
            var ns=n.Select(Transform).ToArray();var actual=DeformationNormals.Build(a,b,ns,t);
            for(int i=0;i<actual.Length;i++)Check(Vector3.Distance(actual[i],Transform(result[i]))<1e-5f,"normal rotation and unit-scale covariance");
        }
        var flipped=v.Select(p=>new Vector3(-p.x,p.y,-p.z)).ToArray();var f=DeformationNormals.Build(v,flipped,n,t);
        Check(f.All(x=>Vector3.Distance(x,-Vector3.forward)<1e-6f),"180 degree normal rotation is finite and correct");
        Check(DeformationNormals.Build(v,d,n,t).SequenceEqual(result),"repeat computation does not accumulate normal changes");
    }
}
