using System.Reflection;
using UnityEngine;

namespace UnityEngine
{
    public struct Vector3(float x, float y = 0, float z = 0)
    {
        public float x = x, y = y, z = z;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x * b, a.y * b, a.z * b);
    }
    public class Mesh
    {
        Vector3[] v, n;
        public Vector3[] vertices { get => (Vector3[])v.Clone(); set => v = (Vector3[])value.Clone(); }
        public Vector3[] normals { get => (Vector3[])n.Clone(); set => n = (Vector3[])value.Clone(); }
        public int[] triangles = [0, 1, 2];
        public int[] GetTriangles(int submesh) => (int[])triangles.Clone();
    }
    public class GameObject { public bool activeInHierarchy = true; }
    public class SkinnedMeshRenderer
    {
        public Mesh sharedMesh;
        public bool enabled = true;
        public GameObject gameObject = new();
    }
    public static class Mathf { public static float Clamp01(float value) => Math.Clamp(value, 0, 1); }
}
public class Maid { public bool boMAN; public List<SkinnedMeshRenderer> Renderers = new(); }
public class TBody { public Maid maid; }
public class TBodySkin { public TBody body; public TMorph morph; public bool boVisible = true; }

// The clean-copy/add-deltas/Reset behavior below follows installed TMorph
// decompilation lines 1542-1595. Geometry generation itself is not under test.
public class TMorph
{
    public TBodySkin bodyskin;
    public int MorphCount = 1;
    public Vector3[] m_vOriVert, m_vOriNorm;
    public int[][] m_nSubMeshOriTri = [[0, 1, 2]];
    private readonly SkinnedMeshRenderer smr_src;
    private readonly float[] BlendValues = new float[1];
    private readonly float[] BlendValuesCHK = new float[1];
    private readonly Vector3[] m_vTmpVert, m_vTmpNorm;
    private bool m_bMorph;
    public TMorph(Maid maid, SkinnedMeshRenderer renderer)
    {
        smr_src = renderer;
        bodyskin = new TBodySkin { body = new TBody { maid = maid }, morph = this };
        m_vOriVert = renderer.sharedMesh.vertices; m_vOriNorm = renderer.sharedMesh.normals;
        m_vTmpVert = (Vector3[])m_vOriVert.Clone(); m_vTmpNorm = (Vector3[])m_vOriNorm.Clone();
    }
    public void SetBlend(float value) => BlendValues[0] = value;
    public void FixBlendValues()
    {
        int changed = 0;
        for (int i = 0; i < MorphCount; i++)
            if (BlendValuesCHK[i] != BlendValues[i]) { changed++; BlendValuesCHK[i] = BlendValues[i]; }
        if (changed == 0) return;
        m_vOriVert.CopyTo(m_vTmpVert, 0); m_vOriNorm.CopyTo(m_vTmpNorm, 0); m_bMorph = true;
        if (BlendValues[0] >= .01f)
        {
            m_vTmpVert[0] += new Vector3(2) * BlendValues[0];
            m_vTmpNorm[0] += new Vector3(.2f) * BlendValues[0];
        }
        smr_src.sharedMesh.vertices = m_vTmpVert; smr_src.sharedMesh.normals = m_vTmpNorm;
    }
    public void ResetBlendValues()
    {
        if (m_bMorph) { smr_src.sharedMesh.vertices = m_vTmpVert; smr_src.sharedMesh.normals = m_vTmpNorm; }
    }
}

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        static readonly FieldInfo _clothBodyRendererField = typeof(TMorph).GetField("smr_src", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo _blendValuesChkField = typeof(TMorph).GetField("BlendValuesCHK", BindingFlags.NonPublic | BindingFlags.Instance);
        static bool _suppressMorphBaseBake;
        sealed class Settings { public Settings Copy() => new(); }
        static Settings Shape = new();
        static readonly Dictionary<int, Settings> _maidShapes = new();
        static readonly Dictionary<int, float> _activeProgress = new();
        internal enum MeshMorphClass { Ignore, Body }
        static MeshMorphClass ClassifyMesh(SkinnedMeshRenderer renderer) => MeshMorphClass.Body;
        static bool IsValid(Maid maid) => maid != null && !maid.boMAN;
        sealed class MaidShapeScope : IDisposable { public MaidShapeScope(Maid maid) { } public void Dispose() { } }
        sealed class RefreshTrace : IDisposable { public RefreshTrace(Maid maid, string operation) { } public void Dispose() { } }
        static void EnsureMonitor(Maid maid) { }
        static void PruneRecords(Maid maid) { }
        static void UpdateALBindings(Maid maid) { }
        static int[] MorphNormalTopology(TMorph morph, Mesh mesh) => morph.m_nSubMeshOriTri.SelectMany(x => x).ToArray();
        sealed class DeformRecord { public Vector3[] Base, Normals, Output; }
        static readonly Dictionary<SkinnedMeshRenderer, DeformRecord> Records = new();
        internal static bool IncludedInactive;
        static bool Same(Vector3[] a, Vector3[] b) => a != null && b != null && a.Length == b.Length &&
            a.Zip(b).All(p => p.First.x == p.Second.x && p.First.y == p.Second.y && p.First.z == p.Second.z);
        static void ApplyToSlots(Maid maid, float progress, bool includeInactive)
        {
            IncludedInactive = includeInactive;
            foreach (var renderer in maid.Renderers)
            {
                if (!includeInactive && !renderer.enabled) continue;
                var mesh = renderer.sharedMesh;
                var current = mesh.vertices;
                if (!Records.TryGetValue(renderer, out var record) || !Same(current, record.Output))
                    Records[renderer] = record = new DeformRecord { Base = current, Normals = mesh.normals };
                var output = (Vector3[])record.Base.Clone();
                output[0] += new Vector3(10) * progress;
                var normals = (Vector3[])record.Normals.Clone();
                normals[0] += new Vector3(1) * progress;
                record.Output = output;
                mesh.vertices = output; mesh.normals = normals;
            }
        }
        static void ResetInternal(Maid maid)
        {
            _activeProgress.Remove(maid.GetHashCode()); _maidShapes.Remove(maid.GetHashCode());
            foreach (var renderer in maid.Renderers)
                if (Records.Remove(renderer, out var record) && Same(renderer.sharedMesh.vertices, record.Output))
                { renderer.sharedMesh.vertices = record.Base; renderer.sharedMesh.normals = record.Normals; }
        }
        internal static int RegisteredMorphCount => NativeMorphs.Count;
        internal static bool BodyVisibleForTests(SkinnedMeshRenderer renderer) => NativeBodyVisible(renderer);
    }
}
