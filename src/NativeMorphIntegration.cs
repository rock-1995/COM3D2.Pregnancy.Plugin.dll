using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        static readonly Dictionary<SkinnedMeshRenderer, TMorph> NativeMorphs = new Dictionary<SkinnedMeshRenderer, TMorph>();
        static readonly Dictionary<SkinnedMeshRenderer, Maid> NativeOwners = new Dictionary<SkinnedMeshRenderer, Maid>();
        static readonly FieldInfo NativeTempVertices = typeof(TMorph).GetField("m_vTmpVert", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo NativeTempNormals = typeof(TMorph).GetField("m_vTmpNorm", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo NativeHasMorph = typeof(TMorph).GetField("m_bMorph", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo NativeBlendValues = typeof(TMorph).GetField("BlendValues", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        internal static bool IsNativeMorphRelevant(TMorph morph)
        {
            if (morph == null || morph.bodyskin == null || morph.bodyskin.body == null) return false;
            var maid = morph.bodyskin.body.maid;
            if (maid == null || maid.boMAN) return false;
            var renderer = _clothBodyRendererField.GetValue(morph) as SkinnedMeshRenderer;
            if (renderer == null || renderer.sharedMesh == null || ClassifyMesh(renderer) == MeshMorphClass.Ignore) return false;
            NativeMorphs[renderer] = morph;
            NativeOwners[renderer] = maid;
            return true;
        }

        internal static bool NativeMorphWillChange(TMorph morph)
        {
            if (_suppressMorphBaseBake || !IsNativeMorphRelevant(morph)) return false;
            var previous = _blendValuesChkField.GetValue(morph) as float[];
            var values = NativeBlendValues.GetValue(morph) as float[];
            if (previous == null || values == null) return false;
            for (int i = 0; i < morph.MorphCount; i++)
                if (previous[i] != values[i]) return true;
            return false;
        }

        static bool NativeBodyVisible(SkinnedMeshRenderer renderer)
        {
            TMorph morph;
            // The slot flag is finalized in native phase 3. The corresponding
            // GameObject can still be inactive until TBodySkin.Update runs.
            return renderer.enabled && (NativeMorphs.TryGetValue(renderer, out morph) && morph.bodyskin != null
                ? morph.bodyskin.boVisible : renderer.gameObject.activeInHierarchy);
        }

        static int[] NativeOriginalTopology(SkinnedMeshRenderer renderer)
        {
            TMorph morph;
            return NativeMorphs.TryGetValue(renderer, out morph)
                ? MorphNormalTopology(morph, renderer.sharedMesh)
                : renderer.sharedMesh.triangles;
        }

        static int[] NativeOriginalSubmesh(SkinnedMeshRenderer renderer, int submesh)
        {
            TMorph morph;
            if (NativeMorphs.TryGetValue(renderer, out morph) && morph.m_nSubMeshOriTri != null && submesh < morph.m_nSubMeshOriTri.Length)
                return morph.m_nSubMeshOriTri[submesh];
            return renderer.sharedMesh.GetTriangles(submesh);
        }

        internal static void CaptureShapeForMaid(Maid maid)
        {
            if (maid != null) _maidShapes[maid.GetHashCode()] = Shape.Copy();
        }

        internal static void ApplyNativeAppearance(Maid maid, float progress)
        {
            if (!IsValid(maid)) return;
            PruneNativeMorphs();
            using (new MaidShapeScope(maid))
            using (new RefreshTrace(maid, "native-build"))
            {
                if (progress <= 0f)
                {
                    ResetInternal(maid);
                    SyncNativeOutput(maid);
                    return;
                }
                _activeProgress[maid.GetHashCode()] = Mathf.Clamp01(progress);
                _maidShapes[maid.GetHashCode()] = Shape.Copy();
                EnsureMonitor(maid);
                PruneRecords(maid);
                // A loaded but currently hidden garment must have its final
                // geometry too. Showing it later only restores native buffers.
                ApplyToSlots(maid, Mathf.Clamp01(progress), true);
                UpdateALBindings(maid);
                SyncNativeOutput(maid);
            }
        }

        static void PruneNativeMorphs()
        {
            var removed = new List<SkinnedMeshRenderer>();
            foreach (var pair in NativeMorphs)
                if (pair.Key == null || pair.Value.bodyskin == null || pair.Value.bodyskin.morph != pair.Value)
                    removed.Add(pair.Key);
            foreach (var renderer in removed) { NativeOwners.Remove(renderer); NativeMorphs.Remove(renderer); }
        }

        static void SyncNativeOutput(Maid maid)
        {
            foreach (var pair in NativeMorphs)
            {
                Maid owner;
                var renderer = pair.Key;
                var morph = pair.Value;
                if (!NativeOwners.TryGetValue(renderer, out owner) || owner != maid || renderer == null ||
                    renderer.sharedMesh == null || morph.bodyskin == null || (_clothBodyRendererField.GetValue(morph) as SkinnedMeshRenderer) != renderer) continue;
                var vertices = renderer.sharedMesh.vertices;
                var normals = renderer.sharedMesh.normals;
                var temporary = NativeTempVertices.GetValue(morph) as Vector3[];
                var temporaryNormals = NativeTempNormals.GetValue(morph) as Vector3[];
                if (temporary == null || temporary.Length != vertices.Length || temporaryNormals == null || temporaryNormals.Length != normals.Length)
                    throw new InvalidOperationException("Native customization buffers do not match the loaded mesh.");
                Array.Copy(vertices, temporary, vertices.Length);
                Array.Copy(normals, temporaryNormals, normals.Length);
                NativeHasMorph.SetValue(morph, true);
            }
        }

        internal static void ForgetNativeAppearance(Maid maid)
        {
            if (ReferenceEquals(maid, null)) return;
            ResetInternal(maid);
            SyncNativeOutput(maid);
            var removed = new List<SkinnedMeshRenderer>();
            foreach (var pair in NativeOwners) if (pair.Value == maid) removed.Add(pair.Key);
            foreach (var renderer in removed) { NativeOwners.Remove(renderer); NativeMorphs.Remove(renderer); }
        }
    }
}
