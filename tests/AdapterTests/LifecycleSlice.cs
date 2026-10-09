// Methods below are extracted verbatim from production by sync_refresh_tests.py.
// Only Unity scene discovery / native engine calls are replaced by shims.
using UnityEngine;
using Object=UnityEngine.Object;
namespace COM3D2.Pregnancy.Plugin;
public static partial class BellyMorphController
{
    static Dictionary<int,float> _activeProgress=new();
    static Dictionary<int,HashSet<TBodySkin>> _morphDirtySkins=new();
    static Dictionary<int,int> _meshInventorySignatures=new();
    static HashSet<int> _pendingVisibilityApply=new();
    static bool _bpWorldCached,_suppressMorphBaseBake;
    static Dictionary<string,Matrix4x4> _bpBoneWorld=new();
    const int AutoMorphStableFrames=2;
    static MorphTriggerMode CurrentTriggerMode=>MorphTriggerMode.VisibilityChange;
    public static bool IsValid(Maid m)=>m!=null&&m.body0!=null;
    static List<SkinnedMeshRenderer> CollectTargetRenderers(Maid m)=>m.Renderers.ToList();
    static void AttachNotifier(GameObject obj,Maid m){}
    static bool TryCacheBindPoseWorldRef(SkinnedMeshRenderer r)=>true;
    static bool CacheBindPoseWorldForMaid(Maid m)=>true;
    static void MaybeLogMeshInventory(Maid m,List<SkinnedMeshRenderer> r,string s){}
    static bool IsFaceSlot(string s)=>false;
    static string GetMeshId(SkinnedMeshRenderer r)=>r.name;
    static string GetMaidName(Maid m)=>"test";
    static void LogMorphScan(Maid m,SkinnedMeshRenderer r,MeshMorphClass c,bool a,bool b){}
    static void FlushMorphDirty(Maid m){if(_morphDirtySkins.TryGetValue(m.GetHashCode(),out var s)&&s.Count>0)throw new Exception("Dirty engine callback not simulated");}
    static SkinnedMeshRenderer FindRendererForMorph(Maid m,TMorph t)=>t.Renderer;
    static float GetEffectiveMorphProgress(float p,MeshMorphClass c)=>Mathf.Clamp01(p);
    static void ForceBlendValuesDirty(TMorph m){}
    static void LogMorphBaseBake(Maid m,TMorph t,SkinnedMeshRenderer r,MeshMorphClass c,float p,float ep,int id,MorphBaseBakeState state,DeformStats stats,bool reused){}
public static float GetActiveProgress(Maid maid)
        {
            return maid != null && _activeProgress.TryGetValue(maid.GetHashCode(), out float p) ? p : 0f;
        }
public static void ApplyProgress(Maid maid, float progress)
        {
            using(new RefreshTrace(maid,"apply"))
            {
                if (!IsValid(maid)) return;
                _bpWorldCached = false;
                _activeProgress[maid.GetHashCode()] = Mathf.Clamp01(progress);
                if(progress<=0){ResetInternal(maid);return;}
                _maidShapes[maid.GetHashCode()]=Shape.Copy();
                EnsureMonitor(maid);
                PruneRecords(maid);

                ApplyToSlots(maid, Mathf.Clamp01(progress), false);
            }
        }
public static void Reset(Maid maid)
        {
            if (maid == null) return;

            ResetInternal(maid);
            _bpWorldCached = false;

            var mon = maid.gameObject.GetComponent<BellyMonitor>();
            if (mon != null) Object.DestroyImmediate(mon);
        }
static void ResetInternal(Maid maid)
        {
            int key = maid.GetHashCode();
            _activeProgress.Remove(key);
            _maidShapes.Remove(key);
            _morphDirtySkins.Remove(key);
            ReleaseALBindings(maid);
            _alContexts.Remove(key);
            RestoreMorphBases(maid);

            if (!_records.TryGetValue(key, out var records)) return;

            foreach (var r in records)
            {
                if (r.SMR != null && r.Mesh != null && r.OrigVerts != null)
                {
                    Vector3[] currentVerts = r.Mesh.vertices;
                    if (r.LastDeltaVerts != null
                        && currentVerts != null
                        && currentVerts.Length == r.LastDeltaVerts.Length
                        && r.AppliedSignature != 0
                        && ComputeVertexSignature(currentVerts) == r.AppliedSignature)
                    {
                        for (int i = 0; i < currentVerts.Length; i++)
                            currentVerts[i] -= r.LastDeltaVerts[i];
                        r.Mesh.vertices = currentVerts;
                        if(r.OrigNormals!=null && r.OrigNormals.Length==currentVerts.Length)
                            r.Mesh.normals=(Vector3[])r.OrigNormals.Clone();
                        r.Mesh.RecalculateBounds();
                    }
                }
            }

            records.Clear();
            _records.Remove(key);
        }
static void ApplyToSlots(Maid maid, float progress, bool includeInactive)
        {
            if (maid == null || maid.body0 == null || maid.body0.goSlot == null) return;
            List<SkinnedMeshRenderer> targetSmrs = CollectTargetRenderers(maid);
            MaybeLogMeshInventory(maid, targetSmrs, "apply");

            _bpWorldCached = false;
            _bpBoneWorld.Clear();
            foreach (SkinnedMeshRenderer smr in targetSmrs)
            {
                if (smr?.sharedMesh == null) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.Body) continue;
                if (TryCacheBindPoseWorldRef(smr)) break;
            }

