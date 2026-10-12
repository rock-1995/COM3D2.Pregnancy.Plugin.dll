namespace COM3D2.Pregnancy.Plugin;
// Geometry is tested here with the body-slider native engine at a boundary.
// The real BodyChanges implementation is exercised separately by BodyRuntimeTests.
static class BodyChanges
{
    internal class State { public Maid Maid; }
    internal static HashSet<Maid> History = new(), Pending = new();
    internal static Dictionary<Maid, float> Progress = new();
    internal static int Applied, Resets;
    internal static bool HasHistory(Maid m) => History.Contains(m);
    internal static State Prepare(Maid m, float p)
    { Progress[m] = p; return Pending.Remove(m) ? new State { Maid=m } : null; }
    internal static void Apply(State state) { Applied++; }
    internal static void Reset(Maid m) { Resets++; }
}
public static partial class BellyMorphController
{
    static void RunBodyRoutingTests()
    {
        Shape=new();var (m,body,cloth)=LiveFixture();
        BodyChanges.Pending.Add(m);ApplyProgress(m,1);
        Check(BodyChanges.Applied==1 && BodyChanges.Progress[m]==1,"body update is connected to Apply Belly");
        var original=FindRecord(m,body).OrigVerts.ToArray();
        m.Pregnant=false;m.Progress=.003f;BodyChanges.History.Add(m);BodyChanges.Pending.Add(m);
        EnsureMonitor(m).RequestVisibilityApply(m.GetHashCode());Tick(m,3);
        Check(BodyChanges.Applied==2 && BodyChanges.Progress[m]==0,"postpartum visibility uses birth history with zero current-pregnancy contribution");
        Check(!_activeProgress.ContainsKey(m.GetHashCode()),"postpartum does not retain pregnant belly");
        Same(body.sharedMesh.vertices,original,"postpartum belly reset");
        BodyChanges.Pending.Add(m);ApplyProgress(m,0);
        Check(BodyChanges.Applied==3,"body changes run even when belly progress is zero");
        int before=BodyChanges.Resets;Reset(m);
        Check(BodyChanges.Resets==before+1,"Reset Belly restores body overlay too");
        Console.WriteLine($"PASS: {tests} body/belly routing and postpartum assertions.");
    }
}
