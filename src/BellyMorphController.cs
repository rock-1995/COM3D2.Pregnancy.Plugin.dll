using UnityEngine;
using System.Collections.Generic;

namespace COM3D2.Pregnancy.Plugin
{
    public static class BellyMorphController
    {
        public const int HaraMax = 100;

        // ── Parameters ──────────────────────────────────────────────────────
        public static float SpineLerpT     = 1.0f;
        public static float OffsetForward  = -0.055f;
        public static float OffsetSide     = 0.0f;
        public static float RadiusH        = 0.22f;
        public static float RadiusV        = 0.21f;
        public static float MaxPushOut     = 1.5f;
        public static float MaxDropDown    = 0.2f;
        public static float TaperZ         = -1.5f;
        public static float TaperTop       = 0.0f;
        public static float StretchX       = -0.8f;  // 横方向（左右）
        public static float StretchY       = 0.0f;   // Vertical (up and down)
        public static float StretchZ       = 0.0f;   // 前後方向
        public static float SideFadeStart  = 10.0f;
        public static float SideFadeCutoff = 0.0f;
        // Inner clothing (high belly coverage): small overdrive
        public static float ClothOverdrive      = 1.03f;
        // Outer clothing (low belly coverage): larger overdrive
        public static float OuterClothOverdrive = 1.20f;
        // masked/total vertex ratio threshold: below = outer clothing
        public static float OuterClothThreshold = 0.30f;

        // World-space T-pose reference — cached from body mesh, reused by all other meshes.
        static bool    _wdCached;
        static Vector3 _wdUp, _wdRight, _wdFwd, _wdCenter;

        static readonly string[] FaceKeywords = {
            "face", "head", "eye", "mayu", "tooth", "teeth",
            "tongue", "lip", "hair", "nose", "ear"
        };

        static bool IsFaceSlot(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLowerInvariant();
            foreach (string kw in FaceKeywords)
                if (lower.Contains(kw)) return true;
            return false;
        }

        // ── アクティブ状態 ────────────────────────────────────────────
        static Dictionary<int, float> _activeProgress = new Dictionary<int, float>();

        // メッシュ記録：sharedMesh は差し替えない。元の頂点を保存して in-place に書き換え
        class MeshRecord
        {
            public SkinnedMeshRenderer SMR;
            public Mesh   Mesh;            // sharedMesh への参照（差し替えない）
            public Vector3[] OrigVerts;    // 元の bind pose 頂点（復元用）
            public Vector3[] OrigNormals;
        }

        static Dictionary<int, List<MeshRecord>> _records = new Dictionary<int, List<MeshRecord>>();

        static BepInEx.Logging.ManualLogSource _log =
            BepInEx.Logging.Logger.CreateLogSource("Pregnancy");

        // ── Public API ──────────────────────────────────────────────────

        public static bool IsValid(Maid maid)
            => maid != null && maid.body0 != null && maid.body0.isLoadedBody;

        public static bool IsActive(Maid maid)
            => maid != null && _activeProgress.ContainsKey(maid.GetHashCode());

        public static float GetActiveProgress(Maid maid)
        {
            float p;
            return (maid != null && _activeProgress.TryGetValue(maid.GetHashCode(), out p)) ? p : 0f;
        }

        public static void MarkActive(Maid maid, float progress)
        {
            if (maid == null) return;
            _activeProgress[maid.GetHashCode()] = Mathf.Clamp01(progress);
        }

        public static void ApplyProgress(Maid maid, float progress)
        {
            if (!IsValid(maid)) return;
            ResetInternal(maid);
            _activeProgress[maid.GetHashCode()] = Mathf.Clamp01(progress);
            ApplyToSlots(maid, Mathf.Clamp01(progress));

            if (maid.gameObject.GetComponent<BellyMonitor>() == null)
            {
                var mon = maid.gameObject.AddComponent<BellyMonitor>();
                mon.SetMaid(maid);
            }
        }

        public static void SetBelly(Maid maid, float sliderValue)
            => ApplyProgress(maid, sliderValue / 100f);