            HashSet<int> appliedMeshIds = new HashSet<int>();
            PrepareALContext(maid, targetSmrs, progress);
            foreach (SkinnedMeshRenderer smr in targetSmrs)
            {
                if (smr?.sharedMesh == null) continue;

                MeshMorphClass meshClass = ClassifyMesh(smr);
                if (meshClass == MeshMorphClass.Ignore)
                {
                    if (IsDebugMeshLoggingEnabled() && !IsFaceSlot(GetMeshId(smr)))
                        _log.LogInfo($"[BellyDiag] unrecognized maid={GetMaidName(maid)}"
                            + $" id={GetMeshId(smr)}"
                            + $" verts={smr.sharedMesh.vertexCount}");
                    continue;
                }

                AttachNotifier(smr.gameObject, maid);

                bool willApply = includeInactive || (smr.enabled && smr.gameObject.activeInHierarchy);
                if (IsDebugMeshLoggingEnabled())
                    LogMorphScan(maid, smr, meshClass, includeInactive, willApply);

                if (willApply)
                {
                    int meshId = smr.sharedMesh.GetInstanceID();
                    if (!appliedMeshIds.Add(meshId))
                    {
                        if (IsDebugMeshLoggingEnabled())
                            LogMorphSkip(maid, smr, meshClass, "duplicate-shared-mesh");
                        continue;
                    }

                    ApplySMR(maid, smr, progress, meshClass, false);

                }
            }


        }
static void PruneRecords(Maid maid)
        {
            if (maid == null) return;

            int key = maid.GetHashCode();
            if (!_records.TryGetValue(key, out var records)) return;

            records.RemoveAll(r =>
                r == null
                || r.SMR == null
                || r.Mesh == null
                || r.SMR.sharedMesh == null
                || r.SMR.sharedMesh != r.Mesh);

            if (records.Count == 0)
                _records.Remove(key);
        }
static BellyMonitor EnsureMonitor(Maid maid)
        {
            var mon = maid.gameObject.GetComponent<BellyMonitor>();
            if (mon == null)
            {
                mon = maid.gameObject.AddComponent<BellyMonitor>();
                mon.SetMaid(maid);
            }
            return mon;
        }
static int GetRendererKey(SkinnedMeshRenderer smr)
        {
            return smr != null ? smr.GetInstanceID() : 0;
        }
