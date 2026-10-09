using COM3D2.Pregnancy.Plugin.Growth;
using NVector = COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
using NMatrix = COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        public const int HaraMax = 100;
        const int AutoMorphStableFrames = 2;

        const float ReferenceBodyUp = 0.25f;
        const float ReferenceBodySide = 0.25f;
        const float MinBodyRelativeScale = 0.35f;
        const float MaxBodyRelativeScale = 3.0f;

        public static VtxSettings Shape = new VtxSettings();

        static readonly string[] FaceKeywords =
        {
            "face", "head", "eye", "mayu", "tooth", "teeth",
            "tongue", "lip", "nose", "ear"
        };

        static bool IsFaceSlot(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            string lower = name.ToLowerInvariant();
            if (lower.Contains("wear")) return false;
            foreach (string kw in FaceKeywords)
                if (lower.Contains(kw)) return true;

            return false;
        }

        static Dictionary<int, float> _activeProgress = new Dictionary<int, float>();
        static Dictionary<int, int> _aysHaraWriteSpyCountBySession = new Dictionary<int, int>();
        static bool _aysHooksPatched;

        class MeshRecord
        {
            public SkinnedMeshRenderer SMR;
            public Mesh Mesh;
            public Vector3[] OrigVerts;
            public Vector3[] OrigNormals;
            public Vector3[] LastDeltaVerts;
            public Vector3[] LastNewV;
            public NMatrix ToReference;
            public float[] BellyInfluence;
            public float[] TorsoOwnership;
            public float[] ThighGuardRestore;
            public bool[] BreastExcluded;
            public List<int>[] Neighbors;
            public ALContext GrowthContext;
            public NavelAttachment Navel;
            public SkirtDrapePlan Skirt;
            public ClothingMotionPlan ClothingMotion;
            public int AppliedSignature;
        }

        class MorphBaseBakeState
        {
            public Maid Maid;
            public TBodySkin Skin;
            public SkinnedMeshRenderer Renderer;
            public Mesh Mesh;
            public Vector3[] OriginalVerts;
            public Vector3[] OriginalNormals;
            public Vector3[] BakedVerts;
            public Vector3[] BakedNormals;
            public int OriginalSignature;
            public int AppliedSignature;
            public int ShapeSignature;
            public float Progress;
            public int MeshInstanceId;
            public MeshMorphClass MeshClass;
        }

        enum MeshMorphClass
        {
            Ignore,
            Body,
            InnerCloth,
            OuterCloth,
            NavelAccessory,
        }

        static Dictionary<int, List<MeshRecord>> _records = new Dictionary<int, List<MeshRecord>>();
        static readonly Dictionary<int, HashSet<TBodySkin>> _morphDirtySkins = new Dictionary<int, HashSet<TBodySkin>>();
        static readonly Dictionary<int, float> _morphSpyLastLogTime = new Dictionary<int, float>();
        static readonly Dictionary<TMorph, MorphBaseBakeState> _morphBaseBakeStates = new Dictionary<TMorph, MorphBaseBakeState>();
        static readonly FieldInfo _blendValuesChkField = typeof(TMorph).GetField(
            "BlendValuesCHK",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        static BepInEx.Logging.ManualLogSource _log = BepInEx.Logging.Logger.CreateLogSource("Pregnancy");
        static bool _suppressMorphBaseBake = false;
        const float MorphSpyMinInterval = 0.5f;

        struct LocalFrame
        {
            public Vector3 Center;
            public Vector3 Up;
            public Vector3 Fwd;
            public Vector3 Right;
            public float ScaleSide;
            public float ScaleUp;
            public float ScaleFwd;
            public float ScaleGeneral;
            public TorsoProfile Profile;
            public float BoneLen;
        }

        struct DeformStats
        {
            public int MaskedVerts;
            public int EllipsoidVerts;
            public int NonZeroVerts;
            public float MaxDelta;
            public float MaxStrength;
        }

        static bool _bpWorldCached = false;
        static LocalFrame _bpWorldFrame;
        static Dictionary<string, Matrix4x4> _bpBoneWorld = new Dictionary<string, Matrix4x4>();
        static HashSet<int> _pendingVisibilityApply = new HashSet<int>();
        static MorphTriggerMode CurrentTriggerMode =>
            PregnancyPlugin.CfgMorphTriggerMode != null
                ? PregnancyPlugin.CfgMorphTriggerMode.Value
                : MorphTriggerMode.ManualOnly;

        public static bool IsValid(Maid maid) => maid != null && maid.body0 != null;

        public static bool IsActive(Maid maid) => maid != null && _activeProgress.ContainsKey(maid.GetHashCode());

        public static float GetActiveProgress(Maid maid)
        {
            return maid != null && _activeProgress.TryGetValue(maid.GetHashCode(), out float p) ? p : 0f;
        }

        public static void MarkActive(Maid maid, float progress)
        {
            if (maid == null) return;
            _activeProgress[maid.GetHashCode()] = Mathf.Clamp01(progress);
        }

        public static void ApplyProgress(Maid maid, float progress)
        {
            if (!IsValid(maid)) return;
            _bpWorldCached = false;
            _activeProgress[maid.GetHashCode()] = Mathf.Clamp01(progress);
            EnsureMonitor(maid);
            PruneRecords(maid);

            ApplyToSlots(maid, Mathf.Clamp01(progress), false);
        }

        public static void SetBelly(Maid maid, float sliderValue)
        {
            ApplyProgress(maid, sliderValue / 100f);
        }

        public static void Reset(Maid maid)
        {
            if (maid == null) return;

            ResetInternal(maid);
            _bpWorldCached = false;

            var mon = maid.gameObject.GetComponent<BellyMonitor>();
            if (mon != null) Object.DestroyImmediate(mon);
        }

        public static void ResetToDefaults() { Shape = new VtxSettings(); }

        static void ResetInternal(Maid maid)
        {
            int key = maid.GetHashCode();
            _activeProgress.Remove(key);
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

                bool willApply = includeInactive || smr.gameObject.activeInHierarchy;
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

        static void ForgetRecords(Maid maid)
        {
            if (maid == null) return;
            ReleaseALBindings(maid);
            _alContexts.Remove(maid.GetHashCode());

            int key = maid.GetHashCode();
            if (_records.TryGetValue(key, out var records))
            {
                records.Clear();
                _records.Remove(key);
            }
        }

        static int GetRendererKey(SkinnedMeshRenderer smr)
        {
            return smr != null ? smr.GetInstanceID() : 0;
        }

        static MeshRecord FindRecord(Maid maid, SkinnedMeshRenderer smr)
        {
            if (maid == null || smr == null || smr.sharedMesh == null) return null;
            int key = maid.GetHashCode();
            if (!_records.TryGetValue(key, out var records)) return null;
            return records.Find(r => r != null && r.SMR == smr && r.Mesh == smr.sharedMesh);
        }

        static List<SkinnedMeshRenderer> CollectTargetRenderers(Maid maid)
        {
            List<SkinnedMeshRenderer> result = new List<SkinnedMeshRenderer>();
            HashSet<int> seen = new HashSet<int>();

            for (int si = 0; si < maid.body0.goSlot.Count; si++)
            {
                TBodySkin slot = maid.body0.goSlot[si];
                if (slot?.obj == null) continue;
                AddRenderers(slot.obj.transform, result, seen);
            }

            AddRenderers(maid.transform, result, seen);
            return result;
        }

        static void AddRenderers(Transform root, List<SkinnedMeshRenderer> result, HashSet<int> seen)
        {
            if (root == null) return;

            SkinnedMeshRenderer[] smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer smr in smrs)
            {
                if (smr == null) continue;
                if (seen.Add(smr.GetInstanceID()))
                    result.Add(smr);
            }
        }

        static string GetMeshId(SkinnedMeshRenderer smr)
        {
            if (smr == null) return string.Empty;
            return (smr.name + "/" + smr.gameObject.name).ToLowerInvariant();
        }

        static bool ContainsAny(string value, params string[] patterns)
        {
            foreach (string pattern in patterns)
                if (value.Contains(pattern)) return true;
            return false;
        }

        static bool[] BuildVertexMask(SkinnedMeshRenderer smr, MeshRecord rec, MeshMorphClass meshClass)
        {
            var mask = new bool[rec.OrigVerts.Length];
            for (int i=0; i<mask.Length; i++) mask[i] = meshClass != MeshMorphClass.Ignore;
            return mask;
        }

        static MeshMorphClass ClassifyMesh(SkinnedMeshRenderer smr)
        {
            string id = GetMeshId(smr);
            if (string.IsNullOrEmpty(id)) return MeshMorphClass.Ignore;
            if (IsFaceSlot(id)) return MeshMorphClass.Ignore;
            if (id.Contains("moza")) return MeshMorphClass.Ignore;
            if (id.Contains("accheso")) return MeshMorphClass.NavelAccessory;
            if (ContainsAny(id, "body", "base", "karada", "inmou", "nip", "under")) return MeshMorphClass.Body;
            if (ContainsAny(id, "bra", "pants", "psnts", "stkg", "mizugi", "zurashi")) return MeshMorphClass.InnerCloth;
            if (ContainsAny(id, "wear", "onep", "skrt", "zubon", "skirt", "mekure")) return MeshMorphClass.OuterCloth;

            return MeshMorphClass.Ignore;
        }

        static bool IsSkirtLikeMesh(SkinnedMeshRenderer smr)
        {
            string id = GetMeshId(smr);
            if (string.IsNullOrEmpty(id)) return false;
            return ContainsAny(id, "skrt", "skirt", "onep", "mekure");
        }

        static bool ShouldLogMorphDiagnostics(SkinnedMeshRenderer smr, MeshMorphClass meshClass)
        {
            if (!IsDebugMeshLoggingEnabled()) return false;
            return smr != null && !IsFaceSlot(GetMeshId(smr));
        }

        static bool IsDebugMeshLoggingEnabled()
        {
            return PregnancyPlugin.CfgDebugMeshLogging != null
                && PregnancyPlugin.CfgDebugMeshLogging.Value;
        }

        static void LogMorphScan(Maid maid, SkinnedMeshRenderer smr, MeshMorphClass meshClass, bool includeInactive, bool willApply)
        {
            if (!ShouldLogMorphDiagnostics(smr, meshClass)) return;

            Mesh mesh = smr.sharedMesh;
            _log.LogInfo("[BellyDiag] scan"
                + $" maid={GetMaidName(maid)}"
                + $" id={GetMeshId(smr)}"
                + $" class={meshClass}"
                + $" includeInactive={includeInactive}"
                + $" activeSelf={smr.gameObject.activeSelf}"
                + $" activeInHierarchy={smr.gameObject.activeInHierarchy}"
                + $" enabled={smr.enabled}"
                + $" meshId={(mesh != null ? mesh.GetInstanceID() : 0)}"
                + $" verts={(mesh != null ? mesh.vertexCount : 0)}"
                + $" action={(willApply ? "apply" : "skip-inactive")}");
        }

        static void LogMorphApply(
            Maid maid,
            SkinnedMeshRenderer smr,
            MeshMorphClass meshClass,
            string frameSource,
            bool refreshBase,
            int vertexCount,
            float rawProgress,
            float effectiveProgress,
            DeformStats stats)
        {
            if (!ShouldLogMorphDiagnostics(smr, meshClass)) return;

            _log.LogInfo("[BellyDiag] apply"
                + $" maid={GetMaidName(maid)}"
                + $" id={GetMeshId(smr)}"
                + $" class={meshClass}"
                + $" frame={frameSource}"
                + $" refreshBase={refreshBase}"
                + $" verts={vertexCount}"
                + $" masked={stats.MaskedVerts}"
                + $" ellipsoid={stats.EllipsoidVerts}"
                + $" moved={stats.NonZeroVerts}"
                + $" maxDelta={stats.MaxDelta:F6}"
                + $" maxStrength={stats.MaxStrength:F6}"
                + $" progress={rawProgress:F3}"
                + $" effectiveProgress={effectiveProgress:F3}");
        }

        static void LogMorphSkip(Maid maid, SkinnedMeshRenderer smr, MeshMorphClass meshClass, string reason)
        {
            if (!ShouldLogMorphDiagnostics(smr, meshClass)) return;

            _log.LogInfo("[BellyDiag] skip"
                + $" maid={GetMaidName(maid)}"
                + $" id={GetMeshId(smr)}"
                + $" class={meshClass}"
                + $" reason={reason}");
        }

        static string GetMaidName(Maid maid)
        {
            if (maid == null || maid.status == null) return "?";
            return (maid.status.lastName + maid.status.firstName).Trim();
        }

        static void AttachNotifier(GameObject obj, Maid maid)
        {
            var notifier = obj.GetComponent<VisibilityNotifier>();
            if (notifier == null)
            {
                notifier = obj.AddComponent<VisibilityNotifier>();
            }
            notifier.Configure(maid);
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

        public static void NotifyFixBlendValuesSpy(TMorph morph)
        {
            try
            {
                if (PregnancyPlugin.CfgMorphSpyLogging == null || !PregnancyPlugin.CfgMorphSpyLogging.Value)
                    return;
                if (object.ReferenceEquals(morph, null)) return;

                string caller;
                if (!TryGetAysCaller(out caller)) return;

                Maid maid = null;
                if (!object.ReferenceEquals(morph.bodyskin, null))
                    maid = FindMaidForBodySkin(morph.bodyskin);

                float progress = IsValid(maid)
                    ? (PregnancyManager.GetPregnant(maid)
                        ? PregnancyManager.GetProgress(maid)
                        : GetActiveProgress(maid))
                    : 0f;

                int key = morph.GetHashCode();
                float now = Time.unscaledTime;
                float last;
                if (_morphSpyLastLogTime.TryGetValue(key, out last) && now - last < MorphSpyMinInterval)
                    return;
                _morphSpyLastLogTime[key] = now;

                string slot = "";
                if (!object.ReferenceEquals(morph.bodyskin, null)
                    && !object.ReferenceEquals(morph.bodyskin.obj, null))
                    slot = morph.bodyskin.obj.name;

                int oriVertId = !object.ReferenceEquals(morph.m_vOriVert, null) ? morph.m_vOriVert.GetHashCode() : 0;
                int oriNormId = !object.ReferenceEquals(morph.m_vOriNorm, null) ? morph.m_vOriNorm.GetHashCode() : 0;
                int blendDataCount = !object.ReferenceEquals(morph.BlendDatas, null) ? morph.BlendDatas.Count : 0;

                _log.LogInfo("[MorphSpy] AYS FixBlendValues"
                    + " maid=" + GetMaidName(maid)
                    + " slot=" + slot
                    + " morphId=" + morph.GetHashCode()
                    + " oriVertId=" + oriVertId
                    + " oriNormId=" + oriNormId
                    + " blendDatas=" + blendDataCount
                    + " morphCount=" + morph.MorphCount
                    + " vCount=" + morph.VCount
                    + " progress=" + Mathf.Clamp01(progress).ToString("0.###")
                    + " caller=" + caller);
            }
            catch { }
        }

        static bool TryGetAysCaller(out string caller)
        {
            caller = "";
            try
            {
                var frames = new System.Diagnostics.StackTrace(false).GetFrames();
                if (frames == null) return false;

                for (int i = 0; i < frames.Length; i++)
                {
                    var method = frames[i].GetMethod();
                    if (object.ReferenceEquals(method, null)) continue;
                    var type = method.DeclaringType;
                    string fullName = !object.ReferenceEquals(type, null) ? type.FullName : "";
                    if (fullName.IndexOf("AddYotogiSlider", System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    caller = fullName + "." + method.Name;
                    return true;
                }
            }
            catch { }

            return false;
        }

        public static void NotifyFixBlendValues(TMorph morph)
        {
            if (_suppressMorphBaseBake) return;
            if (object.ReferenceEquals(morph, null) || object.ReferenceEquals(morph.bodyskin, null)) return;

            Maid maid = FindMaidForBodySkin(morph.bodyskin);
            if (!IsValid(maid)) return;

            float progress = PregnancyManager.GetPregnant(maid)
                ? PregnancyManager.GetProgress(maid)
                : GetActiveProgress(maid);
            progress = Mathf.Clamp01(progress);
            if (progress <= 0f)
            {
                RestoreMorphBase(morph, true);
                return;
            }

            TryBakeRuntimeMorphBase(maid, morph, progress);
        }

        static bool TryBakeRuntimeMorphBase(Maid maid, TMorph morph, float progress)
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

            Vector3[] bakedNormals = BuildMorphBaseNormals(mesh, rec, bakedVerts);
            if (bakedNormals == null || bakedNormals.Length != bakedVerts.Length)
                bakedNormals = CloneVectorArray(baseNormals);

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

            ForceFixBlendValues(morph);
            InstallALBinding(maid, rec, bakedVerts, progress);
            LogMorphBaseBake(maid, morph, smr, meshClass, progress, effectiveProgress, oldOriVertId, newState, stats, reusedStoredBase);

            return true;
        }

        static SkinnedMeshRenderer FindRendererForMorph(Maid maid, TMorph morph)
        {
            if (object.ReferenceEquals(morph, null) || morph.m_vOriVert == null) return null;

            int vertexCount = morph.m_vOriVert.Length;
            List<SkinnedMeshRenderer> candidates = new List<SkinnedMeshRenderer>();
            HashSet<int> seen = new HashSet<int>();

            if (morph.bodyskin != null && morph.bodyskin.obj != null)
                AddRenderers(morph.bodyskin.obj.transform, candidates, seen);

            SkinnedMeshRenderer ignoredMatch = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                SkinnedMeshRenderer smr = candidates[i];
                if (smr == null || smr.sharedMesh == null) continue;
                if (smr.sharedMesh.vertexCount != vertexCount) continue;

                if (ClassifyMesh(smr) != MeshMorphClass.Ignore)
                    return smr;
                if (ignoredMatch == null)
                    ignoredMatch = smr;
            }

            if (IsValid(maid))
            {
                List<SkinnedMeshRenderer> all = CollectTargetRenderers(maid);
                for (int i = 0; i < all.Count; i++)
                {
                    SkinnedMeshRenderer smr = all[i];
                    if (smr == null || smr.sharedMesh == null) continue;
                    if (smr.sharedMesh.vertexCount != vertexCount) continue;

                    if (ClassifyMesh(smr) != MeshMorphClass.Ignore)
                        return smr;
                    if (ignoredMatch == null)
                        ignoredMatch = smr;
                }
            }

            return ignoredMatch;
        }

        static bool CacheBindPoseWorldForMaid(Maid maid)
        {
            if (!IsValid(maid)) return false;

            _bpWorldCached = false;
            _bpBoneWorld.Clear();

            List<SkinnedMeshRenderer> renderers = CollectTargetRenderers(maid);
            for (int i = 0; i < renderers.Count; i++)
            {
                SkinnedMeshRenderer smr = renderers[i];
                if (smr == null || smr.sharedMesh == null) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.Body) continue;
                if (TryCacheBindPoseWorldRef(smr)) return true;
            }

            return false;
        }

        static bool HasAnyMaskedVertex(bool[] mask)
        {
            if (mask == null) return false;
            for (int i = 0; i < mask.Length; i++)
                if (mask[i]) return true;
            return false;
        }

        static float RelGeneral(float value)
            => value * (_bpWorldCached ? Mathf.Max(_bpWorldFrame.ScaleGeneral, MinBodyRelativeScale) : 1f);

        static string GetBoneName(Transform[] bones, int index)
        {
            if (bones == null || index < 0 || index >= bones.Length || bones[index] == null)
                return string.Empty;
            return bones[index].name ?? string.Empty;
        }

        static float GetEffectiveMorphProgress(float progress, MeshMorphClass meshClass) => Mathf.Clamp01(progress);

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

        static void ForceBlendValuesDirty(TMorph morph)
        {
            try
            {
                if (object.ReferenceEquals(_blendValuesChkField, null)) return;
                float[] chk = _blendValuesChkField.GetValue(morph) as float[];
                if (chk != null && chk.Length > 0)
                    chk[0] = float.NaN;
            }
            catch
            {
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

        static int ComputeMorphBakeSignature(float progress, MeshMorphClass meshClass)
        {
            unchecked
            {
                int hash = 17 * 31 + (int)meshClass;
                AddBakeSignatureFloat(ref hash, progress);
                AddBakeSignatureFloat(ref hash, Shape.SubtleStageProgress);
                AddBakeSignatureFloat(ref hash, Shape.VisibleStageProgress);
                AddBakeSignatureFloat(ref hash, Shape.MidStageProgress);
                AddBakeSignatureFloat(ref hash, Shape.GrowthFullness);
                AddBakeSignatureFloat(ref hash, Shape.GrowthWidth);
                AddBakeSignatureFloat(ref hash, Shape.UpperReach);
                AddBakeSignatureFloat(ref hash, Shape.LateSettle);
                AddBakeSignatureFloat(ref hash, Shape.VerticalRange);
                AddBakeSignatureFloat(ref hash, Shape.WallSmoothing);
                AddBakeSignatureFloat(ref hash, Shape.SagStrength);
                AddBakeSignatureFloat(ref hash, Shape.MidVolume);
                AddBakeSignatureFloat(ref hash, Shape.LowerPoleLift);
                AddBakeSignatureFloat(ref hash, Shape.LateForwardShift);
                AddBakeSignatureFloat(ref hash, Shape.GrowthAxisTilt);
                AddBakeSignatureFloat(ref hash, Shape.LateHeightScale);
                AddBakeSignatureFloat(ref hash, Shape.LateDepthScale);
                AddBakeSignatureFloat(ref hash, Shape.LateWidthScale);
                AddBakeSignatureFloat(ref hash, Shape.ThighGuardSpeed);
                AddBakeSignatureFloat(ref hash, Shape.InnerThighGuardStrength);
                AddBakeSignatureFloat(ref hash, Shape.ThighGuardSmoothStrength);
                AddBakeSignatureFloat(ref hash, Shape.SkinClearance);
                AddBakeSignatureFloat(ref hash, Shape.VirtualAxisStrength);
                AddBakeSignatureFloat(ref hash, Shape.AxisBlendStart);
                AddBakeSignatureFloat(ref hash, Shape.AxisBlendFull);
                AddBakeSignatureFloat(ref hash, Shape.LowerTransitionStart);
                AddBakeSignatureFloat(ref hash, Shape.LowerTransitionWidth);
                AddBakeSignatureFloat(ref hash, Shape.LowerTransitionBias);
                AddBakeSignatureFloat(ref hash, Shape.UpperTransitionStart);
                AddBakeSignatureFloat(ref hash, Shape.UpperTransitionWidth);
                AddBakeSignatureFloat(ref hash, Shape.UpperTransitionBias);
                AddBakeSignatureFloat(ref hash, Shape.UpperTransitionJoin);
                AddBakeSignatureFloat(ref hash, Shape.UpperTransitionActivation);
                hash = hash * 31 + (Shape.BreastExclusionEnabled ? 1 : 0);
                hash = hash * 31 + (Shape.UpperBoneFilterEnabled ? 1 : 0);
                hash = hash * 31 + (Shape.UpperFieldFadeEnabled ? 1 : 0);
                AddBakeSignatureFloat(ref hash, Shape.AxisPullLow);
                AddBakeSignatureFloat(ref hash, Shape.AxisPullHigh);
                AddBakeSignatureFloat(ref hash, Shape.AxisPullAngle);
                AddBakeSignatureFloat(ref hash, Shape.AxisAnchorY);
                AddBakeSignatureFloat(ref hash, Shape.AxisAnchorZ);
                hash = hash * 31 + (Shape.NavelPreviewFull ? 1 : 0);
                AddBakeSignatureFloat(ref hash, Shape.NavelEversion);
                AddBakeSignatureFloat(ref hash, Shape.NavelStart);
                AddBakeSignatureFloat(ref hash, Shape.NavelHeight);
                AddBakeSignatureFloat(ref hash, Shape.NavelVerticalOffset);
                AddBakeSignatureFloat(ref hash, Shape.NavelRadius);
                AddBakeSignatureFloat(ref hash, Shape.NavelProportion);
                AddBakeSignatureFloat(ref hash, Shape.ClothOffset);
                AddBakeSignatureFloat(ref hash, Shape.ClothDistortThreshold);
                AddBakeSignatureFloat(ref hash, Shape.ClothDistortNeighborDiff);
                return hash;
            }
        }

        static void AddBakeSignatureFloat(ref int hash, float value)
        {
            hash = hash * 31 + value.GetHashCode();
        }

        static void LogMorphBaseBake(
            Maid maid,
            TMorph morph,
            SkinnedMeshRenderer smr,
            MeshMorphClass meshClass,
            float progress,
            float effectiveProgress,
            int oldOriVertId,
            MorphBaseBakeState state,
            DeformStats stats,
            bool reusedStoredBase)
        {
            try
            {
                if (PregnancyPlugin.CfgMorphSpyLogging == null || !PregnancyPlugin.CfgMorphSpyLogging.Value)
                    return;

                string caller = "";
                TryGetAysCaller(out caller);

                string slot = "";
                if (!object.ReferenceEquals(morph, null)
                    && morph.bodyskin != null
                    && morph.bodyskin.obj != null)
                    slot = morph.bodyskin.obj.name;

                int oriVertId = !object.ReferenceEquals(morph, null) && morph.m_vOriVert != null ? morph.m_vOriVert.GetHashCode() : 0;
                int oriNormId = !object.ReferenceEquals(morph, null) && morph.m_vOriNorm != null ? morph.m_vOriNorm.GetHashCode() : 0;
                int morphId = !object.ReferenceEquals(morph, null) ? morph.GetHashCode() : 0;
                int vertexCount = state != null && state.BakedVerts != null ? state.BakedVerts.Length : 0;

                _log.LogInfo("[MorphBake] runtime base baked"
                    + " maid=" + GetMaidName(maid)
                    + " slot=" + slot
                    + " id=" + GetMeshId(smr)
                    + " class=" + meshClass
                    + " morphId=" + morphId
                    + " oldOriVertId=" + oldOriVertId
                    + " oriVertId=" + oriVertId
                    + " oriNormId=" + oriNormId
                    + " originalSig=" + (state != null ? state.OriginalSignature : 0)
                    + " bakedSig=" + (state != null ? state.AppliedSignature : 0)
                    + " shapeSig=" + (state != null ? state.ShapeSignature : 0)
                    + " verts=" + vertexCount
                    + " moved=" + stats.NonZeroVerts
                    + " maxDelta=" + stats.MaxDelta.ToString("0.######")
                    + " progress=" + progress.ToString("0.###")
                    + " effectiveProgress=" + effectiveProgress.ToString("0.###")
                    + " source=" + (reusedStoredBase ? "stored-base" : "new-base")
                    + " caller=" + caller);
            }
            catch
            {
            }
        }

        internal static void FlushMorphDirty(Maid maid)
        {
            if (maid == null) return;
            int key = maid.GetHashCode();
            if (!_morphDirtySkins.TryGetValue(key, out var skins) || skins.Count == 0) return;

            var toProcess = new List<TBodySkin>(skins);
            skins.Clear();

            if (!IsValid(maid)) return;

            float progress = PregnancyManager.GetPregnant(maid)
                ? PregnancyManager.GetProgress(maid)
                : GetActiveProgress(maid);
            progress = Mathf.Clamp01(progress);
            if (progress <= 0f) return;

            _bpWorldCached = false;
            _bpBoneWorld.Clear();
            PruneRecords(maid);

            foreach (SkinnedMeshRenderer smr in CollectTargetRenderers(maid))
            {
                if (smr?.sharedMesh == null) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.Body) continue;
                if (TryCacheBindPoseWorldRef(smr)) break;
            }

            foreach (TBodySkin skin in toProcess)
                ApplyToBodySkin(maid, skin, progress);
        }

        static Maid FindMaidForBodySkin(TBodySkin skin)
        {
            if (skin == null) return null;

            var cm = GameMain.Instance?.CharacterMgr;
            if (object.ReferenceEquals(cm, null)) return null;

            int cnt = cm.GetMaidCount();
            for (int i = 0; i < cnt; i++)
            {
                Maid maid = cm.GetMaid(i);
                if (maid == null || maid.body0 == null || maid.body0.goSlot == null) continue;

                for (int si = 0; si < maid.body0.goSlot.Count; si++)
                {
                    TBodySkin slot = maid.body0.goSlot[si];
                    if (object.ReferenceEquals(slot, skin)) return maid;
                    if (slot != null && object.ReferenceEquals(slot.morph, skin.morph)) return maid;
                }
            }

            return null;
        }

        static void ApplyToBodySkin(Maid maid, TBodySkin skin, float progress)
        {
            if (maid == null || skin == null || skin.obj == null) return;
            PrepareALContext(maid, CollectTargetRenderers(maid), progress);

            List<SkinnedMeshRenderer> targetSmrs = new List<SkinnedMeshRenderer>();
            HashSet<int> seenRenderers = new HashSet<int>();
            AddRenderers(skin.obj.transform, targetSmrs, seenRenderers);

            HashSet<int> appliedMeshIds = new HashSet<int>();
            foreach (SkinnedMeshRenderer smr in targetSmrs)
            {
                if (smr?.sharedMesh == null) continue;

                MeshMorphClass meshClass = ClassifyMesh(smr);
                if (meshClass == MeshMorphClass.Ignore) continue;
                if (!smr.gameObject.activeInHierarchy) continue;

                int meshId = smr.sharedMesh.GetInstanceID();
                if (!appliedMeshIds.Add(meshId)) continue;

                ApplySMR(maid, smr, progress, meshClass, false);
            }
        }

        public static void RequestCurrentMeshRefresh(Maid maid)
        {
            if (maid == null) return;
            EnsureMonitor(maid).TriggerFullRefresh();
        }

        public static void EnsureVisibilityObservers(Maid maid)
        {
            if (!IsValid(maid)) return;
            if (CurrentTriggerMode != MorphTriggerMode.VisibilityChange) return;
            if (!PregnancyManager.GetPregnant(maid)) return;
            if (PregnancyManager.GetProgress(maid) <= 0f) return;

            foreach (SkinnedMeshRenderer smr in CollectRelevantRenderers(maid))
                AttachNotifier(smr.gameObject, maid);
        }

        public static void RequestVisibilityApplyBelly(Maid maid)
        {
            if (!IsValid(maid)) return;
            if (CurrentTriggerMode != MorphTriggerMode.VisibilityChange) return;
            if (PregnancyPlugin.Instance == null) return;

            int key = maid.GetHashCode();
            if (!_pendingVisibilityApply.Add(key)) return;
            EnsureMonitor(maid).RequestVisibilityApply(key);
        }

        internal static bool PatchAysHooks(Harmony harmony)
        {
            if (_aysHooksPatched) return true;
            if (object.ReferenceEquals(harmony, null)) return false;

            bool patched = false;
            string[] typeNames =
            {
                "COM3D2.AddYotogiSliderSE.Plugin.AddYotogiSliderSE",
                "COM3D2.AddYotogiSlider.Plugin.AddYotogiSlider"
            };

            for (int i = 0; i < typeNames.Length; i++)
            {
                System.Type type = AccessTools.TypeByName(typeNames[i]);
                if (object.ReferenceEquals(type, null)) continue;

                MethodInfo initOnStartSkill = AccessTools.Method(type, "initOnStartSkill");
                MethodInfo updateMaidHaraValue = AccessTools.Method(type, "updateMaidHaraValue");
                if (object.ReferenceEquals(initOnStartSkill, null)) continue;

                try
                {
                    harmony.Patch(
                        initOnStartSkill,
                        postfix: new HarmonyMethod(typeof(BellyMorphController), nameof(AysInitOnStartSkillPostfix)));

                    if (!object.ReferenceEquals(updateMaidHaraValue, null))
                    {
                        harmony.Patch(
                            updateMaidHaraValue,
                            postfix: new HarmonyMethod(typeof(BellyMorphController), nameof(AysUpdateMaidHaraValuePostfix)));
                    }

                    patched = true;
                }
                catch (System.Exception e)
                {
                    if (!object.ReferenceEquals(PregnancyPlugin.CfgMorphSpyLogging, null)
                        && PregnancyPlugin.CfgMorphSpyLogging.Value)
                        _log.LogInfo("[AYSHaraZero] patch failed type=" + typeNames[i]
                            + " error=" + e.GetType().Name + ": " + e.Message);
                }
            }

            _aysHooksPatched = patched;
            if (patched
                && !object.ReferenceEquals(PregnancyPlugin.CfgMorphSpyLogging, null)
                && PregnancyPlugin.CfgMorphSpyLogging.Value)
                _log.LogInfo("[AYSHaraZero] patched AYS initOnStartSkill");
            return patched;
        }

        public static void AysInitOnStartSkillPostfix(object __instance)
        {
            if (object.ReferenceEquals(__instance, null)) return;
            TryZeroAysHaraAfterRead(__instance, "initOnStartSkill-postfix");
        }

        public static void AysUpdateMaidHaraValuePostfix(object __instance, float value)
        {
            if (object.ReferenceEquals(__instance, null)) return;
            LogAysHaraWriteSpy(__instance, value);
        }

        static bool TryZeroAysHaraAfterRead(object instance, string reason)
        {
            System.Type type = !object.ReferenceEquals(instance, null) ? instance.GetType() : null;
            if (!IsAysPluginType(type)) return false;

            Maid maid = GetAysMaid(instance, type);
            if (!IsValid(maid)) return false;

            float sliderValue;
            if (!TryGetAysHaraSliderValue(instance, type, out sliderValue))
                return false;

            int sessionKey = ComputeAysSessionKey(instance, type, maid);
            int beforeHara = GetMaidHaraValue(maid);
            int defHara = GetAysIntField(instance, type, "iDefHara");
            bool oldSuppress = _suppressMorphBaseBake;

            try
            {
                _suppressMorphBaseBake = true;
                maid.SetProp("Hara", 0, true);
                if (!object.ReferenceEquals(maid.body0, null))
                    maid.body0.VertexMorph_FromProcItem("hara", 0f);

                LogAysHaraZero(maid, beforeHara, defHara, sliderValue, reason, sessionKey);
                return true;
            }
            catch (System.Exception e)
            {
                LogAysHaraZeroError(maid, reason, e);
                return false;
            }
            finally
            {
                _suppressMorphBaseBake = oldSuppress;
            }
        }

        static int ComputeAysSessionKey(object instance, System.Type type, Maid maid)
        {
            object manager = null;
            try
            {
                FieldInfo managerField = type.GetField(
                    "yotogiPlayManager",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!object.ReferenceEquals(managerField, null))
                    manager = managerField.GetValue(instance);
            }
            catch
            {
            }

            unchecked
            {
                int hash = 17;
                hash = hash * 397 + (!object.ReferenceEquals(maid, null) ? maid.GetHashCode() : 0);
                hash = hash * 397 + (!object.ReferenceEquals(instance, null) ? instance.GetHashCode() : 0);
                hash = hash * 397 + (!object.ReferenceEquals(manager, null) ? manager.GetHashCode() : 0);
                return hash;
            }
        }

        static bool IsAysPluginType(System.Type type)
        {
            if (object.ReferenceEquals(type, null)) return false;
            string fullName = type.FullName ?? "";
            return fullName == "COM3D2.AddYotogiSliderSE.Plugin.AddYotogiSliderSE"
                || fullName == "COM3D2.AddYotogiSlider.Plugin.AddYotogiSlider";
        }

        static Maid GetAysMaid(object instance, System.Type type)
        {
            try
            {
                FieldInfo maidField = type.GetField(
                    "maid",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return !object.ReferenceEquals(maidField, null) ? maidField.GetValue(instance) as Maid : null;
            }
            catch
            {
                return null;
            }
        }

        static bool TryGetAysHaraSliderValue(object instance, System.Type type, out float value)
        {
            value = 0f;
            try
            {
                FieldInfo sliderField = type.GetField(
                    "slider",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (object.ReferenceEquals(sliderField, null)) return false;

                object sliders = sliderField.GetValue(instance);
                System.Collections.IDictionary dict = sliders as System.Collections.IDictionary;
                if (object.ReferenceEquals(dict, null) || !dict.Contains("Hara")) return false;

                object haraSlider = dict["Hara"];
                if (object.ReferenceEquals(haraSlider, null)) return false;

                PropertyInfo valueProp = haraSlider.GetType().GetProperty(
                    "Value",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (object.ReferenceEquals(valueProp, null)) return false;

                value = System.Convert.ToSingle(valueProp.GetValue(haraSlider, null));
                return true;
            }
            catch
            {
                return false;
            }
        }

        static int GetAysIntField(object instance, System.Type type, string name)
        {
            try
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return !object.ReferenceEquals(field, null) ? System.Convert.ToInt32(field.GetValue(instance)) : 0;
            }
            catch
            {
                return 0;
            }
        }

        static int GetMaidHaraValue(Maid maid)
        {
            try
            {
                MaidProp prop = !object.ReferenceEquals(maid, null) ? maid.GetProp("Hara") : null;
                return !object.ReferenceEquals(prop, null) ? prop.value : 0;
            }
            catch
            {
                return 0;
            }
        }

        static void LogAysHaraWriteSpy(object instance, float value)
        {
            try
            {
                if (PregnancyPlugin.CfgMorphSpyLogging == null || !PregnancyPlugin.CfgMorphSpyLogging.Value)
                    return;

                System.Type type = !object.ReferenceEquals(instance, null) ? instance.GetType() : null;
                if (!IsAysPluginType(type)) return;

                Maid maid = GetAysMaid(instance, type);
                int sessionKey = ComputeAysSessionKey(instance, type, maid);
                int count;
                _aysHaraWriteSpyCountBySession.TryGetValue(sessionKey, out count);
                if (count >= 12) return;

                count++;
                _aysHaraWriteSpyCountBySession[sessionKey] = count;

                int currentHara = GetMaidHaraValue(maid);
                int defHara = GetAysIntField(instance, type, "iDefHara");
                int currentAysHara = GetAysIntField(instance, type, "iCurrentHara");

                _log.LogInfo("[AYSHaraWriteSpy]"
                    + " maid=" + GetMaidName(maid)
                    + " value=" + value.ToString("0.###")
                    + " maidHara=" + currentHara
                    + " defHara=" + defHara
                    + " currentAysHara=" + currentAysHara
                    + " session=" + sessionKey
                    + " count=" + count);
            }
            catch
            {
            }
        }

        static void LogAysHaraZero(Maid maid, int beforeHara, int defHara, float sliderValue, string reason, int sessionKey)
        {
            try
            {
                if (PregnancyPlugin.CfgMorphSpyLogging == null || !PregnancyPlugin.CfgMorphSpyLogging.Value)
                    return;

                _log.LogInfo("[AYSHaraZero]"
                    + " maid=" + GetMaidName(maid)
                    + " reason=" + reason
                    + " beforeHara=" + beforeHara
                    + " defHara=" + defHara
                    + " sliderHara=" + sliderValue.ToString("0.###")
                    + " session=" + sessionKey);
            }
            catch
            {
            }
        }

        static void LogAysHaraZeroError(Maid maid, string reason, System.Exception e)
        {
            try
            {
                if (PregnancyPlugin.CfgMorphSpyLogging == null || !PregnancyPlugin.CfgMorphSpyLogging.Value)
                    return;

                _log.LogInfo("[AYSHaraZero]"
                    + " maid=" + GetMaidName(maid)
                    + " reason=" + reason
                    + " error=" + e.GetType().Name + ": " + e.Message);
            }
            catch
            {
            }
        }

        static bool TryCacheBindPoseWorldRef(SkinnedMeshRenderer smr)
        {
            if (!TryBuildBindPoseWorldRef(smr, out LocalFrame frame, out Dictionary<string, Matrix4x4> boneWorld))
                return false;

            _bpWorldFrame = frame;
            _bpBoneWorld = boneWorld;
            _bpWorldCached = true;
            if (IsDebugMeshLoggingEnabled())
                _log.LogInfo($"[Belly] BindPose world ref cached from {smr.name}: center={_bpWorldFrame.Center}");
            return true;
        }

        static bool TryBuildBindPoseWorldRef(
            SkinnedMeshRenderer smr,
            out LocalFrame frame,
            out Dictionary<string, Matrix4x4> boneWorld)
        {
            frame = default(LocalFrame);
            boneWorld = new Dictionary<string, Matrix4x4>();
            if (smr == null || smr.sharedMesh == null) return false;

            Matrix4x4[] bindposes = NativeBinds(smr);
            Transform[] bones = NativeBones(smr);
            if (bones == null || bindposes == null) return false;

            int pelvisIdx = -1;
            int spineIdx = -1;
            int spineaIdx = -1;
            int leftRefIdx = -1;
            int rightRefIdx = -1;

            Matrix4x4 l2w = smr.transform.localToWorldMatrix;
            for (int j = 0; j < bones.Length && j < bindposes.Length; j++)
            {
                if (bones[j] == null) continue;
                string n = bones[j].name;
                if (!boneWorld.ContainsKey(n))
                    boneWorld[n] = l2w * bindposes[j].inverse;

                if (n == "Bip01 Pelvis_SCL_" || n == "Bip01 Pelvis") pelvisIdx = j;
                else if (n == "Bip01 Spine_SCL_" || n == "Bip01 Spine") spineIdx = j;
                else if (n == "Bip01 Spinea_SCL_" || n == "Bip01 Spine0a_SCL_" || n == "Bip01 Spinea") spineaIdx = j;

            }

            leftRefIdx = FindBoneIndex(bones,
                "Bip01 L Thigh_SCL_",
                "Bip01 L Thigh",
                "Hip_L",
                "Hip_L_nub",
                "momotwist_L",
                "momotwist2_L",
                "momoniku_L");
            rightRefIdx = FindBoneIndex(bones,
                "Bip01 R Thigh_SCL_",
                "Bip01 R Thigh",
                "Hip_R",
                "Hip_R_nub",
                "momotwist_R",
                "momotwist2_R",
                "momoniku_R");

            if (pelvisIdx < 0 || spineIdx < 0 || leftRefIdx < 0 || rightRefIdx < 0) return false;

            Vector3 pelvisW = GetMatrixPosition(l2w * bindposes[pelvisIdx].inverse);
            Vector3 spineW = GetMatrixPosition(l2w * bindposes[spineIdx].inverse);
            Vector3 leftRefW = GetMatrixPosition(l2w * bindposes[leftRefIdx].inverse);
            Vector3 rightRefW = GetMatrixPosition(l2w * bindposes[rightRefIdx].inverse);

            Vector3 up = (spineW - pelvisW).normalized;
            Vector3 tv = rightRefW - leftRefW;
            Vector3 rawRight = (tv - up * Vector3.Dot(tv, up)).normalized;
            Vector3 rawFwd = Vector3.Cross(rawRight, up).normalized;
            Vector3 fwd = smr.transform.forward;
            fwd = fwd - up * Vector3.Dot(fwd, up);
            if (fwd.sqrMagnitude < 1e-6f)
                fwd = rawFwd;
            else
                fwd = fwd.normalized;

            if (Vector3.Dot(fwd, rawFwd) < 0f)
                fwd = -fwd;

            Vector3 right = Vector3.Cross(up, fwd).normalized;
            if (right.sqrMagnitude < 1e-6f)
                right = rawRight;
            if (Vector3.Dot(right, rawRight) < 0f)
                right = -right;
            fwd = Vector3.Cross(right, up).normalized;

            float t = 1f;
            Vector3 worldBase;
            if (t <= 1f)
            {
                worldBase = Vector3.Lerp(pelvisW, spineW, t);
            }
            else
            {
                float step = Vector3.Distance(pelvisW, spineW);
                if (spineaIdx >= 0)
                {
                    Vector3 spineaW = GetMatrixPosition(l2w * bindposes[spineaIdx].inverse);
                    worldBase = Vector3.Lerp(spineW, spineaW, t - 1f);
                }
                else
                {
                    worldBase = spineW + up * ((t - 1f) * step);
                }
            }

            Vector3 lateralMid = (leftRefW + rightRefW) * 0.5f;
            worldBase += right * Vector3.Dot(lateralMid - worldBase, right);

            frame.Up = up;
            frame.Fwd = fwd;
            frame.Right = right;
            float upLen = Vector3.Distance(pelvisW, spineW);
            float sideLen = Vector3.Distance(leftRefW, rightRefW);
            frame.ScaleUp = Mathf.Clamp(upLen / ReferenceBodyUp, MinBodyRelativeScale, MaxBodyRelativeScale);
            frame.ScaleSide = Mathf.Clamp(sideLen / ReferenceBodySide, MinBodyRelativeScale, MaxBodyRelativeScale);
            frame.ScaleFwd = Mathf.Clamp((frame.ScaleUp + frame.ScaleSide) * 0.5f, MinBodyRelativeScale, MaxBodyRelativeScale);
            frame.ScaleGeneral = Mathf.Clamp((frame.ScaleUp + frame.ScaleSide + frame.ScaleFwd) / 3f, MinBodyRelativeScale, MaxBodyRelativeScale);
            frame.Center = worldBase;
            return true;
        }

        static int FindBoneIndex(Transform[] bones, params string[] names)
        {
            if (bones == null || names == null) return -1;
            for (int n = 0; n < names.Length; n++)
            {
                string target = names[n];
                for (int i = 0; i < bones.Length; i++)
                {
                    Transform bone = bones[i];
                    if (bone != null && bone.name == target)
                        return i;
                }
            }
            return -1;
        }

        static Vector3 GetMatrixPosition(Matrix4x4 matrix)
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
        }

        static void ApplySMR(Maid maid, SkinnedMeshRenderer smr, float progress, MeshMorphClass meshClass, bool refreshBase)
        {
            Mesh mesh = smr.sharedMesh;
            int key = maid.GetHashCode();
            if (!_records.ContainsKey(key)) _records[key] = new List<MeshRecord>();
            var records = _records[key];

            records.RemoveAll(r => r.SMR == smr && r.Mesh != mesh);
            MeshRecord rec = records.Find(r => r.SMR == smr && r.Mesh == mesh);
            Vector3[] currentVerts = mesh.vertices;
            Vector3[] currentNormals = mesh.normals;
            if (rec == null)
            {
                rec = new MeshRecord
                {
                    SMR = smr,
                    Mesh = mesh,
                    OrigVerts = (Vector3[])currentVerts.Clone(),
                    OrigNormals = (Vector3[])currentNormals.Clone(),
                };
                records.Add(rec);
            }

            // Guard: if current mesh already has our deformation applied, keep the stored
            // clean base rather than overwriting it with the deformed state.
            bool alreadyApplied = !refreshBase
                && rec.LastDeltaVerts != null
                && rec.AppliedSignature != 0
                && currentVerts.Length == rec.LastDeltaVerts.Length
                && ComputeVertexSignature(currentVerts) == rec.AppliedSignature;

            if (!alreadyApplied)
            {
                rec.OrigVerts = (Vector3[])CleanVertices(maid, smr).Clone();
                rec.OrigNormals = (Vector3[])currentNormals.Clone();
            }
            if (refreshBase)
            {
                rec.LastDeltaVerts = null;

                rec.AppliedSignature = 0;
            }

            bool[] mask = BuildVertexMask(smr, rec, meshClass);
            int mc = 0;
            for (int i = 0; i < mask.Length; i++)
                if (mask[i]) mc++;

            if (mc == 0)
            {
                LogMorphSkip(maid, smr, meshClass, "empty-mask");
                return;
            }

            float effectiveProgress = Mathf.Clamp01(progress);

            Vector3[] newVerts;
            DeformStats stats;
            if (!TryDeformVertsInBindPoseWorld(
                smr,
                rec,
                mask,
                meshClass,
                effectiveProgress,
                out newVerts,
                out stats))
            {
                LogMorphSkip(maid, smr, meshClass, "bindpose-skin-unavailable");
                return;
            }

            Vector3[] deltaVerts = BuildDeltaVerts(rec.OrigVerts, newVerts);
            // CleanVertices retains current game morphs and removes only our known
            // baked delta. Evaluate once from that base, even after TMorph rewrites it.
            Vector3[] appliedVerts = newVerts;

            rec.LastDeltaVerts = deltaVerts;
            rec.AppliedSignature = ComputeVertexSignature(appliedVerts);
            mesh.vertices = appliedVerts;
            ApplySmoothedNormals(mesh, rec, appliedVerts);
            mesh.RecalculateBounds();
            InstallALBinding(maid, rec, appliedVerts, progress);

            if (ShouldLogMorphDiagnostics(smr, meshClass))
            {
                string frameSource = stats.EllipsoidVerts > 0 ? "bindpose-skin" : "bindpose-skin-zero";
                LogMorphApply(
                    maid,
                    smr,
                    meshClass,
                    frameSource,
                    refreshBase,
                    rec.OrigVerts.Length,
                    progress,
                    effectiveProgress,
                    stats);
            }
        }

        static bool TryDeformVertsInBindPoseWorld(SkinnedMeshRenderer smr, MeshRecord rec, bool[] mask,
            MeshMorphClass meshClass, float progress, out Vector3[] newVerts, out DeformStats stats)
            => TryDeformAL(smr, rec, meshClass, progress, out newVerts, out stats);

        static Vector3[] BuildDeltaVerts(Vector3[] baseVerts, Vector3[] newVerts)
        {
            if (baseVerts == null || newVerts == null || baseVerts.Length != newVerts.Length)
                return null;

            Vector3[] delta = new Vector3[baseVerts.Length];
            for (int i = 0; i < delta.Length; i++)
                delta[i] = newVerts[i] - baseVerts[i];
            return delta;
        }

        public static string ExportAllVertexMorphDump(Maid maid)
        {
            if (maid == null) throw new System.ArgumentNullException("maid");
            string directory = Path.Combine(BepInEx.Paths.PluginPath, "PregnancyDumps");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "ALGrowth-" + SafeDumpName(GetMaidName(maid)) + "-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writer.WriteLine("mesh,index,original_x,original_y,original_z,deformed_x,deformed_y,deformed_z");
                if (_records.TryGetValue(maid.GetHashCode(), out var records))
                    foreach (var r in records)
                        if (r.Mesh != null && r.OrigVerts != null && r.LastNewV != null)
                            for (int i=0;i<r.OrigVerts.Length;i++)
                            {
                                bool first=true;
                                WriteCell(writer,ref first,r.Mesh.name); WriteCell(writer,ref first,i.ToString());
                                WriteVectorCells(writer,ref first,r.OrigVerts[i]); WriteVectorCells(writer,ref first,r.LastNewV[i]);
                                writer.WriteLine();
                            }
            }
            return path;
        }

        static string SafeDumpName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "maid";

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool bad = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j])
                    {
                        bad = true;
                        break;
                    }
                }
                sb.Append(bad ? '_' : c);
            }
            return sb.Length > 0 ? sb.ToString() : "maid";
        }

        static string F(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        static void WriteVectorCells(TextWriter writer, ref bool first, Vector3 v)
        {
            WriteCell(writer, ref first, F(v.x));
            WriteCell(writer, ref first, F(v.y));
            WriteCell(writer, ref first, F(v.z));
        }

        static string FormatBoneCell(string boneName, int boneIndex)
        {
            if (string.IsNullOrEmpty(boneName))
                return boneIndex.ToString(CultureInfo.InvariantCulture);
            return boneName + "#" + boneIndex.ToString(CultureInfo.InvariantCulture);
        }

        static void WriteCell(TextWriter writer, ref bool first, string value)
        {
            if (!first) writer.Write('\t');
            writer.Write(EscapeDumpCell(value));
            first = false;
        }

        static string EscapeDumpCell(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        static float BreastBoneWeight(BoneWeight weight, Transform[] bones)
        {
            float total = 0f;
            AddBreastBoneWeight(ref total, bones, weight.boneIndex0, weight.weight0);
            AddBreastBoneWeight(ref total, bones, weight.boneIndex1, weight.weight1);
            AddBreastBoneWeight(ref total, bones, weight.boneIndex2, weight.weight2);
            AddBreastBoneWeight(ref total, bones, weight.boneIndex3, weight.weight3);
            return Mathf.Clamp01(total);
        }

        static void AddBreastBoneWeight(ref float total, Transform[] bones, int index, float weight)
        {
            if (weight <= 0f || bones == null || index < 0 || index >= bones.Length) return;
            Transform bone = bones[index];
            if (bone == null || !IsBreastBoneName(bone.name)) return;
            total += weight;
        }

        static bool IsBreastBoneName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLowerInvariant();
            return lower.Contains("mune")
                || lower.Contains("breast")
                || lower.Contains("bust")
                || lower.Contains("chichi")
                || lower.Contains("chikubi")
                || lower.Contains("nipple");
        }

        const int SkirtDrapePropagatePasses = 48;
        const float SkirtDrapePropagationDecay = 0.995f;
        const float SkirtDrapeDeltaBoost = 1.03f;
        const int SkirtDrapeSmoothPasses = 4;
        const float SkirtDrapeSmoothStrength = 0.35f;

        static List<int>[] BuildMeshNeighbors(Mesh mesh, int count)
        {
            List<int>[] neighbors = new List<int>[count];
            for (int i = 0; i < count; i++)
                neighbors[i] = new List<int>();

            int[] triangles = null;
            try
            {
                triangles = mesh.triangles;
            }
            catch
            {
                return null;
            }

            if (triangles == null)
                return null;

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= count || b >= count || c >= count) continue;
                AddNeighbor(neighbors, a, b);
                AddNeighbor(neighbors, b, a);
                AddNeighbor(neighbors, a, c);
                AddNeighbor(neighbors, c, a);
                AddNeighbor(neighbors, b, c);
                AddNeighbor(neighbors, c, b);
            }

            return neighbors;
        }

        static void AddNeighbor(List<int>[] neighbors, int index, int neighbor)
        {
            if (!neighbors[index].Contains(neighbor))
                neighbors[index].Add(neighbor);
        }

        static void ApplySmoothedNormals(Mesh mesh, MeshRecord rec, Vector3[] newVerts)
        {
            if(rec?.Skirt!=null)newVerts=rec.Skirt.VisualVertices;
            if (mesh == null || rec == null || rec.OrigVerts == null || rec.OrigNormals == null || newVerts == null)
            {
                if (mesh != null) mesh.RecalculateNormals();
                return;
            }

            if (rec.OrigVerts.Length != newVerts.Length || rec.OrigNormals.Length != newVerts.Length)
            {
                mesh.RecalculateNormals();
                return;
            }

            mesh.normals = DeformationNormals.Build(rec.OrigVerts,newVerts,rec.OrigNormals,mesh.triangles);
        }

        static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        static int ComputeMeshSignature(SkinnedMeshRenderer smr)
        {
            Mesh mesh = smr != null ? smr.sharedMesh : null;
            if (mesh == null) return 0;

            Vector3[] verts = mesh.vertices;
            return ComputeVertexSignature(verts);
        }

        static int ComputeVertexSignature(Vector3[] verts)
        {
            if (verts == null) return 0;

            unchecked
            {
                int count = verts.Length;
                int hash = 17;
                hash = hash * 31 + count;

                if (count == 0) return hash;

                int samples = Mathf.Min(12, count);
                for (int i = 0; i < samples; i++)
                {
                    int idx = samples == 1 ? 0 : (int)((long)i * (count - 1) / (samples - 1));
                    Vector3 v = verts[idx];
                    hash = hash * 31 + Mathf.RoundToInt(v.x * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(v.y * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(v.z * 1000f);
                }

                return hash;
            }
        }

        public static int GetCurrentMeshSignature(Maid maid)
        {
            if (maid == null) return 0;

            unchecked
            {
                int hash = 17;
                foreach (SkinnedMeshRenderer smr in CollectRelevantRenderers(maid))
                {
                    hash = hash * 31 + GetRendererKey(smr);
                    hash = hash * 31 + ComputeMeshSignature(smr);
                }
                return hash;
            }
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

        public class VisibilityNotifier : MonoBehaviour
        {
            Maid _maid;
            SkinnedMeshRenderer _smr;
            bool _initialized;
            bool _lastRendererEnabled;
            bool _lastActiveSelf;
            bool _lastActiveHierarchy;
            int _lastMeshInstanceId;

            public void Configure(Maid maid)
            {
                _maid = maid;
                if (_smr == null) _smr = GetComponent<SkinnedMeshRenderer>();
                CaptureState();
                _initialized = true;
            }

            void OnEnable()
            {
                if (!_initialized) return;
                CaptureState();
                RequestVisibilityApplyBelly(_maid);
            }

            void OnDisable()
            {
                if (!_initialized) return;
                RequestVisibilityApplyBelly(_maid);
            }

            void LateUpdate()
            {
                if (!_initialized) return;
                if (_smr == null) _smr = GetComponent<SkinnedMeshRenderer>();
                if (_smr == null) return;

                bool rendererEnabled = _smr.enabled;
                bool activeSelf = _smr.gameObject.activeSelf;
                bool activeHierarchy = _smr.gameObject.activeInHierarchy;
                int meshInstanceId = _smr.sharedMesh != null ? _smr.sharedMesh.GetInstanceID() : 0;

                bool changed =
                    rendererEnabled != _lastRendererEnabled
                    || activeSelf != _lastActiveSelf
                    || activeHierarchy != _lastActiveHierarchy
                    || meshInstanceId != _lastMeshInstanceId;

                _lastRendererEnabled = rendererEnabled;
                _lastActiveSelf = activeSelf;
                _lastActiveHierarchy = activeHierarchy;
                _lastMeshInstanceId = meshInstanceId;

                if (changed)
                    RequestVisibilityApplyBelly(_maid);
            }

            void CaptureState()
            {
                if (_smr == null) _smr = GetComponent<SkinnedMeshRenderer>();
                if (_smr == null) return;

                _lastRendererEnabled = _smr.enabled;
                _lastActiveSelf = _smr.gameObject.activeSelf;
                _lastActiveHierarchy = _smr.gameObject.activeInHierarchy;
                _lastMeshInstanceId = _smr.sharedMesh != null ? _smr.sharedMesh.GetInstanceID() : 0;
            }
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
                ForgetRecords(_maid);
                PruneRecords(_maid);

                float pFull = GetActiveProgress(_maid);
                if (pFull > 0f)
                    ApplyToSlots(_maid, pFull, false);
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

                PregnancyUI.TriggerApplyBelly(_maid, progress);
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
            }
        }
    }
}