        public static void Reset(Maid maid)
        {
            if (maid == null) return;
            ResetInternal(maid);
            var mon = maid.gameObject.GetComponent<BellyMonitor>();
            if (mon != null) Object.DestroyImmediate(mon);
        }

        static void ResetInternal(Maid maid)
        {
            int key = maid.GetHashCode();
            _activeProgress.Remove(key);
            List<MeshRecord> records;
            if (!_records.TryGetValue(key, out records)) return;
            foreach (var r in records)
            {
                // sharedMesh への参照はそのまま。頂点を元に戻すだけ。
                if (r.SMR != null && r.Mesh != null && r.OrigVerts != null)
                {
                    r.Mesh.vertices = r.OrigVerts;
                    r.Mesh.normals  = r.OrigNormals;
                    r.Mesh.RecalculateBounds();
                }
            }
            records.Clear();
            _records.Remove(key);
        }

        // LoadSkinMesh_R パッチから呼ばれる
        public static void ApplyFromPatch(Maid maid, SkinnedMeshRenderer smr, string slotObjName)
        {
            if (!IsActive(maid)) return;
            if (smr == null || smr.sharedMesh == null) return;
            if (IsFaceSlot(slotObjName)) return;
            // Only react to torso meshes; re-apply everything for consistency
            if (HasTorsoBones(smr))
                ApplyProgress(maid, GetActiveProgress(maid));
        }

        // True if smr has any bone from the pelvis/spine family (any level)
        static bool HasTorsoBones(SkinnedMeshRenderer smr)
        {
            var bones = smr.bones;
            if (bones == null) return false;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                string n = bones[i].name;
                if (n.StartsWith("Bip01 Pelvis") || n.StartsWith("Bip01 Spine"))
                    return true;
            }
            return false;
        }

        static void ApplyToSlots(Maid maid, float progress)
        {
            if (maid.body0.goSlot == null) return;
            _wdCached = false;   // reset world-direction cache for this apply pass
            for (int si = 0; si < maid.body0.goSlot.Count; si++)
            {
                TBodySkin slot = maid.body0.goSlot[si];
                if (slot?.obj == null) continue;
                if (IsFaceSlot(slot.obj.name)) continue;
                var smr = slot.obj.GetComponentInChildren<SkinnedMeshRenderer>(false);
                if (smr == null || smr.sharedMesh == null) continue;
                ApplySMR(maid, smr, progress);
            }
        }