static int ComputeMeshSignature(SkinnedMeshRenderer smr)
        {
            Mesh mesh = smr != null ? smr.sharedMesh : null;
            if (mesh == null) return 0;

            Vector3[] verts = mesh.vertices;
            return ComputeVertexSignature(verts);
        }
static int ComputeVisibilitySignature(List<SkinnedMeshRenderer> renderers)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < renderers.Count; i++)
                {
                    SkinnedMeshRenderer smr = renderers[i];
                    if (smr == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    hash = hash * 31 + smr.GetInstanceID();
                    hash = hash * 31 + (smr.sharedMesh != null ? smr.sharedMesh.GetInstanceID() : 0);
                    hash = hash * 31 + (smr.enabled ? 1 : 0);
                    hash = hash * 31 + (smr.gameObject.activeSelf ? 1 : 0);
                    hash = hash * 31 + (smr.gameObject.activeInHierarchy ? 1 : 0);
                }
                return hash;
            }
        }
static List<SkinnedMeshRenderer> CollectRelevantRenderers(Maid maid)
        {
            List<SkinnedMeshRenderer> result = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer smr in CollectTargetRenderers(maid))
            {
                if (smr?.sharedMesh == null) continue;
                if (ClassifyMesh(smr) == MeshMorphClass.Ignore) continue;
                result.Add(smr);
            }
            return result;
        }
public class BellyMonitor : MonoBehaviour
        {
            Maid _maid;
            bool _needsFullRefresh = false;
            int _refreshStableFrames = 0;
            readonly Dictionary<int, int> _refreshPreviousSignatures = new Dictionary<int, int>();
            bool _visibilityPending = false;
            int _visibilityKey = 0;
            int _visibilityStableFrames = 0;
            int _visibilityPreviousSignature = 0;
            bool _visibilityHasPrevious = false;

            public void SetMaid(Maid m) { _maid = m; }

            public void TriggerFullRefresh()
            {
                if (_maid == null) return;
                _needsFullRefresh = true;
                _refreshStableFrames = 0;
                _refreshPreviousSignatures.Clear();
            }

            public void RequestVisibilityApply(int key)
            {
                if (_maid == null)
                {
                    _pendingVisibilityApply.Remove(key);
                    return;
                }

                _visibilityPending = true;
                _visibilityKey = key;
                _visibilityStableFrames = 0;
                _visibilityPreviousSignature = 0;
                _visibilityHasPrevious = false;
            }

            void ClearVisibilityApply()
            {
                _pendingVisibilityApply.Remove(_visibilityKey);
                _visibilityPending = false;
                _visibilityKey = 0;
                _visibilityStableFrames = 0;
                _visibilityPreviousSignature = 0;
                _visibilityHasPrevious = false;
            }

            bool AreTrackedRenderersStable(List<SkinnedMeshRenderer> renderers, Dictionary<int, int> previous)
            {
                bool stable = true;
                HashSet<int> live = new HashSet<int>();

                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    int key = GetRendererKey(smr);
                    int sig = ComputeMeshSignature(smr);
                    live.Add(key);

                    if (!previous.TryGetValue(key, out int prevSig) || prevSig != sig)
                        stable = false;

                    previous[key] = sig;
                }

                List<int> staleKeys = new List<int>();
                foreach (KeyValuePair<int, int> entry in previous)
                {
                    if (!live.Contains(entry.Key))
                        staleKeys.Add(entry.Key);
                }
                foreach (int stale in staleKeys)
                    previous.Remove(stale);

                return stable;
            }

            void ProcessFullRefresh()
            {
                if (!_needsFullRefresh)
                    return;

                if (_maid == null)
                {
                    _needsFullRefresh = false;
                    _refreshPreviousSignatures.Clear();
                    return;
                }

                if (_maid.body0 == null || !_maid.body0.isLoadedBody)
                    return;

                List<SkinnedMeshRenderer> tracked = CollectRelevantRenderers(_maid);
                if (AreTrackedRenderersStable(tracked, _refreshPreviousSignatures))
                    _refreshStableFrames++;
                else
                    _refreshStableFrames = 0;

                if (_refreshStableFrames < AutoMorphStableFrames)
                    return;

                _needsFullRefresh = false;
                _refreshStableFrames = 0;
                _refreshPreviousSignatures.Clear();
                PruneRecords(_maid);

                float pFull = GetActiveProgress(_maid);
                if (pFull > 0f)
                    RefreshProgress(_maid, pFull);
            }

            void ProcessVisibilityApply()
            {
                if (!_visibilityPending)
                    return;

                if (_maid == null)
                {
                    ClearVisibilityApply();
                    return;
                }

                if (_maid.body0 == null || !_maid.body0.isLoadedBody)
                    return;

                int currentSignature = ComputeVisibilitySignature(CollectRelevantRenderers(_maid));
                if (_visibilityHasPrevious && currentSignature == _visibilityPreviousSignature)
                    _visibilityStableFrames++;
                else
                    _visibilityStableFrames = 0;

                _visibilityPreviousSignature = currentSignature;
                _visibilityHasPrevious = true;

                if (_visibilityStableFrames < AutoMorphStableFrames)
                    return;

                ClearVisibilityApply();
                if (CurrentTriggerMode != MorphTriggerMode.VisibilityChange) return;
                if (!PregnancyManager.GetPregnant(_maid)) return;

                float progress = PregnancyManager.GetProgress(_maid);
                if (progress <= 0f) return;

                RefreshProgress(_maid, progress);
            }

            void LateUpdate()
            {
                if (_maid == null) return;
                if (IsDebugMeshLoggingEnabled())
                    MaybeLogMeshInventory(_maid, CollectTargetRenderers(_maid), "monitor");
                else _meshInventorySignatures.Remove(_maid.GetHashCode());
                BellyMorphController.FlushMorphDirty(_maid);
                ProcessFullRefresh();
                ProcessVisibilityApply();
                UpdateALBindings(_maid);
            }

            void OnDestroy()
            {
                // Release only the resources introduced by virtual skinning.
                if (object.ReferenceEquals(_maid, null)) return;
                ReleaseALBindings(_maid);
                _alContexts.Remove(_maid.GetHashCode());
                _maidShapes.Remove(_maid.GetHashCode());
            }
        }
