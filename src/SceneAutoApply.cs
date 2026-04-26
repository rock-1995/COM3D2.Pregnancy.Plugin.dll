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
            // ① 先に MarkActive だけ行う
            //    これ以降に LoadSkinMesh_R が呼ばれたとき
            //    パッチが自動で変形を適用する
            var cm = GameMain.Instance?.CharacterMgr;
            if (cm != null)
            {
                int cnt = cm.GetMaidCount();
                for (int i = 0; i < cnt; i++)
                {
                    Maid m = cm.GetMaid(i);
                    if (m == null) continue;
                    if (!PregnancyManager.GetPregnant(m)) continue;
                    float progress = PregnancyManager.GetProgress(m);
                    if (progress <= 0f) continue;
                    BellyMorphController.MarkActive(m, progress);
                }
            }

            // ② メッシュが揃うまで待つ
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.15f);

            // ③ ① より前にロード済みのメッシュに手動で適用
            //    （LoadSkinMesh_R パッチが間に合わなかった分の補完）
            if (cm != null)
            {
                int cnt = cm.GetMaidCount();
                for (int i = 0; i < cnt; i++)
                {
                    Maid m = cm.GetMaid(i);
                    if (!BellyMorphController.IsActive(m)) continue;
                    float progress = BellyMorphController.GetActiveProgress(m);
                    BellyMorphController.ApplyProgress(m, progress);
                }
            }
        }
    }
}