        static void ApplySMR(Maid maid, SkinnedMeshRenderer smr, float progress)
        {
            Mesh mesh     = smr.sharedMesh;
            var bindposes = mesh.bindposes;
            var bones     = smr.bones;
            if (bones == null || bindposes == null || bones.Length == 0) return;

            // ── Search _SCL_ proxy bones in smr.bones ────────────────────
            int pelvisIdx = -1, spineIdx = -1, spineaIdx = -1;
            int lThighIdx = -1, rThighIdx = -1;
            bool hasTorso = false;
            for (int j = 0; j < bones.Length && j < bindposes.Length; j++)
            {
                if (bones[j] == null) continue;
                string n = bones[j].name;
                if      (n == "Bip01 Pelvis_SCL_"  || n == "Bip01 Pelvis")   { pelvisIdx = j; hasTorso = true; }
                else if (n == "Bip01 Spine_SCL_"   || n == "Bip01 Spine")    { spineIdx  = j; hasTorso = true; }
                else if (n == "Bip01 Spinea_SCL_"  || n == "Bip01 Spine0a_SCL_"
                      || n == "Bip01 Spinea")                                  { spineaIdx = j; hasTorso = true; }
                else if (n == "Bip01 L Thigh_SCL_" || n == "Bip01 L Thigh")  lThighIdx = j;
                else if (n == "Bip01 R Thigh_SCL_" || n == "Bip01 R Thigh")  rThighIdx = j;
                else if (n.StartsWith("Bip01 Spine1"))                         hasTorso = true;
            }

            _log.LogInfo($"[Belly] {smr.name}: pelvis={pelvisIdx} spine={spineIdx} hasTorso={hasTorso}");
            if (!hasTorso) return;   // not a torso mesh (nipples, shoes…)

            Matrix4x4 l2w = smr.transform.localToWorldMatrix;

            if (pelvisIdx >= 0 && spineIdx >= 0)
            {
                // ── Bindpose T-pose world positions (pose-independent) ────
                Vector3 pelvisW = (l2w * bindposes[pelvisIdx].inverse).MultiplyPoint(Vector3.zero);
                Vector3 spineW  = (l2w * bindposes[spineIdx ].inverse).MultiplyPoint(Vector3.zero);

                if (lThighIdx >= 0 && rThighIdx >= 0)
                {
                    // Best direction: thigh bones give reliable left-right axis
                    Vector3 lW = (l2w * bindposes[lThighIdx].inverse).MultiplyPoint(Vector3.zero);
                    Vector3 rW = (l2w * bindposes[rThighIdx].inverse).MultiplyPoint(Vector3.zero);
                    _wdUp    = (spineW - pelvisW).normalized;
                    Vector3 tv = rW - lW;
                    _wdRight = (tv - _wdUp * Vector3.Dot(tv, _wdUp)).normalized;
                    _wdFwd   = Vector3.Cross(_wdRight, _wdUp).normalized;
                }
                else if (!_wdCached)
                {
                    _wdUp    = (spineW - pelvisW).normalized;
                    _wdRight = maid.transform.right;
                    _wdFwd   = Vector3.Cross(_wdRight, _wdUp).normalized;
                }

                // World center (same formula → same world point for all meshes)
                float t = SpineLerpT;
                Vector3 worldBase;
                if (t <= 1f)
                    worldBase = Vector3.Lerp(pelvisW, spineW, t);
                else
                {
                    float step = Vector3.Distance(pelvisW, spineW);
                    if (spineaIdx >= 0)
                    {
                        Vector3 spineaW = (l2w * bindposes[spineaIdx].inverse).MultiplyPoint(Vector3.zero);
                        worldBase = Vector3.Lerp(spineW, spineaW, t - 1f);
                    }
                    else
                        worldBase = spineW + _wdUp * ((t - 1f) * step);
                }
                _wdCenter = worldBase + _wdFwd * OffsetForward + _wdRight * OffsetSide;
                _wdCached = true;
            }
            else if (!_wdCached)
            {
                // Outer clothing processed before body — skip; will be applied next pass
                return;
            }

            // ── Transform cached world vectors into this mesh's local space ──
            Vector3 localCenter = smr.transform.InverseTransformPoint(_wdCenter);
            Vector3 rawFwd  = smr.transform.InverseTransformDirection(_wdFwd);
            Vector3 rawDown = smr.transform.InverseTransformDirection(-_wdUp);
            float sH = Mathf.Max(rawFwd.magnitude,  0.0001f);
            float sV = Mathf.Max(rawDown.magnitude, 0.0001f);
            Vector3 localFwd  = rawFwd  / sH;
            Vector3 localDown = rawDown / sV;
            Vector3 localUp   = -localDown;

            float rH = RadiusH * sH;
            float rV = RadiusV * sV;

            Vector3[] origVerts   = mesh.vertices;
            Vector3[] origNormals = mesh.normals;

            bool[] mask = BuildMask(origVerts, localCenter, localUp, localFwd, rH, rV, SideFadeCutoff);

            int mc = 0;
            for (int i = 0; i < mask.Length; i++) if (mask[i]) mc++;
            _log.LogInfo("[Pregnancy] " + smr.gameObject.name
                + " verts=" + mesh.vertexCount + " masked=" + mc);
            if (mc == 0) return;

            // ── Effective progress: body unchanged; clothing uses overdrive ──
            float effectiveProgress = progress;
            if (smr.name != "body")
            {
                float ratio = mc / (float)mesh.vertexCount;
                effectiveProgress = progress * (ratio >= OuterClothThreshold
                    ? ClothOverdrive : OuterClothOverdrive);
            }

            var record = new MeshRecord
            {
                SMR       = smr,
                Mesh      = mesh,
                OrigVerts   = (Vector3[])origVerts.Clone(),
                OrigNormals = (Vector3[])origNormals.Clone(),
            };
            int key = maid.GetHashCode();
            if (!_records.ContainsKey(key))
                _records[key] = new List<MeshRecord>();
            _records[key].Add(record);

            Vector3[] newVerts = DeformVerts(
                origVerts, origNormals, mask,
                localCenter, localFwd, localDown, localUp,
                rH, rV, sH, sV, effectiveProgress);

            mesh.vertices = newVerts;
            mesh.RecalculateBounds();
        }