static bool TryBakeRuntimeMorphBase(Maid maid, TMorph morph, float progress)
        {
            using(new MaidShapeScope(maid))
            using(new RefreshTrace(maid,"morph-base"))
            {
                Vector3[] currentVerts = morph.m_vOriVert;
                if (currentVerts == null || currentVerts.Length == 0) return false;

                SkinnedMeshRenderer smr = FindRendererForMorph(maid, morph);
                if (smr == null || smr.sharedMesh == null) return false;

                MeshMorphClass meshClass = ClassifyMesh(smr);
                if (meshClass == MeshMorphClass.Ignore) return false;

                Mesh mesh = smr.sharedMesh;
                int meshId = mesh.GetInstanceID();
                int shapeSignature = ComputeMorphBakeSignature(progress, meshClass);
                int currentSignature = ComputeVertexSignature(currentVerts);

                MorphBaseBakeState state;
                bool hasState = _morphBaseBakeStates.TryGetValue(morph, out state)
                    && state != null
                    && state.MeshInstanceId == meshId
                    && state.OriginalVerts != null
                    && state.OriginalVerts.Length == currentVerts.Length;

                bool currentIsKnownBaked = hasState
                    && state.AppliedSignature != 0
                    && currentSignature == state.AppliedSignature;

                if (currentIsKnownBaked && state.ShapeSignature == shapeSignature)
                    return false;

                Vector3[] baseVerts;
                Vector3[] baseNormals;
                bool reusedStoredBase = false;
                if (currentIsKnownBaked)
                {
                    baseVerts = (Vector3[])state.OriginalVerts.Clone();
                    baseNormals = CloneVectorArray(state.OriginalNormals);
                    reusedStoredBase = true;
                }
                else
                {
                    baseVerts = (Vector3[])currentVerts.Clone();
                    baseNormals = CloneNormalsForMorph(morph.m_vOriNorm, mesh, currentVerts.Length);
                }

                if (!CacheBindPoseWorldForMaid(maid)) return false;
                PrepareALContext(maid, CollectTargetRenderers(maid), progress);

                MeshRecord rec = new MeshRecord
                {
                    SMR = smr,
                    Mesh = mesh,
                    OrigVerts = baseVerts,
                    OrigNormals = baseNormals,
                };

                bool[] mask = BuildVertexMask(smr, rec, meshClass);
                if (!HasAnyMaskedVertex(mask)) return false;

                float effectiveProgress = GetEffectiveMorphProgress(progress, meshClass);
                Vector3[] bakedVerts;
                DeformStats stats;
                long deformStarted=System.Diagnostics.Stopwatch.GetTimestamp();
                if(_refreshStats!=null)_refreshStats.Rebuilt++;
                if (!TryDeformVertsInBindPoseWorld(
                    smr,
                    rec,
                    mask,
                    meshClass,
                    effectiveProgress,
                    out bakedVerts,
                    out stats))
                {
                    return false;
                }

                if(_refreshStats!=null)_refreshStats.DeformTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-deformStarted;
                long normalStarted=System.Diagnostics.Stopwatch.GetTimestamp();
                Vector3[] bakedNormals = BuildMorphBaseNormals(mesh, rec, bakedVerts);
                if (bakedNormals == null || bakedNormals.Length != bakedVerts.Length)
                    bakedNormals = CloneVectorArray(baseNormals);
                if(_refreshStats!=null)_refreshStats.NormalTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-normalStarted;

                int originalSignature = ComputeVertexSignature(baseVerts);
                int appliedSignature = ComputeVertexSignature(bakedVerts);
                int oldOriVertId = currentVerts.GetHashCode();

                morph.m_vOriVert = bakedVerts;
                if (bakedNormals != null && bakedNormals.Length == bakedVerts.Length)
                    morph.m_vOriNorm = bakedNormals;

                MorphBaseBakeState newState = new MorphBaseBakeState
                {
                    Maid = maid,
                    Skin = morph.bodyskin,
                    Renderer = smr,
                    Mesh = mesh,
                    OriginalVerts = baseVerts,
                    OriginalNormals = baseNormals,
                    BakedVerts = bakedVerts,
                    BakedNormals = bakedNormals,
                    OriginalSignature = originalSignature,
                    AppliedSignature = appliedSignature,
                    ShapeSignature = shapeSignature,
                    Progress = progress,
                    MeshInstanceId = meshId,
                    MeshClass = meshClass,
                };
                _morphBaseBakeStates[morph] = newState;
                _maidShapes[maid.GetHashCode()]=Shape.Copy();

                ForceFixBlendValues(morph);
                long bindingStarted=System.Diagnostics.Stopwatch.GetTimestamp();
                InstallALBinding(maid, rec, bakedVerts, progress);
                if(_refreshStats!=null)_refreshStats.BindingTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-bindingStarted;
                if(ComputeVertexSignature(mesh.vertices)==appliedSignature)
                {
                    rec.LastDeltaVerts=BuildDeltaVerts(baseVerts,bakedVerts);rec.AppliedSignature=appliedSignature;
                    if(!_records.TryGetValue(maid.GetHashCode(),out var records))_records[maid.GetHashCode()]=records=new List<MeshRecord>();
                    records.RemoveAll(r=>r.SMR==smr);records.Add(rec);
                    RememberAppliedMesh(rec,bakedVerts,progress,meshClass,MeshStructureSignature(smr));
                }
                LogMorphBaseBake(maid, morph, smr, meshClass, progress, effectiveProgress, oldOriVertId, newState, stats, reusedStoredBase);

                return true;
            }
        }
