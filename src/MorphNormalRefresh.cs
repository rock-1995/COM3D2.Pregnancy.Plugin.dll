using System;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        sealed class MorphNormalCache
        {
            public Vector3[] Vertices, NativeNormals, CleanNormals, AppliedNormals;
            public int[] Triangles;
        }

        static bool SameNormalVectors(Vector3[] a,Vector3[] b)
        {
            if(a==null || b==null || a.Length!=b.Length)return false;
            for(int i=0;i<a.Length;i++)
                if(a[i].x!=b[i].x || a[i].y!=b[i].y || a[i].z!=b[i].z)return false;
            return true;
        }
        static bool SameNormalTopology(int[] a,int[] b)
        {
            if(a==null || b==null || a.Length!=b.Length)return false;
            for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;
            return true;
        }
        static int[] MorphNormalTopology(TMorph morph,Mesh mesh,int[] previous=null)
        {
            // Clothing visibility can collapse skin faces to (0,0,0). Normals
            // need their original neighbors even while those faces are hidden.
            // Only read this topology; never restore hidden render triangles.
            if(morph!=null && morph.m_nSubMeshOriTri!=null)
            {
                int count=mesh.vertexCount,length=0;bool same=previous!=null;
                foreach(var submesh in morph.m_nSubMeshOriTri)
                {
                    if(submesh==null || submesh.Length%3!=0)return mesh.triangles;
                    foreach(int i in submesh)
                    {
                        if(i<0 || i>=count)return mesh.triangles;
                        if(same && (length>=previous.Length || previous[length]!=i))same=false;
                        length++;
                    }
                }
                if(length>0)
                {
                    if(same && previous.Length==length)return previous;
                    var result=new int[length];int offset=0;
                    foreach(var submesh in morph.m_nSubMeshOriTri)
                    {Array.Copy(submesh,0,result,offset,submesh.Length);offset+=submesh.Length;}
                    return result;
                }
            }
            return mesh.triangles;
        }
        static int[] RendererNormalTopology(Maid maid,SkinnedMeshRenderer renderer)
        {
            var topology=ClothingBodyTopology(maid,renderer);
            if(topology!=null)return topology;
            foreach(var pair in _morphBaseBakeStates)
                if(pair.Value.Renderer==renderer && pair.Value.Mesh==renderer.sharedMesh)
                    return MorphNormalTopology(pair.Key,renderer.sharedMesh);
            return renderer.sharedMesh.triangles;
        }

        static Vector3[] CleanNormals(Maid maid,SkinnedMeshRenderer renderer,Vector3[] vertices,Vector3[] normals)
        {
            var record=FindRecord(maid,renderer);
            if(record!=null && record.RefreshReady && record.OrigNormals?.Length==normals.Length &&
                record.AppliedExactSignature==ExactVectors(vertices) && record.AppliedNormalSignature==ExactVectors(normals))
                return (Vector3[])record.OrigNormals.Clone();
            foreach(var pair in _morphBaseBakeStates)
            {
                var state=pair.Value;
                if(state.Renderer!=renderer || state.Mesh!=renderer.sharedMesh ||
                    state.OriginalNormals?.Length!=normals.Length || state.BakedNormals?.Length!=normals.Length ||
                    ComputeVertexSignature(pair.Key.m_vOriVert)!=state.AppliedSignature)continue;
                var cache=state.NormalCache;
                if(cache!=null && SameNormalVectors(vertices,cache.Vertices) &&
                    (SameNormalVectors(normals,cache.AppliedNormals) || SameNormalVectors(normals,cache.NativeNormals)))
                    return (Vector3[])cache.CleanNormals.Clone();
                // TMorph and AYS add their native morph-normal deltas to the
                // pregnancy-baked base. Remove only our known base delta,
                // matching CleanVertices, and retain the native normal changes.
                var clean=new Vector3[normals.Length];
                for(int i=0;i<clean.Length;i++)clean[i]=state.OriginalNormals[i]+(normals[i]-state.BakedNormals[i]);
                return clean;
            }
            return (Vector3[])normals.Clone();
        }

        static void RefreshBakedMorphNormals(Maid maid,TMorph morph,MorphBaseBakeState state)
        {
            // Clothing can bake in a different bind space (skirt offsets and
            // virtual carriers). Its visual-space normal pass stays in ApplySMR.
            if(state.MeshClass!=MeshMorphClass.Body)return;
            var mesh=state.Mesh;
            if(mesh==null || state.Renderer==null || state.Renderer.sharedMesh!=mesh)return;
            var vertices=mesh.vertices;var normals=mesh.normals;
            if(vertices.Length!=state.OriginalVerts.Length || normals.Length!=vertices.Length)return;
            var cache=state.NormalCache;var triangles=MorphNormalTopology(morph,mesh,cache?.Triangles);
            if(cache!=null && SameNormalVectors(vertices,cache.Vertices) && SameNormalTopology(triangles,cache.Triangles))
            {
                if(SameNormalVectors(normals,cache.AppliedNormals))return;
                // Native TMorph may upload the same base again. Reuse the
                // already corrected result without transporting it a second time.
                if(SameNormalVectors(normals,cache.NativeNormals)){mesh.normals=cache.AppliedNormals;return;}
            }
            var cleanVertices=CleanVertices(maid,state.Renderer);
            var cleanNormals=CleanNormals(maid,state.Renderer,vertices,normals);
            var applied=DeformationNormals.Build(cleanVertices,vertices,cleanNormals,triangles,state.NormalRegions);
            state.NormalCache=new MorphNormalCache{Vertices=vertices,NativeNormals=normals,CleanNormals=cleanNormals,AppliedNormals=applied,Triangles=triangles};
            // Keep TMorph/AYS's additive source buffers intact. Only the final
            // mesh receives normals matched to the native morph's final shape.
            if(!SameNormalVectors(normals,applied))mesh.normals=applied;
        }
    }
}