        // ── bind pose 頂点でマスク構築 ────────────────────────────────
        // BakeMesh 不使用。localCenter はメッシュローカル空間で渡す。
        static bool[] BuildMask(Vector3[] verts,
                                 Vector3 localCenter, Vector3 localUp, Vector3 localFwd,
                                 float rH, float rV, float cutoff)
        {
            int    n    = verts.Length;
            bool[] mask = new bool[n];
            // mask 用には少し広いサイズを使う
            float mH = rH * 2.5f / rH * rH; // = rH * 2.5
            float mV = rV * 1.8f / rV * rV; // = rV * 1.8
            mH = rH * 2.5f;
            mV = rV * 1.8f;

            for (int i = 0; i < n; i++)
            {
                Vector3 delta = verts[i] - localCenter;
                if (Vector3.Dot(delta.normalized, localFwd) < cutoff - 0.3f) continue;
                float   upDot = Vector3.Dot(delta, localUp);
                Vector3 lat   = delta - localUp * upDot;
                float   ellip = (lat.magnitude / mH) * (lat.magnitude / mH)
                              + (Mathf.Abs(upDot)   / mV) * (Mathf.Abs(upDot) / mV);
                mask[i] = ellip < 1f;
            }
            return mask;
        }

        static Vector3[] DeformVerts(
            Vector3[] origVerts, Vector3[] origNormals, bool[] mask,
            Vector3 localCenter, Vector3 localFwd, Vector3 localDown, Vector3 localUp,
            float rH, float rV, float sH, float sV,
            float progress)
        {
            float fadeStart  = SideFadeStart;
            float fadeCutoff = SideFadeCutoff;
            float fadeRange  = Mathf.Max(fadeStart - fadeCutoff, 0.0001f);

            Vector3[] newVerts = new Vector3[origVerts.Length];

            for (int i = 0; i < origVerts.Length; i++)
            {
                Vector3 vert   = origVerts[i];
                Vector3 normal = origNormals[i];
                Vector3 delta  = vert - localCenter;

                if (!mask[i]) { newVerts[i] = vert; continue; }

                float normalDot = Vector3.Dot(normal.normalized, localFwd);
                if (normalDot <= fadeCutoff) { newVerts[i] = vert; continue; }
                float sideFade = Mathf.Clamp01((normalDot - fadeCutoff) / fadeRange);

                float   upDot = Vector3.Dot(delta, localUp);
                Vector3 lat   = delta - localUp * upDot;

                float ellip = (lat.magnitude / rH) * (lat.magnitude / rH)
                            + (Mathf.Abs(upDot) / rV) * (Mathf.Abs(upDot) / rV);
                if (ellip >= 1f) { newVerts[i] = vert; continue; }

                float t0      = 1f - ellip;
                float falloff = t0 * t0 * (3f - 2f * t0);
                float str     = falloff * progress * sideFade;
                if (str < 0.0001f) { newVerts[i] = vert; continue; }

                // 中心→頂点の放射方向（前方半球クランプ）
                Vector3 radial = delta;
                float   fd     = Vector3.Dot(radial, localFwd);
                if (fd < 0f) radial -= localFwd * fd;
                if (radial.sqrMagnitude < 1e-6f) radial = localFwd;
                radial = radial.normalized;

                // 上端平滑：上部で放射方向の上向き成分を抑制
                if (upDot > 0f)
                {
                    float topT   = Mathf.Clamp01(upDot / rV);
                    float upComp = Vector3.Dot(radial, localUp);
                    if (upComp > 0f)
                    {
                        radial = radial - localUp * (upComp * topT);
                        if (radial.sqrMagnitude < 1e-6f) radial = localFwd;
                        radial = radial.normalized;
                    }
                }

                // Stretch XYZ（それぞれの方向に放射方向を伸縮）
                // localFwd = Z相当（前後），localUp = Y相当（上下），左右 = X
                if (StretchX != 0f || StretchY != 0f || StretchZ != 0f)
                {
                    Vector3 localRight = Vector3.Cross(localUp, localFwd).normalized;
                    float rx = Vector3.Dot(radial, localRight);
                    float ry = Vector3.Dot(radial, localUp);
                    float rz = Vector3.Dot(radial, localFwd);

                    rx *= (1f + StretchX);
                    ry *= (1f + StretchY);
                    rz *= (1f + StretchZ);

                    radial = localRight * rx + localUp * ry + localFwd * rz;
                    if (radial.sqrMagnitude < 1e-6f) radial = localFwd;
                    radial = radial.normalized;
                }

                float pushMult = 1f;
                if (TaperZ != 0f)
                {
                    float tt = Mathf.Clamp01(-upDot / rV);
                    pushMult *= 1f + TaperZ * tt;
                }
                if (TaperTop != 0f)
                {
                    float tt = Mathf.Clamp01(upDot / rV);
                    pushMult *= 1f + TaperTop * tt;
                }
                if (pushMult < 0f) pushMult = 0f;

                vert += radial * (MaxPushOut * str * pushMult);

                // DropDown：中心より上でフェードアウト
                float dropFade = Mathf.Clamp01(1f - upDot / rV);
                vert += localDown * (MaxDropDown * str * dropFade);

                newVerts[i] = vert;
            }

            return newVerts;
        }

