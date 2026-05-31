using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public class MaidListUI : MonoBehaviour
    {
        struct MaidRow
        {
            public string Name;
            public string Status;
            public bool Pregnant;
        }

        bool _visible = false;
        Rect _win = new Rect(20f, 80f, 340f, 400f);
        int _winId;
        Vector2 _scroll = Vector2.zero;
        List<MaidRow> _rows = new List<MaidRow>();

        void Awake()
        {
            _winId = GetHashCode();
        }

        void Update()
        {
            if (Input.GetKeyDown(PregnancyPlugin.CfgMaidListKey.Value))
            {
                _visible = !_visible;
                if (_visible)
                    RefreshData();
            }
        }

        void RefreshData()
        {
            _rows.Clear();
            var cm = GameMain.Instance?.CharacterMgr;
            if (cm == null) return;

            FertilityCycleMode mode = PregnancyManager.GetCycleMode();
            bool cyclic = PregnancyManager.IsCyclicMode(mode);
            int cycleLength = PregnancyManager.GetCycleLength(mode);
            int totalDays = PregnancyPlugin.CfgPregnancyWeeks != null
                ? PregnancyPlugin.CfgPregnancyWeeks.Value * 7 : 280;

            int count = cm.GetStockMaidCount();
            for (int i = 0; i < count; i++)
            {
                Maid maid = cm.GetStockMaid(i);
                if (maid == null) continue;

                bool preg = PregnancyManager.GetPregnant(maid);
                string statusStr;

                if (preg)
                {
                    float prog = PregnancyManager.GetProgress(maid);
                    int curDay = Mathf.RoundToInt(prog * totalDays);
                    statusStr = string.Format("Pregnant {0}/{1}d", curDay, totalDays);
                }
                else
                {
                    float cp = PregnancyManager.EnsureCycleProgress(maid);
                    int displayLength = cyclic ? cycleLength : 28;
                    int cd = PregnancyManager.GetCycleDay(cp, displayLength);
                    statusStr = string.Format("Cycle {0}/{1}d", cd, displayLength);
                }

                _rows.Add(new MaidRow
                {
                    Name = GetMaidDisplayName(maid),
                    Status = statusStr,
                    Pregnant = preg
                });
            }
        }

        void OnGUI()
        {
            if (!_visible) return;
            _win = GUI.Window(_winId, _win, DrawWindow, "Pregnancy - Maid Status");
        }

        void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0, 0, _win.width, 20f));

            if (GUI.Button(new Rect(_win.width - 26f, 2f, 22f, 18f), "X"))
            {
                _visible = false;
                return;
            }

            if (_rows.Count == 0)
            {
                GUI.Label(new Rect(8f, 26f, _win.width - 16f, 22f), "No maids found.");
                return;
            }

            float x = 8f, y = 26f;
            float w = _win.width - 16f;
            float nameW = 150f;
            float statusW = w - nameW - 4f;

            GUI.Label(new Rect(x, y, nameW, 18f), "Name");
            GUI.Label(new Rect(x + nameW, y, statusW, 18f), "Status");
            y += 20f;

            float rowH = 22f;
            float viewH = _win.height - y - 8f;
            float contentH = rowH * _rows.Count;

            _scroll = GUI.BeginScrollView(
                new Rect(x, y, w, viewH),
                _scroll,
                new Rect(0, 0, w - 18f, Mathf.Max(contentH, viewH)));

            float ry = 0f;
            for (int i = 0; i < _rows.Count; i++)
            {
                MaidRow row = _rows[i];
                if (row.Pregnant)
                    GUI.color = new Color(1f, 0.85f, 0.85f);
                GUI.Label(new Rect(0f, ry, nameW, rowH), row.Name);
                GUI.Label(new Rect(nameW, ry, statusW, rowH), row.Status);
                GUI.color = Color.white;
                ry += rowH;
            }

            GUI.EndScrollView();
        }

        static string GetMaidDisplayName(Maid maid)
        {
            try
            {
                var status = maid?.status;
                if (status == null) return "(null)";
                string name = ((status.lastName ?? "") + " " + (status.firstName ?? "")).Trim();
                return name.Length > 0 ? name : "(unnamed)";
            }
            catch { return "(error)"; }
        }
    }
}
