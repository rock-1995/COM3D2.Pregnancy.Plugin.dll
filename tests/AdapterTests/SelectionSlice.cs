using COM3D2.Pregnancy.Plugin.Growth;
namespace COM3D2.Pregnancy.Plugin;
class PregnancyUI
{
    internal List<Maid> _maids=new();
    internal Maid _curMaid;
    internal VtxSettings _shape;
    int _sel; bool _curPreg,_dropOpen; float _curProg,_curCycle;
    Dictionary<string,string> _shapeText=new();
    Dictionary<string,float> _shapeLastValue=new();
    internal void Select(int i)=>SelectMaid(i);
void SelectMaid(int i)
        {
            _sel = i;
            _curMaid = _maids[i];
            _shape = BellyMorphController.GetAppliedShape(_curMaid);
            _shapeText.Clear(); _shapeLastValue.Clear();
            _curPreg = PregnancyManager.GetPregnant(_curMaid);
            _curProg = PregnancyManager.GetProgress(_curMaid);
            _curCycle = PregnancyManager.EnsureCycleProgress(_curMaid);
            _dropOpen = false;
        }
public static void TriggerApplyBelly(Maid maid, float progress)
        {
            if (maid == null) return;

            PregnancyManager.CaptureCurrentBellySettings();
            BellyMorphController.ApplyProgress(maid, progress);
        }
}
