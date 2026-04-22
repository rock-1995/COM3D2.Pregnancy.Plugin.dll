using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public static class BellyMorphController
    {
        // MPN.Hara の最大有効値（40 を超えると変化しない）
        public const int HaraMax = 200;

        private static int  _originalHara = -1;
        private static Maid _savedMaid    = null;

        /// <summary>progress 0.0-1.0 を腹部変形に適用</summary>
        public static void ApplyProgress(Maid maid, float progress)
        {
            SetBelly(maid, Mathf.Clamp01(progress) * 100f);
        }

        /// <summary>sliderValue 0-100 → MPN.Hara 0-40</summary>
        public static void SetBelly(Maid maid, float sliderValue)
        {
            if (!IsValid(maid)) return;

            if (!object.ReferenceEquals(maid, _savedMaid))
            {
                _savedMaid    = maid;
                _originalHara = maid.GetProp(MPN.Hara).value;
            }

            int val = (int)Mathf.Lerp(0, HaraMax, sliderValue / 100f);
            maid.SetProp(MPN.Hara, val);
            maid.body0.FixVisibleFlag(false);
            maid.AllProcPropSeqStart();
        }

        public static void Reset(Maid maid)
        {
            if (!IsValid(maid)) return;
            if (_originalHara >= 0)
            {
                maid.SetProp(MPN.Hara, _originalHara);
                maid.body0.FixVisibleFlag(false);
                maid.AllProcPropSeqStart();
            }
            _originalHara = -1;
            _savedMaid    = null;
        }

        public static bool IsValid(Maid maid)
            => maid != null && maid.body0 != null;
    }
}
