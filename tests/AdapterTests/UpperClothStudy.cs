using System.Globalization;
using System.Text.Json;
using UnityEngine;
using NV=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
using COM3D2.Pregnancy.Plugin.Growth;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void RunUpperClothStudy(string[] args)
    {
        var body=ReadModel(args[2],"body");var garment=ReadModel(args[3],"dress284_wear");var maid=new Maid();
        if(args.Length>5)body.sharedMesh.vertices=File.ReadAllLines(args[5]).Select(line=>line.Split(',').Select(x=>float.Parse(x,CultureInfo.InvariantCulture)).ToArray()).Select(p=>new Vector3(p[0],p[1],p[2])).ToArray();
        if(args.Length>6)
        {
            if(args[6]=="masked-fixed")
            {
                var morph=new TMorph{m_vOriVert=body.sharedMesh.vertices,m_nSubMeshOriTri=body.sharedMesh.submeshes};morph.SetRenderer(body);
                maid.body0=new TBody();maid.body0.goSlot.Add(new TBodySkin{morph=morph});
            }
            MaskTailcoatBody(body);
        }
        var original=garment.sharedMesh.vertices;
        if(args.Length>4)
        {
            var lines=File.ReadAllLines(args[4]);float max=0;
            foreach(var line in lines.Skip(1))
            {
                var cells=line.Split(',').Select(x=>x.Trim('"')).ToArray();int i=int.Parse(cells[3]);
                var p=new Vector3(float.Parse(cells[5],CultureInfo.InvariantCulture),float.Parse(cells[6],CultureInfo.InvariantCulture),float.Parse(cells[7],CultureInfo.InvariantCulture));
                max=Math.Max(max,Vector3.Distance(original[i],p));original[i]=p;
            }
            Console.WriteLine("Model/dump max original vertex distance="+max.ToString("R"));
            garment.sharedMesh.vertices=original;
        }
        PrepareALContext(maid,new(){body,garment},1);var c=_alWorking;
        var rec=new MeshRecord{SMR=garment,Mesh=garment.sharedMesh,OrigVerts=original};
        TryDeformAL(garment,rec,MeshMorphClass.OuterCloth,1,out var result,out _);
        Vector3 ToFrame(Vector3 p)=>U(NV.Transform(N(p),c.InverseFrame));
        float[] P(Vector3 p)=>new[]{p.x,p.y,p.z};
        var rows=new List<object>();
        for(int i=0;i<original.Length;i++)
        {
            var reference=U(NV.Transform(N(original[i]),rec.ToReference));var local=ToFrame(reference);
            var field=BellyShape.Deform(N(local),c.Frame.Profile,1,Shape);
            float attachment=BellyShape.ClothingFootprint(N(local),c.Frame.Profile,1,Shape);
            Vector3 target=reference;bool hit=TryGetBodySurfaceClothTarget(c.Surface,reference,Shape.ClothOffset,c.Frame,out target);
            TryFindNearestBodySurfaceTriangle(c.Surface,reference,out var nearest);
            var tri=c.Surface.SurfaceTriangles[nearest.TriangleIndex];var mesh=c.Surface.Meshes[tri.MeshIndex];
            var normalM=Vector3.Cross(mesh.Morphed[tri.B]-mesh.Morphed[tri.A],mesh.Morphed[tri.C]-mesh.Morphed[tri.A]).normalized;
            var a=Vector3.Dot(nearest.Normal,normalM);var neighborY=ToFrame(nearest.Closest).y;
            rows.Add(new {i,original=P(local),field=P(U(field)),attachment,hit,target=P(ToFrame(target)),distance=Mathf.Sqrt(nearest.DistanceSq),neighborY,normalDot=a,
                result=P(ToFrame(U(NV.Transform(N(rec.Skirt==null?result[i]:rec.Skirt.VisualVertices[i]),rec.ToReference)))),
                upper=BoneSum(NativeWeights(garment)[i],NativeBones(garment),s=>s.Contains("spine1")),ownership=rec.TorsoOwnership[i],closest=P(ToFrame(nearest.Closest))});
        }
        var br=new MeshRecord{SMR=body,Mesh=body.sharedMesh,OrigVerts=body.sharedMesh.vertices};
        TryDeformAL(body,br,MeshMorphClass.Body,1,out var bodyDeformed,out _);
        float[][] FramePoints(Vector3[] points,MeshRecord rec)=>points.Select(p=>P(ToFrame(U(NV.Transform(N(p),rec.ToReference))))).ToArray();
        File.WriteAllText(args[1],JsonSerializer.Serialize(new{span=c.Frame.Profile.Span,navel=c.Frame.Profile.Navel,ribs=c.Frame.Profile.Ribs,rows,triangles=garment.sharedMesh.triangles,
            original=FramePoints(original,rec),changed=FramePoints(rec.Skirt==null?result:rec.Skirt.VisualVertices,rec),bodyOriginal=FramePoints(br.OrigVerts,br),body=FramePoints(bodyDeformed,br),bodyTriangles=body.sharedMesh.GetTriangles(0),
            meshToFrame=original.Take(4).Select(p=>P(p)).ToArray()}));
        Console.WriteLine("Upper diagnostics written; rows="+rows.Count);
    }
    static void MaskTailcoatBody(SkinnedMeshRenderer body)
    {
        // dress284_wear_i_.menu, in file order. Match TMorph.FixVisibleFlag:
        // any positive influence on a hidden bone collapses the whole triangle.
        var commands=new[]{("Spine1",false),("Spine1a",true),("Clavicle",false),("Forearm",true),("Foretwist1",false),("Mune",false)};
        var visible=body.bones.Select(b=>{
            bool v=true;foreach(var command in commands)
            {
                var seen=new HashSet<Transform>();
                for(var ancestor=b;ancestor!=null&&seen.Add(ancestor);ancestor=ancestor.parent)
                    if(ancestor.name.Contains(command.Item1)){v=command.Item2;break;}
            }
            return v;}).ToArray();
        int hidden=0;
        var subs=body.sharedMesh.submeshes.Select(source=>{
            var tri=(int[])source.Clone();
            for(int i=0;i<tri.Length;i+=3)if(Enumerable.Range(0,3).Any(k=>Influences(body.sharedMesh.boneWeights[tri[i+k]]).Any(w=>w.Weight>0&&!visible[w.Bone])))
            {tri[i]=tri[i+1]=tri[i+2]=0;hidden++;}
            return tri;}).ToArray();
        body.sharedMesh.submeshes=subs;body.sharedMesh.triangles=subs.SelectMany(t=>t).ToArray();
        Console.WriteLine("Tailcoat's native body mask: hidden triangles="+hidden);
    }
}
