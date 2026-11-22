// Test API only. Matching assembly/type/member identities let the locally
// installed ExternalSaveData binary run against synthetic maids and paths.
namespace MaidStatus
{
    public class Status
    {
        public string guid { get; set; }
        public string lastName { get; set; } = "Test";
        public string firstName { get; set; } = "Maid";
        public string creationTime { get; set; } = "test";
    }
}
public class Maid : UnityEngine.Object
{
    public MaidStatus.Status status { get; } = new();
    public Maid(string guid) { status.guid = guid; }
    public static List<MaidProp> DeserializePropPre(BinaryReader reader) => throw new NotSupportedException();
    public static MaidParts.PartsColor[] DeserializeMultiColorPre(BinaryReader reader) => throw new NotSupportedException();
}
public class CharacterMgr
{
    public readonly List<Maid> Maids = new();
    public int GetStockMaidCount() => Maids.Count;
    public Maid GetStockMaid(int index) => Maids[index];
}
public class GameMain
{
    public static GameMain Instance { get; } = new();
    public CharacterMgr CharacterMgr { get; } = new();
    public string Root;
    public string MakeSavePathFileName(int slot) => Path.Combine(Root, $"SaveData{slot:000}.save");
}
