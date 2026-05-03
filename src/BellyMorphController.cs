using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public static class BellyMorphController
    {
        public const int HaraMax = 100;
        const int AutoMorphStableFrames = 2;

        const float BaseRadiusSide = 0.22f;
        const float BaseRadiusFront = 0.22f;
        const float BaseRadiusBack = 0.22f;
        const float BaseRadiusUp = 0.21f;
        const float BaseRadiusDown = 0.21f;
        const float BasePushOut = 1.0f;
        const float NormalAffectedDelta = 0.0002f;
        const int NormalAffectedExpandPasses = 4;

        public static float SpineLerpT = 1.0f;
        public static float OffsetSide = 0.0f;
        public static float InflationMultiplier = 0.0f;
        public static float InflationMoveY = 0.025f;
        public static float InflationMoveZ = 0.0f;
        public static float InflationStretchX = -0.2f;
        public static float InflationStretchY = 0.0f;
        public static float InflationStretchZ = 0.13f;
        public static float InflationShiftY = 0.04f;
        public static float InflationShiftZ = -0.3f;
        public static float InflationTaperY = -0.03f;
        public static float InflationTaperZ = -0.05f;
        public static float InflationRoundness = 0.03f;
        public static float InflationDrop = 0.2f;
        public static float InflationFatFold = 0.0f;
        public static float InflationFatFoldHeight = 0.0f;
        public static float InflationFatFoldGap = 0.0f;
        public static float RegionRadiusSide = BaseRadiusSide;
        public static float RegionRadiusFront = BaseRadiusFront;
        public static float RegionRadiusBack = 0.13f;
        public static float RegionRadiusUp = 0.26f;
        public static float RegionRadiusDown = 0.18f;
        public static float ThighGuardSpeed = 4.0f;
        public static float InnerThighGuardStrength = 1.0f;
        public static float TopEdgeTaper = -1.0f;
        public static float BottomEdgeTaper = 0.0f;
        public static float SideSmoothWidth = 0.8f;
        public static float SideSmoothStrength = 1.4f;
        public static float BreastGuardStrength = 1.0f;
        public static float ClothOverdrive = 1.03f;
        public static float OuterClothOverdrive = 1.20f;
        public static float OuterClothPregnancyScale = 1.0f;
        public static bool OuterClothSkirtDrape = false;
        public static float OuterClothLayerGuard = 0.0f;
        public static float InnerClothOffset = 0.0f;
        public static float OuterClothOffset = 0.006f;
        public static float ClothThicknessPreserve = 3.0f;
        public static float ClothOffsetSideRatio = 0.0f;
        public static float ClothBackOffsetBoost = 0.0f;
        public static float ClothDepthStretch = 4.0f;
        public static float OuterClothLowerFrontGuard = 0.0f;
        public static int   ClothDeformSmoothPasses    = 8;
        public static float ClothDeformSmoothStrength  = 0.6f;
        public static float ClothDeformSmoothThreshold = 60f;
        public static int   ClothDeformSmoothRings     = 2;

        const float BellyEdgeBlend = 0.35f;

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

        class MeshRecord
        {
            public SkinnedMeshRenderer SMR;
            public Mesh Mesh;
            public Vector3[] OrigVerts;
            public Vector3[] OrigNormals;
            public Vector3[] LastDeltaVerts;
            public int AppliedSignature;
            public int[][] Neighbors; // cached adjacency list, built lazily
        }

        class CrossLayerGuardMesh
        {
            public SkinnedMeshRenderer SMR;
            public MeshRecord Record;
            public Mesh Mesh;
            public Vector3[] CurrentVerts;
            public BoneWeight[] Weights;
            public Matrix4x4[] BoneMatrices;
            public Vector3[] OriginalWorld;
            public Vector3[] MorphedWorld;
            public bool[] Valid;
            public LayerGuardCoord[] Coords;
            public bool Changed;
        }

        struct LayerGuardRef
        {
            public int MeshIndex;
            public int VertexIndex;

            public LayerGuardRef(int meshIndex, int vertexIndex)
            {
                MeshIndex = meshIndex;
                VertexIndex = vertexIndex;
            }
        }

        enum MeshMorphClass
        {
            Ignore,
            Body,
            InnerCloth,
            OuterCloth,
        }

        static Dictionary<int, List<MeshRecord>> _records = new Dictionary<int, List<MeshRecord>>();
        static readonly Dictionary<int, HashSet<TBodySkin>> _morphDirtySkins = new Dictionary<int, HashSet<TBodySkin>>();
        static BepInEx.Logging.ManualLogSource _log = BepInEx.Logging.Logger.CreateLogSource("Pregnancy");

        struct LocalFrame
        {
            public Vector3 Center;
            public Vector3 Up;
            public Vector3 Fwd;
            public Vector3 Right;
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

        public static void ResetToDefaults()
        {
            InflationMultiplier       = 0.0f;
            InflationMoveY            = 0.025f;
            InflationMoveZ            = 0.0f;
            InflationStretchX         = -0.2f;
            InflationStretchY         = 0.0f;
            InflationStretchZ         = 0.13f;
            InflationShiftY           = 0.04f;
            InflationShiftZ           = -0.3f;
            InflationTaperY           = -0.03f;
            InflationTaperZ           = -0.05f;
            InflationRoundness        = 0.03f;
            InflationDrop             = 0.2f;
            InflationFatFold          = 0.0f;
            InflationFatFoldHeight    = 0.0f;
            InflationFatFoldGap       = 0.0f;
            RegionRadiusSide          = BaseRadiusSide;
            RegionRadiusFront         = BaseRadiusFront;
            RegionRadiusBack          = 0.13f;
            RegionRadiusUp            = 0.26f;
            RegionRadiusDown          = 0.18f;
            ThighGuardSpeed           = 4.0f;
            InnerThighGuardStrength   = 1.0f;
            TopEdgeTaper              = -1.0f;
            BottomEdgeTaper           = 0.0f;
            SideSmoothWidth           = 0.8f;
            SideSmoothStrength        = 1.4f;
            BreastGuardStrength       = 1.0f;
            OuterClothPregnancyScale  = 1.0f;
            OuterClothSkirtDrape      = false;
            OuterClothLayerGuard      = 0.0f;
            InnerClothOffset          = 0.0f;
            OuterClothOffset          = 0.006f;
            ClothThicknessPreserve    = 3.0f;
            ClothOffsetSideRatio      = 0.0f;
            ClothBackOffsetBoost      = 0.0f;
            ClothDepthStretch         = 4.0f;
            OuterClothLowerFrontGuard = 0.0f;
            ClothDeformSmoothPasses    = 8;
            ClothDeformSmoothStrength  = 0.6f;
            ClothDeformSmoothThreshold = 60f;
            ClothDeformSmoothRings     = 2;
        }

        static void ResetInternal(Maid maid)
        {
            int key = maid.GetHashCode();
            _activeProgress.Remove(key);
            _morphDirtySkins.Remove(key);

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

            _bpWorldCached = false;
            _bpBoneWorld.Clear();
            foreach (SkinnedMeshRenderer smr in targetSmrs)
            {
                if (smr?.sharedMesh == null) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.Body) continue;
                if (TryCacheBindPoseWorldRef(smr)) break;
            }

            HashSet<int> appliedMeshIds = new HashSet<int>();
            List<SkinnedMeshRenderer> skirtLayerGuardCandidates = new List<SkinnedMeshRenderer>();
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
                    if (meshClass == MeshMorphClass.OuterCloth)
                        skirtLayerGuardCandidates.Add(smr);
                }
            }

            if (OuterClothLayerGuard > 0f && skirtLayerGuardCandidates.Count > 0)
                ApplyCrossOuterClothLayerGuard(maid, skirtLayerGuardCandidates);
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
            int count = rec.OrigVerts.Length;
            bool[] mask = new bool[count];

            if (meshClass == MeshMorphClass.Ignore)
                return mask;

            for (int i = 0; i < count; i++)
                mask[i] = true;

            return mask;
        }

        // Skirts: skirt / skrt / mekure (lifted-skirt layers)
        // One-piece dresses: onep
        // Excludes: wear (jackets/tops), zubon (trousers)
        static bool IsSkirtOrDressMesh(SkinnedMeshRenderer smr)
        {
            string id = GetMeshId(smr);
            return ContainsAny(id, "skirt", "skrt", "onep", "mekure");
        }

        static MeshMorphClass ClassifyMesh(SkinnedMeshRenderer smr)
        {
            string id = GetMeshId(smr);
            if (string.IsNullOrEmpty(id)) return MeshMorphClass.Ignore;
            if (IsFaceSlot(id)) return MeshMorphClass.Ignore;
            if (id.Contains("moza")) return MeshMorphClass.Ignore;
            if (ContainsAny(id, "body", "base", "karada", "inmou", "nip", "under")) return MeshMorphClass.Body;
            if (ContainsAny(id, "bra", "pants", "psnts", "stkg", "mizugi", "zurashi")) return MeshMorphClass.InnerCloth;
            if (ContainsAny(id, "wear", "onep", "skrt", "zubon", "skirt", "mekure")) return MeshMorphClass.OuterCloth;

            return MeshMorphClass.Ignore;
        }

        static bool ShouldLogMorphDiagnostics(SkinnedMeshRenderer smr, MeshMorphClass meshClass)
        {
            if (!IsDebugMeshLoggingEnabled()) return false;
            if (smr == null || meshClass == MeshMorphClass.Ignore) return false;
            string id = GetMeshId(smr);
            return meshClass == MeshMorphClass.OuterCloth
                || id.Contains("218")
                || id.Contains("skrt")
                || id.Contains("skirt");
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

        public static void NotifyFixBlendValues(TMorph morph)
        {
            if (morph == null || morph.bodyskin == null) return;

            Maid maid = FindMaidForBodySkin(morph.bodyskin);
            if (!IsValid(maid)) return;

            float progress = PregnancyManager.GetPregnant(maid)
                ? PregnancyManager.GetProgress(maid)
                : GetActiveProgress(maid);
            if (Mathf.Clamp01(progress) <= 0f) return;

            int key = maid.GetHashCode();
            if (!_morphDirtySkins.ContainsKey(key))
                _morphDirtySkins[key] = new HashSet<TBodySkin>();
            _morphDirtySkins[key].Add(morph.bodyskin);
            EnsureMonitor(maid);
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
            PregnancyPlugin.Instance.StartCoroutine(VisibilityApplyBellyCoroutine(maid, key));
        }

        static System.Collections.IEnumerator VisibilityApplyBellyCoroutine(Maid maid, int key)
        {
            try
            {
                while (maid != null && (maid.body0 == null || !maid.body0.isLoadedBody))
                    yield return null;

                if (maid == null) yield break;

                int stableFrames = 0;
                int previousSignature = 0;
                bool hasPrevious = false;

                while (maid != null && stableFrames < AutoMorphStableFrames)
                {
                    yield return new WaitForEndOfFrame();
                    yield return null;

                    while (maid != null && (maid.body0 == null || !maid.body0.isLoadedBody))
                        yield return null;

                    if (maid == null) yield break;

                    int currentSignature = ComputeVisibilitySignature(CollectRelevantRenderers(maid));
                    if (hasPrevious && currentSignature == previousSignature)
                        stableFrames++;
                    else
                        stableFrames = 0;

                    previousSignature = currentSignature;
                    hasPrevious = true;
                }

                if (maid == null) yield break;
                if (CurrentTriggerMode != MorphTriggerMode.VisibilityChange) yield break;
                if (!PregnancyManager.GetPregnant(maid)) yield break;

                float progress = PregnancyManager.GetProgress(maid);
                if (progress <= 0f) yield break;

                PregnancyUI.TriggerApplyBelly(maid, progress);
            }
            finally
            {
                _pendingVisibilityApply.Remove(key);
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

            Matrix4x4[] bindposes = smr.sharedMesh.bindposes;
            Transform[] bones = smr.bones;
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

            float t = SpineLerpT;
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

            frame.Center = worldBase + right * OffsetSide;
            frame.Up = up;
            frame.Fwd = fwd;
            frame.Right = right;
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

        static bool IsLeftLateralRefBone(string name)
        {
            return name == "Bip01 L Thigh_SCL_"
                || name == "Bip01 L Thigh"
                || name == "Hip_L"
                || name == "Hip_L_nub"
                || name == "momotwist_L"
                || name == "momotwist2_L"
                || name == "momoniku_L";
        }

        static bool IsRightLateralRefBone(string name)
        {
            return name == "Bip01 R Thigh_SCL_"
                || name == "Bip01 R Thigh"
                || name == "Hip_R"
                || name == "Hip_R_nub"
                || name == "momotwist_R"
                || name == "momotwist2_R"
                || name == "momoniku_R";
        }

        static bool TryBuildBindPoseSkinMatrices(SkinnedMeshRenderer smr, out Matrix4x4[] boneMatrices)
        {
            boneMatrices = null;
            if (!_bpWorldCached) return false;
            if (smr == null || smr.sharedMesh == null) return false;

            Matrix4x4[] bindposes = smr.sharedMesh.bindposes;
            Transform[] bones = smr.bones;
            if (bones == null || bindposes == null || bones.Length == 0 || bindposes.Length == 0) return false;

            int count = Mathf.Min(bones.Length, bindposes.Length);
            boneMatrices = new Matrix4x4[count];
            Matrix4x4 meshToBodyOffset = Matrix4x4.identity;
            bool hasOffset = TryGetMeshToBodyBindPoseOffset(smr, out meshToBodyOffset);
            bool hasAnyUsableBone = false;

            Matrix4x4 smrL2W = smr.transform.localToWorldMatrix;
            for (int i = 0; i < count; i++)
            {
                Transform bone = bones[i];
                if (bone == null)
                {
                    boneMatrices[i] = smrL2W;
                    continue;
                }

                Matrix4x4 unifiedBoneWorld;
                if (_bpBoneWorld.TryGetValue(bone.name, out unifiedBoneWorld))
                {
                    hasAnyUsableBone = true;
                }
                else
                {
                    Matrix4x4 meshBoneWorld = smrL2W * bindposes[i].inverse;
                    unifiedBoneWorld = hasOffset ? meshToBodyOffset * meshBoneWorld : meshBoneWorld;
                }

                boneMatrices[i] = unifiedBoneWorld * bindposes[i];
            }

            return hasAnyUsableBone || hasOffset;
        }

        static bool TryGetMeshToBodyBindPoseOffset(SkinnedMeshRenderer smr, out Matrix4x4 offset)
        {
            offset = Matrix4x4.identity;
            if (smr == null || smr.sharedMesh == null) return false;

            Matrix4x4[] bindposes = smr.sharedMesh.bindposes;
            Transform[] bones = smr.bones;
            if (bones == null || bindposes == null) return false;

            Matrix4x4 smrL2W = smr.transform.localToWorldMatrix;
            for (int i = 0; i < bones.Length && i < bindposes.Length; i++)
            {
                Transform bone = bones[i];
                if (bone == null) continue;

                Matrix4x4 bodyBoneWorld;
                if (!_bpBoneWorld.TryGetValue(bone.name, out bodyBoneWorld)) continue;

                Matrix4x4 meshBoneWorld = smrL2W * bindposes[i].inverse;
                offset = bodyBoneWorld * meshBoneWorld.inverse;
                return true;
            }

            return false;
        }

        static Matrix4x4 GetWeightedSkinMatrix(Matrix4x4[] boneMatrices, BoneWeight weight)
        {
            Matrix4x4 result = new Matrix4x4();
            AddWeightedMatrix(ref result, boneMatrices, weight.boneIndex0, weight.weight0);
            AddWeightedMatrix(ref result, boneMatrices, weight.boneIndex1, weight.weight1);
            AddWeightedMatrix(ref result, boneMatrices, weight.boneIndex2, weight.weight2);
            AddWeightedMatrix(ref result, boneMatrices, weight.boneIndex3, weight.weight3);
            return result;
        }

        static void AddWeightedMatrix(ref Matrix4x4 result, Matrix4x4[] matrices, int index, float weight)
        {
            if (weight <= 0f || matrices == null || index < 0 || index >= matrices.Length) return;

            Matrix4x4 m = matrices[index];
            result.m00 += m.m00 * weight; result.m01 += m.m01 * weight; result.m02 += m.m02 * weight; result.m03 += m.m03 * weight;
            result.m10 += m.m10 * weight; result.m11 += m.m11 * weight; result.m12 += m.m12 * weight; result.m13 += m.m13 * weight;
            result.m20 += m.m20 * weight; result.m21 += m.m21 * weight; result.m22 += m.m22 * weight; result.m23 += m.m23 * weight;
            result.m30 += m.m30 * weight; result.m31 += m.m31 * weight; result.m32 += m.m32 * weight; result.m33 += m.m33 * weight;
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
                rec.OrigVerts = (Vector3[])currentVerts.Clone();
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

            float effectiveProgress = progress;
            if (meshClass == MeshMorphClass.InnerCloth)
            {
                effectiveProgress = progress * ClothOverdrive;
            }
            else if (meshClass == MeshMorphClass.OuterCloth)
            {
                effectiveProgress = progress * OuterClothPregnancyScale;
            }

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

            if (ClothDeformSmoothPasses > 0 && ClothDeformSmoothStrength > 0f &&
                meshClass == MeshMorphClass.OuterCloth &&
                IsSkirtOrDressMesh(smr))
            {
                Vector3 localUp = smr.transform.InverseTransformDirection(_bpWorldFrame.Up);
                SmoothDisplacementDeltas(rec, mesh, newVerts, localUp);
            }

            Vector3[] deltaVerts = BuildDeltaVerts(rec.OrigVerts, newVerts);
            // When our delta is already in the mesh (alreadyApplied), newVerts is the correct
            // target directly (computed from the clean OrigVerts base).  Otherwise accumulate
            // onto whatever the game left in currentVerts (e.g. after FixBlendValues body-shape
            // update) so shape-slider changes are not discarded.
            Vector3[] appliedVerts = alreadyApplied
                ? newVerts
                : (AddDeltaVerts(currentVerts, deltaVerts) ?? newVerts);

            rec.LastDeltaVerts = deltaVerts;
            rec.AppliedSignature = ComputeVertexSignature(appliedVerts);
            mesh.vertices = appliedVerts;
            ApplySmoothedNormals(mesh, rec, appliedVerts);
            mesh.RecalculateBounds();

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

        static bool TryDeformVertsInBindPoseWorld(
            SkinnedMeshRenderer smr,
            MeshRecord rec,
            bool[] mask,
            MeshMorphClass meshClass,
            float progress,
            out Vector3[] newVerts,
            out DeformStats stats)
        {
            newVerts = null;
            stats = new DeformStats();
            if (!_bpWorldCached) return false;
            if (smr == null || smr.sharedMesh == null || rec == null || rec.OrigVerts == null) return false;

            bool trackStats = IsDebugMeshLoggingEnabled();

            BoneWeight[] weights = smr.sharedMesh.boneWeights;
            if (weights == null || weights.Length != rec.OrigVerts.Length) return false;
            Transform[] bones = smr.bones;

            Matrix4x4[] boneMatrices;
            if (!TryBuildBindPoseSkinMatrices(smr, out boneMatrices)) return false;

            float radiusScale = Mathf.Max(0.05f, 1f + InflationMultiplier);
            float radiusSide = Mathf.Max(RegionRadiusSide * radiusScale, 0.0001f);
            float radiusFront = Mathf.Max(RegionRadiusFront * radiusScale, 0.0001f);
            float radiusBack = Mathf.Max(RegionRadiusBack * radiusScale, 0.0001f);
            float radiusUp = Mathf.Max(RegionRadiusUp * radiusScale, 0.0001f);
            float radiusDown = Mathf.Max(RegionRadiusDown * radiusScale, 0.0001f);
            Vector3 worldCenter = _bpWorldFrame.Center;
            Vector3 worldFwd = _bpWorldFrame.Fwd;
            Vector3 worldUp = _bpWorldFrame.Up;
            Vector3 worldRight = _bpWorldFrame.Right;
            if (worldRight.sqrMagnitude < 1e-8f)
                worldRight = Vector3.Cross(worldUp, worldFwd).normalized;
            if (worldRight.sqrMagnitude < 1e-8f) worldRight = Vector3.right;
            Vector3 regionCenter = worldCenter + worldUp * InflationMoveY + worldFwd * InflationMoveZ;
            bool useSkirtDrape = meshClass == MeshMorphClass.OuterCloth && OuterClothSkirtDrape;
            bool trackOuterClothWorld = useSkirtDrape;
            Vector3[] clothOriginalWorld = trackOuterClothWorld ? new Vector3[rec.OrigVerts.Length] : null;
            Vector3[] clothMorphedWorld = trackOuterClothWorld ? new Vector3[rec.OrigVerts.Length] : null;
            bool[] clothValid = trackOuterClothWorld ? new bool[rec.OrigVerts.Length] : null;

            newVerts = new Vector3[rec.OrigVerts.Length];
            for (int i = 0; i < rec.OrigVerts.Length; i++)
            {
                Vector3 vert = rec.OrigVerts[i];
                newVerts[i] = vert;

                if (mask != null && (i >= mask.Length || !mask[i])) continue;
                if (trackStats) stats.MaskedVerts++;

                BoneWeight weight = weights[i];
                if (!HasValidBoneWeight(weight, boneMatrices)) continue;

                Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weight);
                Vector3 worldVert = skin.MultiplyPoint3x4(vert);
                if (trackOuterClothWorld)
                {
                    clothOriginalWorld[i] = worldVert;
                    clothMorphedWorld[i] = worldVert;
                    clothValid[i] = true;
                }

                Vector3 regionDelta = worldVert - regionCenter;
                float upDot = Vector3.Dot(regionDelta, worldUp);
                float sideDot = Vector3.Dot(regionDelta, worldRight);
                float fwdDot = Vector3.Dot(regionDelta, worldFwd);
                float fwdRadius = fwdDot >= 0f ? radiusFront : radiusBack;
                float upRadius = upDot >= 0f ? radiusUp : radiusDown;
                float ellip = (sideDot / radiusSide) * (sideDot / radiusSide)
                    + (fwdDot / fwdRadius) * (fwdDot / fwdRadius)
                    + (upDot / upRadius) * (upDot / upRadius);
                if (ellip >= 1f) continue;

                if (trackStats) stats.EllipsoidVerts++;

                float edgeRatio = Mathf.Sqrt(ellip);
                float edgeFade = 1f - BellyEdgeCurve(edgeRatio);
                float topBottomStrength = GetTopBottomEdgeStrength(
                    upDot,
                    radiusUp,
                    radiusDown);
                float shapeWeight = Mathf.Clamp01(progress * edgeFade * topBottomStrength * BasePushOut);

                float dist = regionDelta.magnitude;
                if (dist < 1e-6f)
                {
                    regionDelta = worldFwd;
                    dist = 1f;
                }

                float directionalRadius = dist / Mathf.Max(Mathf.Sqrt(ellip), 0.0001f);
                float morphWeight = shapeWeight;
                if (morphWeight <= 0f) continue;

                if (trackStats && morphWeight > stats.MaxStrength) stats.MaxStrength = morphWeight;

                Vector3 sphereTarget = regionCenter + regionDelta.normalized * directionalRadius;
                Vector3 newWorldVert = Vector3.Lerp(worldVert, sphereTarget, morphWeight);
                newWorldVert = SculptBaseShapeWorld(
                    worldVert,
                    newWorldVert,
                    regionCenter,
                    worldRight,
                    worldUp,
                    worldFwd,
                    radiusSide,
                    radiusUp,
                    radiusDown);

                if (InflationShiftY != 0f)
                {
                    float upLimit = upDot >= 0f ? radiusUp : radiusDown;
                    float centerYFade = Mathf.Clamp01(1f - Mathf.Abs(upDot) / Mathf.Max(upLimit * 1.8f, 0.0001f));
                    float sideLimit = Mathf.Clamp01(1f - Mathf.Abs(sideDot) / Mathf.Max(radiusSide * 3f, 0.0001f));
                    newWorldVert += worldUp * (InflationShiftY * centerYFade * sideLimit * morphWeight);
                }

                if (InflationShiftZ != 0f)
                {
                    float frontFade = Mathf.Clamp01(fwdDot / Mathf.Max(radiusFront * 2f, 0.0001f));
                    newWorldVert += worldFwd * (InflationShiftZ * frontFade * morphWeight);
                }

                float sx = Mathf.Max(0.05f, 1f + InflationStretchX);
                float sy = Mathf.Max(0.05f, 1f + InflationStretchY);
                float sz = Mathf.Max(0.05f, 1f + InflationStretchZ);
                if (sx != 1f || sy != 1f || sz != 1f)
                {
                    Vector3 rel = newWorldVert - regionCenter;
                    float relSide = Vector3.Dot(rel, worldRight) * sx;
                    float relUp = Vector3.Dot(rel, worldUp) * sy;
                    float relFwd = Vector3.Dot(rel, worldFwd) * sz;
                    newWorldVert = regionCenter + worldRight * relSide + worldUp * relUp + worldFwd * relFwd;
                }

                if (InflationRoundness != 0f)
                {
                    Vector3 rel = newWorldVert - regionCenter;
                    float relFwd = Vector3.Dot(rel, worldFwd);
                    float roundFade = Mathf.Clamp01(relFwd / Mathf.Max(radiusFront, 0.0001f));
                    Vector3 roundCenter = regionCenter + worldFwd * (radiusFront / 3f);
                    Vector3 roundDir = newWorldVert - roundCenter;
                    if (roundDir.sqrMagnitude < 1e-6f) roundDir = worldFwd;
                    newWorldVert += roundDir.normalized * (InflationRoundness * roundFade * edgeFade * progress);
                }

                if (InflationTaperY != 0f)
                {
                    Vector3 rel = newWorldVert - regionCenter;
                    float relUp = Vector3.Dot(rel, worldUp);
                    float relSide = Vector3.Dot(rel, worldRight);
                    float relUpRadius = relUp >= 0f ? radiusUp : radiusDown;
                    float taper = InflationTaperY
                        * Mathf.Clamp01(Mathf.Abs(relUp) / Mathf.Max(relUpRadius, 0.0001f))
                        * Mathf.Clamp01(Mathf.Abs(relSide) / Mathf.Max(radiusSide, 0.0001f))
                        * morphWeight;
                    if (relSide < 0f) taper = -taper;
                    if (relUp < 0f) taper = -taper;
                    newWorldVert += worldRight * taper;
                }

                if (InflationTaperZ != 0f)
                {
                    Vector3 rel = newWorldVert - regionCenter;
                    float relUp = Vector3.Dot(rel, worldUp);
                    float relFwd = Vector3.Dot(rel, worldFwd);
                    float relUpRadius = relUp >= 0f ? radiusUp : radiusDown;
                    float taper = InflationTaperZ
                        * Mathf.Clamp01(Mathf.Abs(relUp) / Mathf.Max(relUpRadius, 0.0001f))
                        * Mathf.Clamp01((relFwd + radiusBack) / Mathf.Max(radiusFront + radiusBack, 0.0001f))
                        * morphWeight;
                    if (relUp < 0f) taper = -taper;
                    newWorldVert += worldFwd * taper;
                }

                if (InflationFatFold > 0f)
                {
                    Vector3 rel = newWorldVert - regionCenter;
                    float relUp = Vector3.Dot(rel, worldUp);
                    float relSide = Vector3.Dot(rel, worldRight);
                    float foldCenter = InflationFatFoldHeight * radiusUp;
                    float foldDist = Mathf.Abs(relUp - foldCenter);
                    float foldFade = 1f - BellyGapCurve(foldDist / Mathf.Max(radiusUp, 0.0001f));
                    if (foldFade > 0f)
                    {
                        float foldPull = Mathf.Clamp01(InflationFatFold * foldFade);
                        newWorldVert = Vector3.Lerp(newWorldVert, worldVert, foldPull);

                        if (InflationFatFoldGap != 0f)
                        {
                            float sideLimit = Mathf.Clamp01(1f - Mathf.Abs(relSide) / Mathf.Max(radiusSide, 0.0001f));
                            float gapDir = relUp >= foldCenter ? 1f : -1f;
                            newWorldVert += worldUp * (InflationFatFoldGap * gapDir * radiusUp * 0.35f * foldFade * sideLimit * progress);
                        }
                    }
                }

                if (InflationDrop > 0f)
                {
                    Vector3 rel = newWorldVert - regionCenter;
                    float frontFade = Mathf.Clamp01(Vector3.Dot(rel, worldFwd) / Mathf.Max(radiusFront * 1.5f, 0.0001f));
                    newWorldVert -= worldUp * (radiusFront * InflationDrop * frontFade * morphWeight);
                }

                newWorldVert = RoundToSidesWorld(
                    worldVert,
                    newWorldVert,
                    regionCenter,
                    worldFwd,
                    radiusBack,
                    radiusFront);

                newWorldVert = ReduceRibStretchingWorld(
                    worldVert,
                    newWorldVert,
                    regionCenter,
                    worldUp,
                    worldFwd,
                    radiusUp);

                float thighRestore = LowerBodyRestoreMask(
                    upDot,
                    sideDot,
                    fwdDot,
                    radiusDown,
                    radiusSide,
                    radiusBack,
                    radiusFront);
                if (thighRestore > 0f)
                    newWorldVert = Vector3.Lerp(newWorldVert, worldVert, thighRestore);

                newWorldVert = ApplyClothDepthStretchWorld(
                    meshClass,
                    worldVert,
                    newWorldVert,
                    directionalRadius,
                    dist,
                    upDot,
                    radiusDown);

                newWorldVert = ApplyClothLayerOffsetWorld(
                    meshClass,
                    worldVert,
                    newWorldVert,
                    regionCenter,
                    morphWeight,
                    directionalRadius,
                    dist,
                    edgeRatio,
                    upDot,
                    radiusDown,
                    worldRight,
                    worldUp,
                    worldFwd);

                Vector3 originalRel = worldVert - regionCenter;
                Vector3 finalRel = newWorldVert - regionCenter;
                float originalCoreSide = Vector3.Dot(originalRel, worldRight);
                float originalCoreFwd = Vector3.Dot(originalRel, worldFwd);
                float finalCoreSide = Vector3.Dot(finalRel, worldRight);
                float finalCoreFwd = Vector3.Dot(finalRel, worldFwd);
                float originalCoreDist = Mathf.Sqrt(originalCoreSide * originalCoreSide + originalCoreFwd * originalCoreFwd);
                float finalCoreDist = Mathf.Sqrt(finalCoreSide * finalCoreSide + finalCoreFwd * finalCoreFwd);
                if (finalCoreDist < originalCoreDist)
                {
                    float finalUp = Vector3.Dot(finalRel, worldUp);
                    newWorldVert = regionCenter + worldRight * originalCoreSide + worldUp * finalUp + worldFwd * originalCoreFwd;
                    finalRel = newWorldVert - regionCenter;
                    finalCoreFwd = originalCoreFwd;
                }

                float breastRestore = BreastRestoreMask(weight, bones);
                if (breastRestore <= 0f)
                    breastRestore = OuterClothBreastRestoreMask(
                        meshClass,
                        upDot,
                        fwdDot,
                        radiusUp,
                        radiusBack,
                        radiusFront);
                if (breastRestore > 0f)
                    newWorldVert = Vector3.Lerp(newWorldVert, worldVert, breastRestore);

                float innerThighRestore = InnerThighRestoreMask(weight, bones);
                if (innerThighRestore > 0f)
                    newWorldVert = Vector3.Lerp(newWorldVert, worldVert, innerThighRestore);

                float armRestore = ArmRestoreMask(weight, bones);
                if (armRestore > 0f)
                    newWorldVert = Vector3.Lerp(newWorldVert, worldVert, armRestore);

                float outerClothLowerFront = OuterClothLowerFrontRestoreMask(
                    meshClass, upDot, edgeRatio, radiusDown);
                if (outerClothLowerFront > 0f)
                    newWorldVert = Vector3.Lerp(newWorldVert, worldVert, outerClothLowerFront);

                if (trackOuterClothWorld)
                    clothMorphedWorld[i] = newWorldVert;

                Vector3 newLocalVert = skin.inverse.MultiplyPoint3x4(newWorldVert);
                if (trackStats)
                {
                    float moved = (newLocalVert - vert).magnitude;
                    if (moved > 0.00001f)
                    {
                        stats.NonZeroVerts++;
                        if (moved > stats.MaxDelta) stats.MaxDelta = moved;
                    }
                }

                newVerts[i] = newLocalVert;
            }

            if (useSkirtDrape)
            {
                ApplyOuterClothSkirtDrapeToMesh(
                    smr.sharedMesh,
                    rec.OrigVerts,
                    weights,
                    boneMatrices,
                    clothOriginalWorld,
                    clothMorphedWorld,
                    clothValid,
                    newVerts,
                    regionCenter,
                    worldRight,
                    worldUp,
                    worldFwd,
                    radiusSide,
                    radiusDown);
            }

            return true;
        }

        static Vector3[] BuildDeltaVerts(Vector3[] baseVerts, Vector3[] newVerts)
        {
            if (baseVerts == null || newVerts == null || baseVerts.Length != newVerts.Length)
                return null;

            Vector3[] delta = new Vector3[baseVerts.Length];
            for (int i = 0; i < delta.Length; i++)
                delta[i] = newVerts[i] - baseVerts[i];
            return delta;
        }

        static Vector3[] AddDeltaVerts(Vector3[] currentVerts, Vector3[] deltaVerts)
        {
            if (currentVerts == null || deltaVerts == null || currentVerts.Length != deltaVerts.Length)
                return null;

            Vector3[] result = new Vector3[currentVerts.Length];
            for (int i = 0; i < currentVerts.Length; i++)
                result[i] = currentVerts[i] + deltaVerts[i];
            return result;
        }

        // Builds an adjacency list (shared-edge neighbors) from mesh triangles.
        // Returns int[][] where [i] is an array of neighbor vertex indices for vertex i.
        static int[][] BuildNeighborLists(int vertCount, int[] triangles)
        {
            // Use List<int> to collect, then convert to arrays for fast iteration.
            var lists = new List<int>[vertCount];
            for (int i = 0; i < vertCount; i++)
                lists[i] = new List<int>(6);

            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (a < 0 || a >= vertCount || b < 0 || b >= vertCount || c < 0 || c >= vertCount)
                    continue;
                if (!lists[a].Contains(b)) lists[a].Add(b);
                if (!lists[a].Contains(c)) lists[a].Add(c);
                if (!lists[b].Contains(a)) lists[b].Add(a);
                if (!lists[b].Contains(c)) lists[b].Add(c);
                if (!lists[c].Contains(a)) lists[c].Add(a);
                if (!lists[c].Contains(b)) lists[c].Add(b);
            }

            int[][] result = new int[vertCount][];
            for (int i = 0; i < vertCount; i++)
                result[i] = lists[i].ToArray();
            return result;
        }

        // Laplacian smoothing of per-vertex displacements within a single mesh.
        // Modifies newVerts in place: newVerts[i] = origVerts[i] + smoothedDelta[i].
        // Seeds eligible set from all vertices with non-zero plugin deformation, then expands
        // outward by ClothDeformSmoothRings rings.  Within eligible set, only vertices whose
        // max pairwise delta difference exceeds ClothDeformSmoothThreshold are smoothed.
        // Neighbor list is cached in rec.Neighbors and rebuilt only when mesh changes.
        static void SmoothDisplacementDeltas(MeshRecord rec, Mesh mesh, Vector3[] newVerts, Vector3 localUp)
        {
            int count = rec.OrigVerts.Length;
            if (newVerts == null || newVerts.Length != count) return;

            // Rebuild neighbor cache when vertex count changes (e.g. outfit swap).
            if (rec.Neighbors == null || rec.Neighbors.Length != count)
            {
                int[] tris = mesh.triangles;
                rec.Neighbors = BuildNeighborLists(count, tris ?? new int[0]);
            }

            int[][] neighbors = rec.Neighbors;
            int   passes    = ClothDeformSmoothPasses;
            float strength  = Mathf.Clamp01(ClothDeformSmoothStrength);
            // User-facing value is scaled by 1e-4 internally so that setting 1
            // equals what used to require 0.0001, giving 10000x finer UI control.
            float threshold = Mathf.Max(ClothDeformSmoothThreshold, 0f) * 1e-4f;

            // Working delta arrays — we ping-pong to avoid read/write conflicts.
            Vector3[] deltas  = new Vector3[count];
            Vector3[] scratch = new Vector3[count];

            for (int i = 0; i < count; i++)
                deltas[i] = newVerts[i] - rec.OrigVerts[i];

            // Seed (center points): vertices with a downward deformation component.
            // Downward filter and threshold only apply to these seed vertices.
            // Ring-expanded neighbors bypass both checks and are always smoothed.
            bool[] isSeed   = new bool[count];
            bool[] eligible = new bool[count];
            for (int i = 0; i < count; i++)
            {
                bool seed = Vector3.Dot(deltas[i], localUp) < 0f;
                isSeed[i]   = seed;
                eligible[i] = seed;
            }

            // Expand eligible set outward by exactly ClothDeformSmoothRings rings.
            // Use a two-buffer approach so each pass adds exactly one true ring
            // (in-place modification would cause a single pass to flood multiple rings
            // depending on vertex ordering).
            int rings = Mathf.Max(ClothDeformSmoothRings, 0);
            bool[] pending = new bool[count];
            for (int ring = 0; ring < rings; ring++)
            {
                bool expanded = false;
                for (int i = 0; i < count; i++)
                {
                    if (eligible[i]) continue;
                    int[] nbrs = neighbors[i];
                    if (nbrs == null) continue;
                    for (int n = 0; n < nbrs.Length; n++)
                    {
                        if (eligible[nbrs[n]]) { pending[i] = true; expanded = true; break; }
                    }
                }
                if (!expanded) break;
                for (int i = 0; i < count; i++)
                {
                    if (pending[i]) { eligible[i] = true; pending[i] = false; }
                }
            }

            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    if (!eligible[i])
                    {
                        scratch[i] = deltas[i];
                        continue;
                    }

                    int[] nbrs = neighbors[i];
                    if (nbrs == null || nbrs.Length == 0)
                    {
                        scratch[i] = deltas[i];
                        continue;
                    }

                    // Accumulate neighbour average and max pairwise diff simultaneously.
                    float maxPairDiff = 0f;
                    float ax = 0f, ay = 0f, az = 0f;
                    for (int n = 0; n < nbrs.Length; n++)
                    {
                        float ddx = deltas[i].x - deltas[nbrs[n]].x;
                        float ddy = deltas[i].y - deltas[nbrs[n]].y;
                        float ddz = deltas[i].z - deltas[nbrs[n]].z;
                        float pairSqr = ddx * ddx + ddy * ddy + ddz * ddz;
                        if (pairSqr > maxPairDiff) maxPairDiff = pairSqr;
                        ax += deltas[nbrs[n]].x;
                        ay += deltas[nbrs[n]].y;
                        az += deltas[nbrs[n]].z;
                    }
                    maxPairDiff = Mathf.Sqrt(maxPairDiff);

                    // Seed (center) points must exceed threshold to be smoothed.
                    // Ring-expanded points bypass the threshold and are always smoothed.
                    float t;
                    if (isSeed[i])
                    {
                        if (maxPairDiff <= threshold) { scratch[i] = deltas[i]; continue; }
                        float excessRatio = (maxPairDiff - threshold) / maxPairDiff;
                        t = excessRatio * strength;
                    }
                    else
                    {
                        t = strength;
                    }

                    // Pull delta[i] toward the neighbour average.
                    float invN = 1f / nbrs.Length;
                    float avgX = ax * invN, avgY = ay * invN, avgZ = az * invN;
                    float dx = deltas[i].x - avgX;
                    float dy = deltas[i].y - avgY;
                    float dz = deltas[i].z - avgZ;
                    scratch[i] = new Vector3(
                        deltas[i].x - dx * t,
                        deltas[i].y - dy * t,
                        deltas[i].z - dz * t);
                }

                // Swap ping-pong buffers.
                Vector3[] tmp = deltas;
                deltas  = scratch;
                scratch = tmp;
            }

            // Write smoothed result back.
            for (int i = 0; i < count; i++)
                newVerts[i] = rec.OrigVerts[i] + deltas[i];
        }

        static Vector3 SculptBaseShapeWorld(
            Vector3 original,
            Vector3 smoothed,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            float radiusSide,
            float radiusUp,
            float radiusDown)
        {
            Vector3 originalRel = original - center;
            Vector3 smoothedRel = smoothed - center;

            float originalSide = Vector3.Dot(originalRel, right);
            float originalUp = Vector3.Dot(originalRel, up);
            float smoothedSide = Vector3.Dot(smoothedRel, right);
            float smoothedUp = Vector3.Dot(smoothedRel, up);
            float smoothedFwd = Vector3.Dot(smoothedRel, fwd);

            float averageVerticalRadius = (radiusUp + radiusDown) * 0.5f;
            float smoothRadius = Mathf.Max((radiusSide + averageVerticalRadius) * 0.5f, 0.0001f);
            float sideUpDist = Mathf.Sqrt(smoothedSide * smoothedSide + smoothedUp * smoothedUp);
            float restore = Mathf.Clamp01(sideUpDist / (smoothRadius * 10f));

            float limitedSide = Mathf.Lerp(smoothedSide, originalSide, restore);
            float limitedUp = Mathf.Lerp(smoothedUp, originalUp, restore);
            return center + right * limitedSide + up * limitedUp + fwd * smoothedFwd;
        }

        static float GetTopBottomEdgeStrength(
            float upDot,
            float radiusUp,
            float radiusDown)
        {
            if (upDot > 0f && TopEdgeTaper != 0f)
                return EdgeTaperStrength(TopEdgeTaper, upDot / Mathf.Max(radiusUp, 0.0001f));
            if (upDot < 0f && BottomEdgeTaper != 0f)
                return EdgeTaperStrength(BottomEdgeTaper, -upDot / Mathf.Max(radiusDown, 0.0001f));

            return 1f;
        }

        static float EdgeTaperStrength(float taper, float edgeRatio)
        {
            float t = Mathf.Clamp01(edgeRatio);
            if (taper < 0f)
            {
                float oldLinearFade = 1f - t;
                return Mathf.Lerp(1f, oldLinearFade, Mathf.Clamp01(-taper));
            }

            return 1f + taper * t;
        }

        static float LowerBodyRestoreMask(
            float upDot,
            float sideDot,
            float fwdDot,
            float radiusDown,
            float radiusSide,
            float radiusBack,
            float radiusFront)
        {
            if (upDot >= 0f) return 0f;

            float lowerRatio = -upDot / Mathf.Max(radiusDown, 0.0001f);
            float speed = Mathf.Max(ThighGuardSpeed, 0.05f);
            float lower = AccelerateGuard(Smooth01(lowerRatio), speed);
            float sideRatio = Mathf.Abs(sideDot) / Mathf.Max(radiusSide, 0.0001f);
            float side = AccelerateGuard(Smooth01((sideRatio - 0.18f) / 0.62f), speed);
            float front = Smooth01((fwdDot + radiusBack) / Mathf.Max(radiusFront + radiusBack, 0.0001f));
            float frontKeep = Mathf.Lerp(1f, 0.35f, front);
            return Mathf.Clamp01(lower * side * frontKeep);
        }

        static float AccelerateGuard(float value, float speed)
        {
            float t = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - t, speed);
        }

        static float BreastRestoreMask(BoneWeight weight, Transform[] bones)
        {
            float strength = Mathf.Max(BreastGuardStrength, 0f);
            if (strength <= 0f) return 0f;

            float breastWeight = BreastBoneWeight(weight, bones);
            if (breastWeight <= 0f) return 0f;

            return Mathf.Clamp01(breastWeight * 4f * strength);
        }

        static float OuterClothBreastRestoreMask(
            MeshMorphClass meshClass,
            float upDot,
            float fwdDot,
            float radiusUp,
            float radiusBack,
            float radiusFront)
        {
            if (meshClass == MeshMorphClass.Body || meshClass == MeshMorphClass.Ignore) return 0f;

            float strength = Mathf.Max(BreastGuardStrength, 0f);
            if (strength <= 0f) return 0f;

            float upper = Smooth01((upDot / Mathf.Max(radiusUp, 0.0001f) - 0.48f) / 0.40f);
            if (upper <= 0f) return 0f;

            float frontRatio = (fwdDot + radiusBack) / Mathf.Max(radiusFront + radiusBack, 0.0001f);
            float front = Smooth01((frontRatio - 0.35f) / 0.65f);
            return Mathf.Clamp01(upper * front * strength);
        }

        // Prevents isolated spike vertices at the lower hem of skirts/dresses.
        // The spike forms when a vertex near the ellipsoid boundary in the lower region
        // gets displaced while its mesh neighbors are outside and stay put.
        // Guard is gated on edgeRatio (proximity to ellipsoid boundary) so only those
        // boundary vertices are suppressed; vertices deep inside the ellipsoid (the
        // main belly area of the skirt) are unaffected and deform normally.
        static float OuterClothLowerFrontRestoreMask(
            MeshMorphClass meshClass,
            float upDot,
            float edgeRatio,
            float radiusDown)
        {
            if (meshClass != MeshMorphClass.OuterCloth) return 0f;

            float strength = Mathf.Max(OuterClothLowerFrontGuard, 0f);
            if (strength <= 0f || upDot >= 0f) return 0f;

            // Lower region of the belly ellipsoid
            float lower = Smooth01(-upDot / Mathf.Max(radiusDown, 0.0001f));
            if (lower <= 0f) return 0f;

            // Near the ellipsoid boundary (outer 40%): this is where isolated
            // spike vertices live. Deep-inside vertices (edgeRatio < 0.6) are
            // not guarded so the skirt still follows the belly shape normally.
            float edge = Smooth01((edgeRatio - 0.60f) / 0.40f);
            if (edge <= 0f) return 0f;

            return Mathf.Clamp01(lower * edge * strength);
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

        static float InnerThighRestoreMask(BoneWeight weight, Transform[] bones)
        {
            float strength = Mathf.Max(InnerThighGuardStrength, 0f);
            if (strength <= 0f) return 0f;

            float thighWeight = 0f;
            AddInnerThighBoneWeight(ref thighWeight, bones, weight.boneIndex0, weight.weight0);
            AddInnerThighBoneWeight(ref thighWeight, bones, weight.boneIndex1, weight.weight1);
            AddInnerThighBoneWeight(ref thighWeight, bones, weight.boneIndex2, weight.weight2);
            AddInnerThighBoneWeight(ref thighWeight, bones, weight.boneIndex3, weight.weight3);
            thighWeight = Mathf.Clamp01(thighWeight);
            if (thighWeight <= 0f) return 0f;

            return Mathf.Clamp01(thighWeight * 4f * strength);
        }

        static void AddInnerThighBoneWeight(ref float total, Transform[] bones, int index, float weight)
        {
            if (weight <= 0f || bones == null || index < 0 || index >= bones.Length) return;
            Transform bone = bones[index];
            if (bone == null || !IsInnerThighBoneName(bone.name)) return;
            total += weight;
        }

        static bool IsInnerThighBoneName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLowerInvariant();
            return lower.Contains("momoniku")
                || lower.Contains("momotwist");
        }

        static float ArmRestoreMask(BoneWeight weight, Transform[] bones)
        {
            float armWeight = 0f;
            AddArmBoneWeight(ref armWeight, bones, weight.boneIndex0, weight.weight0);
            AddArmBoneWeight(ref armWeight, bones, weight.boneIndex1, weight.weight1);
            AddArmBoneWeight(ref armWeight, bones, weight.boneIndex2, weight.weight2);
            AddArmBoneWeight(ref armWeight, bones, weight.boneIndex3, weight.weight3);
            armWeight = Mathf.Clamp01(armWeight);
            if (armWeight <= 0f) return 0f;

            return Mathf.Clamp01(armWeight * 4f);
        }

        static void AddArmBoneWeight(ref float total, Transform[] bones, int index, float weight)
        {
            if (weight <= 0f || bones == null || index < 0 || index >= bones.Length) return;
            Transform bone = bones[index];
            if (bone == null || !IsArmBoneName(bone.name)) return;
            total += weight;
        }

        static bool IsArmBoneName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLowerInvariant();
            return lower.Contains("upperarm")
                || lower.Contains("forearm")
                || lower.Contains("clavicle")
                || lower.Contains("腕")
                || lower.Contains("ude");
        }

        const int SkirtDrapePropagatePasses = 48;
        const float SkirtDrapePropagationDecay = 0.995f;
        const float SkirtDrapeDeltaBoost = 1.03f;
        const int SkirtDrapeSmoothPasses = 4;
        const float SkirtDrapeSmoothStrength = 0.35f;

        static void ApplyOuterClothSkirtDrapeToMesh(
            Mesh mesh,
            Vector3[] localOriginalVerts,
            BoneWeight[] weights,
            Matrix4x4[] boneMatrices,
            Vector3[] originalWorldVerts,
            Vector3[] morphedWorldVerts,
            bool[] valid,
            Vector3[] newLocalVerts,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            float radiusSide,
            float radiusDown)
        {
            if (mesh == null || localOriginalVerts == null || weights == null || boneMatrices == null)
                return;
            if (originalWorldVerts == null || morphedWorldVerts == null || valid == null || newLocalVerts == null)
                return;

            int count = newLocalVerts.Length;
            if (localOriginalVerts.Length != count || weights.Length != count
                || originalWorldVerts.Length != count || morphedWorldVerts.Length != count || valid.Length != count)
                return;

            List<int>[] neighbors = BuildMeshNeighbors(mesh, count);
            if (neighbors == null)
                return;

            Vector2[] propagatedDelta = new Vector2[count];
            bool[] reached = new bool[count];

            for (int i = 0; i < count; i++)
            {
                if (!valid[i]) continue;

                Vector3 originalRel = originalWorldVerts[i] - center;
                Vector3 deltaWorld = morphedWorldVerts[i] - originalWorldVerts[i];
                float originalUp = Vector3.Dot(originalRel, up);
                float originalFwd = Vector3.Dot(originalRel, fwd);
                if (originalUp > 0f || originalFwd <= 0f) continue;
                if (originalUp < -radiusDown * 0.70f) continue;

                Vector2 delta = new Vector2(
                    Vector3.Dot(deltaWorld, right),
                    Vector3.Dot(deltaWorld, fwd));
                if (delta.y <= 0f || delta.sqrMagnitude <= 1e-8f) continue;

                propagatedDelta[i] = delta;
                reached[i] = true;
            }

            PropagateSkirtDrapeDelta(neighbors, valid, originalWorldVerts, center, up, fwd, propagatedDelta, reached);
            SmoothSkirtDrapeDelta(neighbors, valid, originalWorldVerts, center, up, fwd, radiusDown, propagatedDelta, reached);

            for (int i = 0; i < count; i++)
            {
                if (!valid[i]) continue;
                if (!reached[i] || propagatedDelta[i].sqrMagnitude <= 1e-8f) continue;

                Vector3 originalRel = originalWorldVerts[i] - center;
                float originalUp = Vector3.Dot(originalRel, up);
                float originalRight = Vector3.Dot(originalRel, right);
                float originalFwd = Vector3.Dot(originalRel, fwd);
                if (originalFwd <= 0f) continue;

                Vector2 delta = propagatedDelta[i] * SkirtDrapeDeltaBoost;
                if (delta.y <= 0f) continue;

                float targetRight = originalRight + delta.x;
                float targetFwd = originalFwd + delta.y;
                float finalRight = Vector3.Dot(morphedWorldVerts[i] - center, right);
                float finalFwd = Vector3.Dot(morphedWorldVerts[i] - center, fwd);
                float addRight = targetRight - finalRight;
                float addFwd = Mathf.Max(0f, targetFwd - finalFwd);
                if (Mathf.Abs(addRight) <= 1e-5f && addFwd <= 1e-5f)
                    continue;

                Vector3 adjustedWorld = morphedWorldVerts[i] + right * addRight + fwd * addFwd;
                Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[i]);
                newLocalVerts[i] = skin.inverse.MultiplyPoint3x4(adjustedWorld);
                morphedWorldVerts[i] = adjustedWorld;
            }
        }

        static void PropagateSkirtDrapeDelta(
            List<int>[] neighbors,
            bool[] valid,
            Vector3[] originalWorldVerts,
            Vector3 center,
            Vector3 up,
            Vector3 fwd,
            Vector2[] propagatedDelta,
            bool[] reached)
        {
            int count = propagatedDelta.Length;
            for (int pass = 0; pass < SkirtDrapePropagatePasses; pass++)
            {
                bool changed = false;
                Vector2[] nextDelta = (Vector2[])propagatedDelta.Clone();
                bool[] nextReached = (bool[])reached.Clone();

                for (int i = 0; i < count; i++)
                {
                    if (!valid[i]) continue;

                    Vector3 rel = originalWorldVerts[i] - center;
                    float originalUp = Vector3.Dot(rel, up);
                    float originalFwd = Vector3.Dot(rel, fwd);
                    if (originalFwd <= 0f) continue;

                    Vector2 sumDelta = reached[i] ? propagatedDelta[i] : Vector2.zero;
                    int sumCount = reached[i] ? 1 : 0;
                    Vector2 bestDelta = reached[i] ? propagatedDelta[i] : Vector2.zero;
                    float bestMagnitude = bestDelta.sqrMagnitude;
                    List<int> ns = neighbors[i];
                    for (int n = 0; n < ns.Count; n++)
                    {
                        int j = ns[n];
                        if (!reached[j]) continue;

                        Vector3 nr = originalWorldVerts[j] - center;
                        float neighborUp = Vector3.Dot(nr, up);
                        float neighborFwd = Vector3.Dot(nr, fwd);
                        if (neighborFwd <= 0f) continue;
                        if (neighborUp < originalUp - 0.0001f) continue;

                        Vector2 candidate = propagatedDelta[j] * SkirtDrapePropagationDecay;
                        if (candidate.y <= 0f || candidate.sqrMagnitude <= 1e-8f) continue;
                        sumDelta += candidate;
                        sumCount++;
                        float candidateMagnitude = candidate.sqrMagnitude;
                        if (candidateMagnitude > bestMagnitude)
                        {
                            bestDelta = candidate;
                            bestMagnitude = candidateMagnitude;
                        }
                    }

                    if (sumCount <= 0) continue;

                    Vector2 averageDelta = sumDelta / sumCount;
                    Vector2 targetDelta = reached[i]
                        ? (averageDelta.sqrMagnitude > propagatedDelta[i].sqrMagnitude ? averageDelta : propagatedDelta[i])
                        : Vector2.Lerp(averageDelta, bestDelta, 0.50f);

                    if (targetDelta.y > 0f
                        && targetDelta.sqrMagnitude > 1e-8f
                        && (!nextReached[i] || targetDelta.sqrMagnitude > nextDelta[i].sqrMagnitude + 1e-8f))
                    {
                        nextDelta[i] = targetDelta;
                        nextReached[i] = true;
                        changed = true;
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    propagatedDelta[i] = nextDelta[i];
                    reached[i] = nextReached[i];
                }

                if (!changed)
                    break;
            }
        }

        static void SmoothSkirtDrapeDelta(
            List<int>[] neighbors,
            bool[] valid,
            Vector3[] originalWorldVerts,
            Vector3 center,
            Vector3 up,
            Vector3 fwd,
            float radiusDown,
            Vector2[] propagatedDelta,
            bool[] reached)
        {
            int count = propagatedDelta.Length;
            float upRange = Mathf.Max(radiusDown * 0.16f, 0.01f);

            for (int pass = 0; pass < SkirtDrapeSmoothPasses; pass++)
            {
                Vector2[] nextDelta = (Vector2[])propagatedDelta.Clone();

                for (int i = 0; i < count; i++)
                {
                    if (!valid[i] || !reached[i]) continue;

                    Vector3 rel = originalWorldVerts[i] - center;
                    float originalUp = Vector3.Dot(rel, up);
                    float originalFwd = Vector3.Dot(rel, fwd);
                    if (originalFwd <= 0f) continue;

                    Vector2 sum = propagatedDelta[i] * 2f;
                    float weight = 2f;
                    List<int> ns = neighbors[i];
                    for (int n = 0; n < ns.Count; n++)
                    {
                        int j = ns[n];
                        if (!valid[j] || !reached[j]) continue;

                        Vector3 nr = originalWorldVerts[j] - center;
                        float neighborUp = Vector3.Dot(nr, up);
                        float neighborFwd = Vector3.Dot(nr, fwd);
                        if (neighborFwd <= 0f) continue;

                        float upDiff = Mathf.Abs(neighborUp - originalUp);
                        float w = 1f - Mathf.Clamp01(upDiff / upRange);
                        if (w <= 0f) continue;

                        sum += propagatedDelta[j] * w;
                        weight += w;
                    }

                    if (weight <= 2f) continue;

                    Vector2 average = sum / weight;
                    if (average.y <= 0f) continue;

                    nextDelta[i] = Vector2.Lerp(propagatedDelta[i], average, SkirtDrapeSmoothStrength);
                }

                for (int i = 0; i < count; i++)
                    propagatedDelta[i] = nextDelta[i];
            }
        }

        struct LayerGuardCoord
        {
            public float Side;
            public float Up;
            public float Fwd;
        }

        static void ApplyCrossOuterClothLayerGuard(Maid maid, List<SkinnedMeshRenderer> smrs)
        {
            if (!_bpWorldCached || maid == null || smrs == null || smrs.Count == 0)
                return;

            float strength = Mathf.Max(0f, OuterClothLayerGuard);
            if (strength <= 0f)
                return;

            float radiusScale = Mathf.Max(0.05f, 1f + InflationMultiplier);
            float radiusSide = Mathf.Max(RegionRadiusSide * radiusScale, 0.0001f);
            float radiusFront = Mathf.Max(RegionRadiusFront * radiusScale, 0.0001f);
            float radiusUp = Mathf.Max(RegionRadiusUp * radiusScale, 0.0001f);
            float radiusDown = Mathf.Max(RegionRadiusDown * radiusScale, 0.0001f);
            Vector3 center = _bpWorldFrame.Center + _bpWorldFrame.Up * InflationMoveY + _bpWorldFrame.Fwd * InflationMoveZ;
            Vector3 right = _bpWorldFrame.Right;
            Vector3 up = _bpWorldFrame.Up;
            Vector3 fwd = _bpWorldFrame.Fwd;

            float sideRange = Mathf.Max(radiusSide * 0.18f, 0.016f);
            float upRange = Mathf.Max((radiusUp + radiusDown) * 0.08f, 0.014f);
            float minOriginalGap = Mathf.Max(radiusFront * 0.006f, 0.001f);
            float maxOriginalGap = Mathf.Max(radiusFront * 0.80f, minOriginalGap * 2f);
            float minFinalGap = Mathf.Max(0.001f, 0.008f * strength);

            List<CrossLayerGuardMesh> meshes = new List<CrossLayerGuardMesh>();
            Dictionary<long, List<LayerGuardRef>> buckets = new Dictionary<long, List<LayerGuardRef>>();
            HashSet<int> seenMeshes = new HashSet<int>();

            for (int m = 0; m < smrs.Count; m++)
            {
                SkinnedMeshRenderer smr = smrs[m];
                if (smr == null || smr.sharedMesh == null) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.OuterCloth) continue;

                Mesh mesh = smr.sharedMesh;
                if (!seenMeshes.Add(mesh.GetInstanceID())) continue;

                MeshRecord rec = FindRecord(maid, smr);
                if (rec == null || rec.OrigVerts == null || rec.Mesh != mesh) continue;

                Vector3[] currentVerts = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;
                if (currentVerts == null || weights == null) continue;
                if (currentVerts.Length != rec.OrigVerts.Length || weights.Length != currentVerts.Length) continue;

                Matrix4x4[] boneMatrices;
                if (!TryBuildBindPoseSkinMatrices(smr, out boneMatrices)) continue;

                CrossLayerGuardMesh entry = new CrossLayerGuardMesh
                {
                    SMR = smr,
                    Record = rec,
                    Mesh = mesh,
                    CurrentVerts = (Vector3[])currentVerts.Clone(),
                    Weights = weights,
                    BoneMatrices = boneMatrices,
                    OriginalWorld = new Vector3[currentVerts.Length],
                    MorphedWorld = new Vector3[currentVerts.Length],
                    Valid = new bool[currentVerts.Length],
                    Coords = new LayerGuardCoord[currentVerts.Length],
                    Changed = false,
                };

                int meshIndex = meshes.Count;
                for (int i = 0; i < entry.CurrentVerts.Length; i++)
                {
                    BoneWeight weight = weights[i];
                    if (!HasValidBoneWeight(weight, boneMatrices)) continue;

                    Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weight);
                    Vector3 originalWorld = skin.MultiplyPoint3x4(rec.OrigVerts[i]);
                    Vector3 morphedWorld = skin.MultiplyPoint3x4(entry.CurrentVerts[i]);
                    Vector3 rel = originalWorld - center;
                    float side = Vector3.Dot(rel, right);
                    float originalUp = Vector3.Dot(rel, up);
                    float originalFwd = Vector3.Dot(rel, fwd);

                    entry.OriginalWorld[i] = originalWorld;
                    entry.MorphedWorld[i] = morphedWorld;
                    entry.Valid[i] = true;
                    entry.Coords[i].Side = side;
                    entry.Coords[i].Up = originalUp;
                    entry.Coords[i].Fwd = originalFwd;

                    if (originalFwd <= 0f) continue;
                    if (GetOuterLayerGuardVerticalFade(originalUp, radiusUp, radiusDown) <= 0f) continue;

                    int sideBin = Mathf.FloorToInt(side / sideRange);
                    int upBin = Mathf.FloorToInt(originalUp / upRange);
                    long key = LayerBucketKey(sideBin, upBin);
                    if (!buckets.TryGetValue(key, out List<LayerGuardRef> list))
                    {
                        list = new List<LayerGuardRef>();
                        buckets[key] = list;
                    }
                    list.Add(new LayerGuardRef(meshIndex, i));
                }

                meshes.Add(entry);
            }

            if (meshes.Count == 0 || buckets.Count == 0)
                return;

            for (int pass = 0; pass < 2; pass++)
            {
                bool changedThisPass = false;

                for (int mi = 0; mi < meshes.Count; mi++)
                {
                    CrossLayerGuardMesh entry = meshes[mi];
                    for (int i = 0; i < entry.CurrentVerts.Length; i++)
                    {
                        if (!entry.Valid[i]) continue;

                        LayerGuardCoord c = entry.Coords[i];
                        if (c.Fwd <= 0f) continue;
                        float guardFade = GetOuterLayerGuardVerticalFade(c.Up, radiusUp, radiusDown);
                        if (guardFade <= 0f) continue;

                        int sideBin = Mathf.FloorToInt(c.Side / sideRange);
                        int upBin = Mathf.FloorToInt(c.Up / upRange);
                        int coverMesh = -1;
                        int coverVertex = -1;
                        float bestScore = float.MaxValue;

                        for (int sy = -2; sy <= 2; sy++)
                        {
                            for (int uy = -2; uy <= 2; uy++)
                            {
                                long key = LayerBucketKey(sideBin + sy, upBin + uy);
                                if (!buckets.TryGetValue(key, out List<LayerGuardRef> list)) continue;

                                for (int n = 0; n < list.Count; n++)
                                {
                                    LayerGuardRef r = list[n];
                                    if (r.MeshIndex == mi && r.VertexIndex == i) continue;

                                    CrossLayerGuardMesh other = meshes[r.MeshIndex];
                                    if (!other.Valid[r.VertexIndex]) continue;

                                    LayerGuardCoord jc = other.Coords[r.VertexIndex];
                                    float fwdGap = jc.Fwd - c.Fwd;
                                    if (fwdGap < minOriginalGap || fwdGap > maxOriginalGap) continue;

                                    float sideDiff = Mathf.Abs(jc.Side - c.Side);
                                    if (sideDiff > sideRange * 1.6f) continue;
                                    float upDiff = Mathf.Abs(jc.Up - c.Up);
                                    if (upDiff > upRange * 1.6f) continue;

                                    float sideScore = sideDiff / sideRange;
                                    float upScore = upDiff / upRange;
                                    float fwdScore = fwdGap / maxOriginalGap;
                                    float sameMeshPenalty = r.MeshIndex == mi ? 0.15f : 0f;
                                    float score = sideScore * sideScore + upScore * upScore + fwdScore * 0.20f + sameMeshPenalty;
                                    if (score < bestScore)
                                    {
                                        bestScore = score;
                                        coverMesh = r.MeshIndex;
                                        coverVertex = r.VertexIndex;
                                    }
                                }
                            }
                        }

                        if (coverMesh < 0 || coverVertex < 0)
                            continue;

                        CrossLayerGuardMesh cover = meshes[coverMesh];
                        float currentFwd = Vector3.Dot(entry.MorphedWorld[i] - center, fwd);
                        float coverFwd = Vector3.Dot(cover.MorphedWorld[coverVertex] - center, fwd);
                        float targetFwd = coverFwd - minFinalGap * guardFade;
                        float overrun = currentFwd - targetFwd;
                        if (overrun <= 0f)
                            continue;

                        Vector3 adjustedWorld = entry.MorphedWorld[i] - fwd * (overrun * guardFade);
                        Matrix4x4 skin = GetWeightedSkinMatrix(entry.BoneMatrices, entry.Weights[i]);
                        entry.CurrentVerts[i] = skin.inverse.MultiplyPoint3x4(adjustedWorld);
                        entry.MorphedWorld[i] = adjustedWorld;
                        entry.Changed = true;
                        changedThisPass = true;
                    }
                }

                if (!changedThisPass)
                    break;
            }

            for (int i = 0; i < meshes.Count; i++)
            {
                CrossLayerGuardMesh entry = meshes[i];
                if (!entry.Changed) continue;

                entry.Record.LastDeltaVerts = BuildDeltaVerts(entry.Record.OrigVerts, entry.CurrentVerts);
                entry.Record.AppliedSignature = ComputeVertexSignature(entry.CurrentVerts);
                entry.Mesh.vertices = entry.CurrentVerts;
                ApplySmoothedNormals(entry.Mesh, entry.Record, entry.CurrentVerts);
                entry.Mesh.RecalculateBounds();
            }
        }

        static float GetOuterLayerGuardVerticalFade(float originalUp, float radiusUp, float radiusDown)
        {
            if (originalUp > radiusUp * 0.35f)
                return 0f;

            float fadeStart = -radiusDown * 0.55f;
            float fadeEnd = -radiusDown * 1.15f;
            if (originalUp >= fadeStart)
                return 1f;
            if (originalUp <= fadeEnd)
                return 0f;

            return Smooth01((originalUp - fadeEnd) / Mathf.Max(fadeStart - fadeEnd, 0.0001f));
        }

        static long LayerBucketKey(int sideBin, int upBin)
        {
            return ((long)sideBin << 32) ^ (uint)upBin;
        }

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

        static Vector3 ApplyClothLayerOffsetWorld(
            MeshMorphClass meshClass,
            Vector3 original,
            Vector3 morphed,
            Vector3 center,
            float shapeWeight,
            float directionalRadius,
            float originalRadius,
            float edgeRatio,
            float upDot,
            float radiusDown,
            Vector3 right,
            Vector3 up,
            Vector3 fwd)
        {
            if (meshClass == MeshMorphClass.Body || meshClass == MeshMorphClass.Ignore)
                return morphed;

            bool isOuterCloth = meshClass == MeshMorphClass.OuterCloth;
            float layerOffset = meshClass == MeshMorphClass.OuterCloth
                ? OuterClothOffset
                : InnerClothOffset;
            float preserve = isOuterCloth ? ClothThicknessPreserve : 0f;

            if (layerOffset == 0f && preserve == 0f) return morphed;

            Vector3 rel = original - center;
            if (rel.sqrMagnitude < 1e-8f)
                rel = morphed - center;
            if (rel.sqrMagnitude < 1e-8f)
                return morphed;

            Vector3 displacement = morphed - original;
            Vector3 offsetBase = displacement.sqrMagnitude > 1e-8f
                ? displacement.normalized
                : rel.normalized;

            float side = Vector3.Dot(offsetBase, right);
            float vertical = Vector3.Dot(offsetBase, up);
            float forward = Vector3.Dot(offsetBase, fwd);
            float sideRatio = isOuterCloth ? ClothOffsetSideRatio : 1f;
            Vector3 offsetDir = right * (side * sideRatio)
                + up * vertical
                + fwd * forward;
            float directionScale = offsetDir.magnitude;
            if (offsetDir.sqrMagnitude < 1e-8f)
                return morphed;
            else
                offsetDir.Normalize();

            float shellRatio = originalRadius / Mathf.Max(directionalRadius, 0.0001f);
            float surface = Smooth01((shellRatio - 0.35f) / 0.65f);
            float boundaryFade = Smooth01((1f - edgeRatio) / 0.35f);
            float fade = Smooth01(shapeWeight) * boundaryFade;
            float baseThickness = layerOffset + directionalRadius * 0.10f * preserve * surface;
            float back = Mathf.Clamp01(-forward);
            float backScale = isOuterCloth ? Mathf.Lerp(1f, ClothBackOffsetBoost, back) : 1f;
            float baseAmount = baseThickness * fade;

            return morphed + offsetDir * (baseAmount * directionScale * backScale);
        }

        static Vector3 ApplyClothDepthStretchWorld(
            MeshMorphClass meshClass,
            Vector3 original,
            Vector3 morphed,
            float directionalRadius,
            float originalRadius,
            float upDot,
            float radiusDown)
        {
            if (meshClass != MeshMorphClass.OuterCloth || ClothDepthStretch == 0f)
                return morphed;

            Vector3 displacement = morphed - original;
            if (displacement.sqrMagnitude < 1e-10f)
                return morphed;

            float depthStretch = GetClothDepthStretchFactor(
                meshClass,
                originalRadius,
                directionalRadius,
                upDot,
                radiusDown);

            return original + displacement * depthStretch;
        }

        static float GetClothDepthStretchFactor(
            MeshMorphClass meshClass,
            float originalRadius,
            float directionalRadius,
            float upDot,
            float radiusDown)
        {
            if (meshClass != MeshMorphClass.OuterCloth)
                return 1f;

            float shellRatio = Mathf.Clamp01(originalRadius / Mathf.Max(directionalRadius, 0.0001f));
            float outerSurface = Smooth01((shellRatio - 0.20f) / 0.80f);
            float lower = upDot < 0f
                ? Smooth01((-upDot / Mathf.Max(radiusDown, 0.0001f) - 0.25f) / 0.75f)
                : 0f;
            float minFollow = Mathf.Lerp(0.30f, 0.16f, lower);
            float oldCompressionFollow = Mathf.Lerp(minFollow, 1f, outerSurface);
            float stretchControl = ClothDepthStretch * Mathf.Lerp(1f, 1.35f, lower);
            float inverseCompression = (oldCompressionFollow - minFollow) / Mathf.Max(1f - minFollow, 0.0001f);
            return 1f + inverseCompression * stretchControl;
        }

        static void ApplySmoothedNormals(Mesh mesh, MeshRecord rec, Vector3[] newVerts)
        {
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

            bool[] affected = BuildNormalAffectedMask(mesh, rec.OrigVerts, newVerts);
            mesh.RecalculateNormals();

            Vector3[] recalculated = mesh.normals;
            if (recalculated == null || recalculated.Length != newVerts.Length) return;

            Vector3[] finalNormals = new Vector3[newVerts.Length];
            for (int i = 0; i < finalNormals.Length; i++)
                finalNormals[i] = affected[i] ? recalculated[i] : rec.OrigNormals[i];

            mesh.normals = finalNormals;
        }

        static bool[] BuildNormalAffectedMask(Mesh mesh, Vector3[] originalVerts, Vector3[] newVerts)
        {
            int count = newVerts.Length;
            bool[] affected = new bool[count];

            for (int i = 0; i < count; i++)
                affected[i] = (newVerts[i] - originalVerts[i]).sqrMagnitude >= NormalAffectedDelta * NormalAffectedDelta;

            int[] triangles = null;
            try
            {
                triangles = mesh.triangles;
            }
            catch
            {
                return affected;
            }

            if (triangles == null || triangles.Length < 3) return affected;

            for (int pass = 0; pass < NormalAffectedExpandPasses; pass++)
            {
                bool[] expanded = (bool[])affected.Clone();

                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int a = triangles[i];
                    int b = triangles[i + 1];
                    int c = triangles[i + 2];
                    if (a < 0 || b < 0 || c < 0) continue;
                    if (a >= count || b >= count || c >= count) continue;
                    if (!affected[a] && !affected[b] && !affected[c]) continue;

                    expanded[a] = true;
                    expanded[b] = true;
                    expanded[c] = true;
                }

                affected = expanded;
            }

            return affected;
        }

        static Vector3 RoundToSidesWorld(
            Vector3 original,
            Vector3 smoothed,
            Vector3 center,
            Vector3 fwd,
            float radiusBack,
            float radiusFront)
        {
            float strength = Mathf.Max(SideSmoothStrength, 0f);
            float width = Mathf.Max(SideSmoothWidth, 0f);
            if (strength <= 0f || width <= 0f) return smoothed;

            float originalFwd = Vector3.Dot(original - center, fwd);
            float forwardFromBack = originalFwd + radiusBack;
            float smoothDistance = Mathf.Max((radiusBack + radiusFront * 0.5f) * width, 0.0001f);
            if (forwardFromBack >= smoothDistance) return smoothed;

            float curve = BellySidesCurve(forwardFromBack / smoothDistance);
            float t = Mathf.Clamp01(1f - (1f - curve) * strength);
            return Vector3.Lerp(original, smoothed, t);
        }

        static Vector3 ReduceRibStretchingWorld(
            Vector3 original,
            Vector3 smoothed,
            Vector3 center,
            Vector3 up,
            Vector3 fwd,
            float radiusUp)
        {
            float originalUp = Vector3.Dot(original - center, up);
            float topExtent = radiusUp;
            float topOffset = Mathf.Max(radiusUp * 0.5f, 0.0001f);

            if (originalUp > topExtent)
                return original;

            if (originalUp < topExtent - topOffset)
                return smoothed;

            float t = BellyTopCurve((topExtent - originalUp) / topOffset);
            return Vector3.Lerp(original, smoothed, t);
        }

        static float BellyEdgeCurve(float value)
        {
            float t = Mathf.Clamp01(value);
            if (t < 0.25f) return Mathf.Lerp(0f, 0.001f, t / 0.25f);
            if (t < 0.5f) return 0.001f;
            if (t < 0.75f) return Mathf.Lerp(0.001f, 0.2f, (t - 0.5f) / 0.25f);
            if (t < 0.9f) return Mathf.Lerp(0.2f, 0.7f, (t - 0.75f) / 0.15f);
            return Mathf.Lerp(0.7f, 1f, (t - 0.9f) / 0.1f);
        }

        static float BellySidesCurve(float value)
        {
            float t = Mathf.Clamp01(value);
            if (t < 0.25f) return Mathf.Lerp(0f, 0.15f, t / 0.25f);
            if (t < 0.5f) return Mathf.Lerp(0.15f, 0.35f, (t - 0.25f) / 0.25f);
            if (t < 0.7f) return Mathf.Lerp(0.35f, 0.7f, (t - 0.5f) / 0.2f);
            if (t < 0.9f) return Mathf.Lerp(0.7f, 0.9f, (t - 0.7f) / 0.2f);
            return Mathf.Lerp(0.9f, 1f, (t - 0.9f) / 0.1f);
        }

        static float BellyTopCurve(float value)
        {
            float t = Mathf.Clamp01(value);
            if (t < 0.25f) return Mathf.Lerp(0f, 0.1f, t / 0.25f);
            if (t < 0.5f) return Mathf.Lerp(0.1f, 0.35f, (t - 0.25f) / 0.25f);
            if (t < 0.75f) return Mathf.Lerp(0.35f, 0.9f, (t - 0.5f) / 0.25f);
            return Mathf.Lerp(0.9f, 1f, (t - 0.75f) / 0.25f);
        }

        static float BellyGapCurve(float value)
        {
            float t = Mathf.Clamp01(value);
            if (t < 0.1f) return Mathf.Lerp(0f, 0.15f, t / 0.1f);
            if (t < 0.25f) return Mathf.Lerp(0.15f, 0.25f, (t - 0.1f) / 0.15f);
            if (t < 0.5f) return Mathf.Lerp(0.25f, 0.7f, (t - 0.25f) / 0.25f);
            if (t < 0.75f) return Mathf.Lerp(0.7f, 0.95f, (t - 0.5f) / 0.25f);
            return Mathf.Lerp(0.95f, 1f, (t - 0.75f) / 0.25f);
        }

        static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        static bool HasValidBoneWeight(BoneWeight weight, Matrix4x4[] matrices)
        {
            return IsValidBoneWeightIndex(weight.boneIndex0, weight.weight0, matrices)
                || IsValidBoneWeightIndex(weight.boneIndex1, weight.weight1, matrices)
                || IsValidBoneWeightIndex(weight.boneIndex2, weight.weight2, matrices)
                || IsValidBoneWeightIndex(weight.boneIndex3, weight.weight3, matrices);
        }

        static bool IsValidBoneWeightIndex(int index, float weight, Matrix4x4[] matrices)
        {
            return weight > 0f && matrices != null && index >= 0 && index < matrices.Length;
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
            bool _isMorphing = false;
            bool _needsFullRefresh = false;

            public void SetMaid(Maid m) { _maid = m; }

            public void TriggerFullRefresh()
            {
                if (_maid == null) return;
                _needsFullRefresh = true;
                EnsureRunning();
            }

            void EnsureRunning()
            {
                if (_isMorphing || _maid == null) return;
                StartCoroutine(MorphCoroutine());
            }

            bool HasPendingWork()
            {
                return _needsFullRefresh;
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

            System.Collections.IEnumerator MorphCoroutine()
            {
                _isMorphing = true;
                while (_maid != null && HasPendingWork())
                {
                    while (_maid != null && (_maid.body0 == null || !_maid.body0.isLoadedBody))
                        yield return null;

                    if (_maid == null) break;

                    if (_needsFullRefresh)
                    {
                        int stableFrames = 0;
                        Dictionary<int, int> previousSignatures = new Dictionary<int, int>();

                        while (_maid != null && stableFrames < AutoMorphStableFrames)
                        {
                            yield return new WaitForEndOfFrame();
                            yield return null;

                            while (_maid != null && (_maid.body0 == null || !_maid.body0.isLoadedBody))
                                yield return null;

                            if (_maid == null) break;

                            List<SkinnedMeshRenderer> tracked = CollectRelevantRenderers(_maid);
                            if (AreTrackedRenderersStable(tracked, previousSignatures))
                                stableFrames++;
                            else
                                stableFrames = 0;
                        }

                        if (_maid == null) break;

                        _needsFullRefresh = false;
                        ForgetRecords(_maid);
                        PruneRecords(_maid);

                        float pFull = GetActiveProgress(_maid);
                        if (pFull > 0f)
                            ApplyToSlots(_maid, pFull, false);

                        continue;
                    }
                }

                _isMorphing = false;
            }

            void LateUpdate()
            {
                if (_maid == null) return;
                BellyMorphController.FlushMorphDirty(_maid);
            }
        }
    }
}
