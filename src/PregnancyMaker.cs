using System;
using System.Globalization;
using HarmonyLib;
using I2.Loc;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    // The two controls belong to the existing body-slider panel. They do not
    // become MaidProps, so native presets cannot serialize them as Hara etc.
    internal sealed class PregnancyMaker : MonoBehaviour
    {
        sealed class Row
        {
            internal GameObject Root;
            internal UISlider Slider;
            internal UILabel Value;
        }

        static readonly BepInEx.Logging.ManualLogSource Log =
            BepInEx.Logging.Logger.CreateLogSource("PregnancyMaker");
        Maid _maid;
        Row _belly, _body;
        bool _preview, _syncing;

        internal static bool IsBodyPage(SceneEdit.SPartsType parts)
        {
            SceneEditInfo.CCateNameType body;
            return parts != null && parts.m_eType == SceneEditInfo.CCateNameType.EType.Slider
                && SceneEditInfo.m_dicPartsTypePair.TryGetValue(MPN.sintyou, out body)
                && body.m_eMenuCate == SceneEditInfo.EMenuCategory.身体
                && parts.m_ePartsType == body.m_ePartsType;
        }

        internal static void Clear(SceneEdit scene)
        {
            var maker = scene.GetComponent<PregnancyMaker>();
            if (maker != null) maker.ClearRows();
        }

        internal static void Show(SceneEdit scene, SceneEdit.SPartsType parts, object panel)
        {
            if (!IsBodyPage(parts) || scene.maid == null) return;
            var maker = scene.GetComponent<PregnancyMaker>() ?? scene.gameObject.AddComponent<PregnancyMaker>();
            try
            {
                var grid = (GameObject)AccessTools.Field(panel.GetType(), "goGrid").GetValue(panel);
                maker.Open(scene.maid, grid);
                grid.GetComponent<UIGrid>().Reposition();
                grid.GetComponentInParent<UIScrollView>().ResetPosition();
            }
            catch (Exception e)
            {
                maker.ClearRows();
                Log.LogWarning("Could not add pregnancy controls to the native body panel: " + e.Message);
            }
        }

        void Open(Maid maid, GameObject grid)
        {
            if (_maid != maid)
            {
                Finish(false);
                _maid = maid;
            }
            ClearRows();
            _belly = CreateRow(grid, "zz_Pregnancy_Belly", "Belly shape progress");
            _body = CreateRow(grid, "zz_Pregnancy_Body", "Body change progress");
            RefreshValues();
            EventDelegate.Add(_belly.Slider.onChange, () => Changed(true));
            EventDelegate.Add(_body.Slider.onChange, () => Changed(false));
        }

        static Row CreateRow(GameObject grid, string name, string title)
        {
            var prefab = Resources.Load("SceneEdit/MainMenu/Prefab/Slider") as GameObject;
            if (prefab == null) throw new InvalidOperationException("Native slider prefab was not found.");
            var root = NGUITools.AddChild(grid, prefab);
            try
            {
                root.name = name;
                var row = new Row {
                    Root = root,
                    Slider = root.GetComponentsInChildren<UISlider>(true)[0],
                    Value = root.transform.Find("CategoryTitle/Number/Value").GetComponent<UILabel>()
                };
                var label = root.transform.Find("CategoryTitle/Name").GetComponent<UILabel>();
                var localize = label.GetComponent<Localize>();
                if (localize != null) localize.enabled = false;
                label.text = title;
                // Native property callbacks require a real MPN. These controls
                // have their own callbacks and independent saved values.
                foreach (var button in root.GetComponentsInChildren<ButtonEdit>(true))
                    UnityEngine.Object.DestroyImmediate(button);
                row.Slider.onChange.Clear();
                row.Slider.numberOfSteps = 101;
                return row;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        void Changed(bool bellyChanged)
        {
            if (_syncing || _maid == null || _belly == null || _body == null) return;
            var current = PregnancyAppearance.Get(_maid);
            // Touching one slider preserves the other value's full precision.
            float belly = bellyChanged ? Mathf.Round(_belly.Slider.value * 100f) / 100f : current.Belly;
            float body = bellyChanged ? current.Body : Mathf.Round(_body.Slider.value * 100f) / 100f;
            if (belly == current.Belly && body == current.Body) return;
            if (!_preview)
            {
                PregnancyAppearance.BeginPreview(_maid);
                _preview = true;
            }
            PregnancyAppearance.SetPreview(_maid, belly, body);
            RefreshValues();
            NativeAppearanceRuntime.RequestApply(_maid);
        }

        internal void RefreshValues()
        {
            if (_maid == null || _belly == null || _body == null) return;
            var values = PregnancyAppearance.Get(_maid);
            _syncing = true;
            try
            {
                SetRow(_belly, values.Belly);
                SetRow(_body, values.Body);
            }
            finally { _syncing = false; }
        }

        static void SetRow(Row row, float value)
        {
            row.Slider.value = value;
            row.Value.text = Mathf.RoundToInt(value * 100f).ToString(CultureInfo.InvariantCulture);
        }

        internal void Finish(bool commit)
        {
            if (!_preview) return;
            _preview = false;
            if (_maid == null) return;
            if (commit) PregnancyAppearance.CommitPreview(_maid);
            else
            {
                PregnancyAppearance.CancelPreview(_maid);
                if (GameMain.Instance != null && !GameMain.Instance.isQuitting) NativeAppearanceRuntime.RequestApply(_maid);
            }
        }

        void ClearRows()
        {
            if (_belly != null && _belly.Root != null) UnityEngine.Object.DestroyImmediate(_belly.Root);
            if (_body != null && _body.Root != null) UnityEngine.Object.DestroyImmediate(_body.Root);
            _belly = _body = null;
        }

        void OnDestroy() { Finish(false); }
    }

    [HarmonyPatch(typeof(SceneEdit), "UpdatePanel_MenuItem")]
    static class PregnancyMakerMenuPatch
    {
        static void Prefix(SceneEdit __instance) { PregnancyMaker.Clear(__instance); }
        static void Postfix(SceneEdit __instance, SceneEdit.SPartsType f_pt, object ___m_Panel_SliderItem)
        { PregnancyMaker.Show(__instance, f_pt, ___m_Panel_SliderItem); }
    }

    [HarmonyPatch(typeof(SceneEdit), "UpdatePanel_PartsType")]
    static class PregnancyMakerCategoryPatch
    {
        static void Prefix(SceneEdit __instance) { PregnancyMaker.Clear(__instance); }
    }

    [HarmonyPatch(typeof(SceneEdit), "UpdateSliders")]
    static class PregnancyMakerValuesPatch
    {
        static void Postfix(SceneEdit __instance)
        {
            var maker = __instance.GetComponent<PregnancyMaker>();
            if (maker != null) maker.RefreshValues();
        }
    }

    // OnEditOk may still open a confirmation dialog. Its Cancel only returns
    // to editing; the actual accepted edit enters OnEndDlgOk.
    [HarmonyPatch(typeof(SceneEdit), "OnEndDlgOk")]
    static class PregnancyMakerCommitPatch
    {
        static void Postfix(SceneEdit __instance)
        {
            var maker = __instance.GetComponent<PregnancyMaker>();
            if (maker != null) maker.Finish(true);
        }
    }

    [HarmonyPatch(typeof(SceneEdit), "OnDestroy")]
    static class PregnancyMakerExitPatch
    {
        static void Prefix(SceneEdit __instance)
        {
            var maker = __instance.GetComponent<PregnancyMaker>();
            if (maker != null) maker.Finish(false);
        }
    }
}
