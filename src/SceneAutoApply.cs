using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public class SceneAutoApply : MonoBehaviour
    {
        float _nextScanTime;

        void Update()
        {
            if (PregnancyPlugin.CfgMorphTriggerMode == null) return;
            if (PregnancyPlugin.CfgMorphTriggerMode.Value == MorphTriggerMode.ManualOnly) return;
            if (Time.unscaledTime < _nextScanTime) return;

            _nextScanTime = Time.unscaledTime + 1f;
            AttachModeObservers();
        }

        void AttachModeObservers()
        {
            CharacterMgr cm = GameMain.Instance?.CharacterMgr;
            if (cm == null) return;
            int cnt = cm.GetMaidCount();
            for (int i = 0; i < cnt; i++)
            {
                Maid maid = cm.GetMaid(i);
                if (maid == null || maid.body0 == null) continue;

                if (PregnancyPlugin.CfgMorphTriggerMode.Value == MorphTriggerMode.VisibilityChange)
                    BellyMorphController.EnsureVisibilityObservers(maid);
            }
        }
    }
}
