using System.Collections.Generic;
using MaidStatus;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin;

public class MaidListUI : MonoBehaviour
{
	private struct MaidRow
	{
		public string Name;

		public string Status;

		public bool Pregnant;
	}

	private bool _visible;

	private Rect _win = new Rect(20f, 80f, 340f, 400f);

	private int _winId;

	private Vector2 _scroll = Vector2.zero;

	private List<MaidRow> _rows = new List<MaidRow>();

	private void Awake()
	{
		_winId = GetHashCode();
	}

	private void Update()
	{
		if (Input.GetKeyDown(PregnancyPlugin.CfgMaidListKey.Value))
		{
			_visible = !_visible;
			if (_visible)
			{
				RefreshData();
			}
		}
	}

	private void RefreshData()
	{
		_rows.Clear();
		CharacterMgr characterMgr = GameMain.Instance?.CharacterMgr;
		if (characterMgr == null)
		{
			return;
		}
		FertilityCycleMode cycleMode = PregnancyManager.GetCycleMode();
		bool flag = PregnancyManager.IsCyclicMode(cycleMode);
		int cycleLength = PregnancyManager.GetCycleLength(cycleMode);
		int num = ((PregnancyPlugin.CfgPregnancyWeeks != null) ? (PregnancyPlugin.CfgPregnancyWeeks.Value * 7) : 280);
		int stockMaidCount = characterMgr.GetStockMaidCount();
		for (int i = 0; i < stockMaidCount; i++)
		{
			Maid stockMaid = characterMgr.GetStockMaid(i);
			if (!(stockMaid == null))
			{
				bool pregnant = PregnancyManager.GetPregnant(stockMaid);
				string status;
				if (pregnant)
				{
					int num2 = Mathf.RoundToInt(PregnancyManager.GetProgress(stockMaid) * (float)num);
					status = $"Pregnant {num2}/{num}d";
				}
				else
				{
					float cycleProgress = PregnancyManager.EnsureCycleProgress(stockMaid);
					int num3 = (flag ? cycleLength : 28);
					int cycleDay = PregnancyManager.GetCycleDay(cycleProgress, num3);
					status = $"Cycle {cycleDay}/{num3}d";
				}
				_rows.Add(new MaidRow
				{
					Name = GetMaidDisplayName(stockMaid),
					Status = status,
					Pregnant = pregnant
				});
			}
		}
	}

	private void OnGUI()
	{
		if (_visible)
		{
			_win = GUI.Window(_winId, _win, DrawWindow, "Pregnancy - Maid Status");
		}
	}

	private void DrawWindow(int id)
	{
		GUI.DragWindow(new Rect(0f, 0f, _win.width, 20f));
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
		float num = 8f;
		float num2 = 26f;
		float num3 = _win.width - 16f;
		float num4 = 150f;
		float width = num3 - num4 - 4f;
		GUI.Label(new Rect(num, num2, num4, 18f), "Name");
		GUI.Label(new Rect(num + num4, num2, width, 18f), "Status");
		num2 += 20f;
		float num5 = 22f;
		float num6 = _win.height - num2 - 8f;
		float a = num5 * (float)_rows.Count;
		_scroll = GUI.BeginScrollView(new Rect(num, num2, num3, num6), _scroll, new Rect(0f, 0f, num3 - 18f, Mathf.Max(a, num6)));
		float num7 = 0f;
		for (int i = 0; i < _rows.Count; i++)
		{
			MaidRow maidRow = _rows[i];
			if (maidRow.Pregnant)
			{
				GUI.color = new Color(1f, 0.85f, 0.85f);
			}
			GUI.Label(new Rect(0f, num7, num4, num5), maidRow.Name);
			GUI.Label(new Rect(num4, num7, width, num5), maidRow.Status);
			GUI.color = Color.white;
			num7 += num5;
		}
		GUI.EndScrollView();
	}

	private static string GetMaidDisplayName(Maid maid)
	{
		try
		{
			Status status = maid?.status;
			if (status == null)
			{
				return "(null)";
			}
			string text = (status.lastName + " " + status.firstName).Trim();
			return (text.Length > 0) ? text : "(unnamed)";
		}
		catch
		{
			return "(error)";
		}
	}
}
