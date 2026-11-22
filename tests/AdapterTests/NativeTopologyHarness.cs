using System.Reflection;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static readonly FieldInfo _blendValuesChkField = typeof(TMorph).GetField(
        "BlendValuesChk", BindingFlags.Instance | BindingFlags.NonPublic);

    static TMorph RegisterNativeMesh(Maid maid, SkinnedMeshRenderer renderer)
    {
        var morph = new TMorph {
            m_vOriVert=renderer.sharedMesh.vertices,
            m_vOriNorm=renderer.sharedMesh.normals,
            m_nSubMeshOriTri=Enumerable.Range(0,renderer.sharedMesh.subMeshCount)
                .Select(i=>(int[])renderer.sharedMesh.GetTriangles(i).Clone()).ToArray()
        };
        morph.SetRenderer(renderer);
        var skin=new TBodySkin {morph=morph,body=maid.body0};
        morph.bodyskin=skin;
        maid.body0.goSlot.Add(skin);
        Check(IsNativeMorphRelevant(morph),"native mesh registration succeeds");
        return morph;
    }

    static (Maid maid,SkinnedMeshRenderer body,SkinnedMeshRenderer cloth,TMorph bodyMorph,TMorph clothMorph) NativeFixture()
    {
        var (maid,body,cloth)=LiveFixture();
        foreach(var renderer in new[]{body,cloth})
            renderer.sharedMesh.normals=renderer.sharedMesh.vertices.Select(v=>new Vector3(v.x/(1.35f*1.35f),0,(v.z+.15f)/(.85f*.85f)).normalized).ToArray();
        return(maid,body,cloth,RegisterNativeMesh(maid,body),RegisterNativeMesh(maid,cloth));
    }

    static int[] HiddenFaces(int[] topology,Vector3[] vertices)
    {
        var hidden=(int[])topology.Clone();
        for(int i=0;i<hidden.Length;i+=3)
            if(vertices[hidden[i]].y>1.75f || vertices[hidden[i+1]].y>1.75f || vertices[hidden[i+2]].y>1.75f)
                hidden[i]=hidden[i+1]=hidden[i+2]=0;
        return hidden;
    }


}