        static Transform FindBone(Maid maid, string name)
        {
            Transform b = maid.body0.GetBone(name);
            if (b != null) return b;
            return FindInChildren(maid.transform, name);
        }

        static Transform FindInChildren(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform r = FindInChildren(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        static Vector3 CalcCenter(Transform pelvis, Transform spine, Transform spinea,
                                  Vector3 bodyUp, Vector3 bodyForward, Vector3 bodyRight)
        {
            float t = SpineLerpT;
            Vector3 basePos;
            if (t <= 1f)
                basePos = Vector3.Lerp(pelvis.position, spine.position, t);
            else
            {
                float t2   = t - 1f;
                float step = Vector3.Distance(pelvis.position, spine.position);
                if (spinea != null && Vector3.Distance(spine.position, spinea.position) > 0.01f)
                    basePos = Vector3.Lerp(spine.position, spinea.position, t2);
                else
                    basePos = spine.position + bodyUp * (t2 * step);
            }
            return basePos + bodyForward * OffsetForward + bodyRight * OffsetSide;
        }

        // ── 衣服変更監視 ──────────────────────────────────────────────

        public class BellyMonitor : MonoBehaviour
        {
            Maid _maid;
            public void SetMaid(Maid m) { _maid = m; }

            void Update()
            {
                if (_maid == null) return;
                int key = _maid.GetHashCode();
                List<MeshRecord> records;
                if (!_records.TryGetValue(key, out records) || records.Count == 0) return;

                // SMR が null（脱衣で GameObject 破棄）になったレコードをクリーンアップ
                bool needsCleanup = false;
                foreach (var r in records)
                {
                    if (r.SMR == null) { needsCleanup = true; break; }
                }

                if (!needsCleanup) return;

                // 生きている SMR は元の頂点に戻す
                foreach (var r in records)
                {
                    if (r.SMR != null && r.Mesh != null && r.OrigVerts != null)
                    {
                        r.Mesh.vertices = r.OrigVerts;
                        r.Mesh.normals  = r.OrigNormals;
                        r.Mesh.RecalculateBounds();
                    }
                }
                records.Clear();
                _records.Remove(key);
            }
        }
    }
}