static bool HasAnyMaskedVertex(bool[] mask)
        {
            if (mask == null) return false;
            for (int i = 0; i < mask.Length; i++)
                if (mask[i]) return true;
            return false;
        }
static Vector3[] CloneVectorArray(Vector3[] values)
        {
            return values != null ? (Vector3[])values.Clone() : null;
        }
static Vector3[] CloneNormalsForMorph(Vector3[] morphNormals, Mesh mesh, int vertexCount)
        {
            if (morphNormals != null && morphNormals.Length == vertexCount)
                return (Vector3[])morphNormals.Clone();

            Vector3[] meshNormals = mesh != null ? mesh.normals : null;
            if (meshNormals != null && meshNormals.Length == vertexCount)
                return (Vector3[])meshNormals.Clone();

            return null;
        }
static Vector3[] BuildMorphBaseNormals(Mesh mesh, MeshRecord rec, Vector3[] bakedVerts)
        {
            if (mesh == null || bakedVerts == null) return null;

            Vector3[] oldVerts = null;
            Vector3[] oldNormals = null;
            try
            {
                oldVerts = mesh.vertices;
                oldNormals = mesh.normals;
                mesh.vertices = bakedVerts;

                if (rec != null
                    && rec.OrigVerts != null
                    && rec.OrigNormals != null
                    && rec.OrigVerts.Length == bakedVerts.Length
                    && rec.OrigNormals.Length == bakedVerts.Length)
                {
                    ApplySmoothedNormals(mesh, rec, bakedVerts);
                }
                else
                {
                    mesh.RecalculateNormals();
                }

                Vector3[] normals = mesh.normals;
                if (normals != null && normals.Length == bakedVerts.Length)
                    return (Vector3[])normals.Clone();
            }
            catch
            {
            }
            finally
            {
                if (oldVerts != null)
                    mesh.vertices = oldVerts;
                if (oldNormals != null)
                    mesh.normals = oldNormals;
            }

            return rec != null ? CloneVectorArray(rec.OrigNormals) : null;
        }
