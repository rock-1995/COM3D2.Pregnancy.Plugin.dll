using System.Text.Json;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunBakeNormalStudy(string[] args)
    {
        var body=ReadModel(args[1],"body-bake-normal-study");var maid=new Maid();Shape=new VtxSettings();
        var rows=File.ReadAllLines(args[2]).Skip(1).Take(body.sharedMesh.vertexCount).Select(s=>s.Split('\t')).ToArray();
        float Number(string s)=>float.Parse(s,System.Globalization.CultureInfo.InvariantCulture);
        var original=rows.Select(a=>new Vector3(Number(a[2]),Number(a[3]),Number(a[4]))).ToArray();
        var final=rows.Select(a=>new Vector3(Number(a[5]),Number(a[6]),Number(a[7]))).ToArray();
        var authored=body.sharedMesh.normals;body.sharedMesh.vertices=original;
        PrepareALContext(maid,new(){body},1);var r=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=original,OrigNormals=authored};
        Check(TryDeformAL(body,r,MeshMorphClass.Body,1,out _,out _),"bake normal study calibration");
        float[] Flatten(Vector3[] values)=>values.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray();
        File.WriteAllText(args[3],JsonSerializer.Serialize(new{original=Flatten(original),deformed=Flatten(final),normals=Flatten(authored),fixedNormals=Flatten(DeformationNormals.Build(original,final,authored,body.sharedMesh.triangles,r.NormalRegions)),triangles=body.sharedMesh.triangles,skin=body.sharedMesh.GetTriangles(0),excluded=r.BreastExcluded,thigh=r.ThighGuardRestore,ownership=r.TorsoOwnership,
            local=Flatten(original.Select(v=>U(Growth.Numerics.Vector3.Transform(N(v),r.ToReference*r.GrowthContext.InverseFrame))).ToArray())}));
        Console.WriteLine("Exported actual shape and protected regions.");
    }
}
