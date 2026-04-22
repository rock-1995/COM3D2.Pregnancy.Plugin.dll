using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D2.Pregnancy.Plugin
{
    public class SceneAutoApply : MonoBehaviour
    {
        void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            => StartCoroutine(ApplyAfterDelay());

        IEnumerator ApplyAfterDelay()
        {
            yield return null;
            yield return null;

            var cm = GameMain.Instance?.CharacterMgr;
            if (cm == null) yield break;

            int cnt = cm.GetMaidCount();
            for (int i = 0; i < cnt; i++)
            {
                Maid m = cm.GetMaid(i);
                if (m == null || m.body0 == null) continue;
                if (!PregnancyManager.GetPregnant(m)) continue;
                float progress = PregnancyManager.GetProgress(m);
                if (progress <= 0f) continue;
                int val = (int)Mathf.Lerp(0, BellyMorphController.HaraMax, progress);
                m.SetProp(MPN.Hara, val);
                m.body0.FixVisibleFlag(false);
                m.AllProcPropSeqStart();
            }
        }
    }
}