static void ForceFixBlendValues(TMorph morph)
        {
            if (object.ReferenceEquals(morph, null)) return;

            bool oldSuppress = _suppressMorphBaseBake;
            _suppressMorphBaseBake = true;
            try
            {
                ForceBlendValuesDirty(morph);
                morph.FixBlendValues();
            }
            finally
            {
                _suppressMorphBaseBake = oldSuppress;
            }
        }
static void RestoreMorphBases(Maid maid)
        {
            if (maid == null || _morphBaseBakeStates.Count == 0) return;

            List<TMorph> toRestore = new List<TMorph>();
            foreach (KeyValuePair<TMorph, MorphBaseBakeState> entry in _morphBaseBakeStates)
            {
                MorphBaseBakeState state = entry.Value;
                if (state != null && object.ReferenceEquals(state.Maid, maid))
                    toRestore.Add(entry.Key);
            }

            for (int i = 0; i < toRestore.Count; i++)
                RestoreMorphBase(toRestore[i], true);
        }
static void RestoreMorphBase(TMorph morph, bool forceFix)
        {
            if (object.ReferenceEquals(morph, null)) return;

            MorphBaseBakeState state;
            if (!_morphBaseBakeStates.TryGetValue(morph, out state) || state == null) return;

            if (state.Renderer != null) ReleaseALBinding(state.Renderer);
            try
            {
                Vector3[] currentVerts = morph.m_vOriVert;
                bool currentIsOurs = currentVerts != null
                    && state.AppliedSignature != 0
                    && ComputeVertexSignature(currentVerts) == state.AppliedSignature;

                if (currentIsOurs && state.OriginalVerts != null)
                {
                    morph.m_vOriVert = (Vector3[])state.OriginalVerts.Clone();
                    if (state.OriginalNormals != null && state.OriginalNormals.Length == state.OriginalVerts.Length)
                        morph.m_vOriNorm = (Vector3[])state.OriginalNormals.Clone();
                    if (forceFix)
                        ForceFixBlendValues(morph);
                }
            }
            finally
            {
                _morphBaseBakeStates.Remove(morph);
            }
        }
}
