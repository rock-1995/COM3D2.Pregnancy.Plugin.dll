using UnityEngine;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static void TestHiddenBodyTopology()
    {
        var (maid,body,cloth)=Fixture();
        var nativeTriangles=(int[])body.sharedMesh.triangles.Clone();
        var nativeVertices=body.sharedMesh.vertices;
        int writes=body.sharedMesh.VertexWrites;
        Vector3[] Apply(Maid owner,SkinnedMeshRenderer mesh,MeshMorphClass kind)
        {
            PrepareALContext(owner,new(){body,cloth},1);
            TryDeformAL(mesh,new MeshRecord{SMR=mesh,Mesh=mesh.sharedMesh,OrigVerts=mesh.sharedMesh.vertices},kind,1,out var result,out _);
            return result;
        }
        var fullCloth=Apply(maid,cloth,MeshMorphClass.OuterCloth);
        var fullBody=Apply(maid,body,MeshMorphClass.Body);
        var hidden=(int[])nativeTriangles.Clone();
        for(int i=0;i<hidden.Length;i+=3)
            if(Enumerable.Range(0,3).Any(k=>nativeVertices[hidden[i+k]].y>1.75f))hidden[i]=hidden[i+1]=hidden[i+2]=0;
        body.sharedMesh.triangles=hidden;
        var broken=Apply(new Maid(),cloth,MeshMorphClass.OuterCloth);
        float error=broken.Select((v,i)=>Vector3.Distance(v,fullCloth[i])).Max();
        Check(error>.02f,"hidden body topology reproduces clothing attachment distortion");
        var morph=new TMorph{m_vOriVert=nativeVertices,m_nSubMeshOriTri=new[]{nativeTriangles}};morph.SetRenderer(body);
        var fixedMaid=new Maid{body0=new TBody()};fixedMaid.body0.goSlot.Add(new TBodySkin{morph=morph});
        var corrected=Apply(fixedMaid,cloth,MeshMorphClass.OuterCloth);
        float correctedError=corrected.Select((v,i)=>Vector3.Distance(v,fullCloth[i])).Max();
        Check(correctedError<2e-6f,"hidden clothing contact equals full original body surface");
        Check(body.sharedMesh.triangles==hidden&&nativeTriangles.Any(i=>i!=0),"body visibility and retained original topology untouched");
        Check(Apply(fixedMaid,body,MeshMorphClass.Body).Select((v,i)=>Vector3.Distance(v,fullBody[i])).Max()<2e-6f,"body deformation unchanged by contact topology");
        Check(body.sharedMesh.VertexWrites==writes&&nativeVertices.Zip(body.sharedMesh.vertices,(a,b)=>Vector3.Distance(a,b)).Max()==0,"contact does not rewrite body geometry");
        var unrelated=new TMorph{m_vOriVert=nativeVertices,m_nSubMeshOriTri=new[]{nativeTriangles}};unrelated.SetRenderer(cloth);
        var wrongMaid=new Maid{body0=new TBody()};wrongMaid.body0.goSlot.Add(new TBodySkin{morph=unrelated});
        Check(ClothingBodyTopology(wrongMaid,body)==null,"same vertex count cannot select another renderer's topology");
        morph.m_nSubMeshOriTri=new[]{new[]{0,1,nativeVertices.Length}};
        Check(ClothingBodyTopology(fixedMaid,body)==null,"invalid retained topology falls back safely");
        Console.WriteLine($"Hidden-body contact regression: max attachment error={error:F6}, corrected={correctedError:F6}");
    }
}
