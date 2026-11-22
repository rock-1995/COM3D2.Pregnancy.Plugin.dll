using COM3D2.Pregnancy.Plugin;
using CM3D2.ExternalSaveData.Managed;
using Callbacks = CM3D2.ExternalSaveData.Managed.GameMainCallbacks;
using System.Xml;

static class Program
{
    static int checks;
    static void Check(bool condition, string description)
    {
        checks++;
        if (!condition) throw new Exception("FAILED: " + description);
    }
    static void Equal(AppearanceValues actual, float belly, float body, string description)
        => Check(Math.Abs(actual.Belly - belly) < 0.000001f && Math.Abs(actual.Body - body) < 0.000001f, description);
    static void Load(int slot)
    {
        PregnancyAppearance.ClearPreviews();
        Callbacks.Deserialize.Invoke(GameMain.Instance, slot);
    }
    static void Save(int slot) => Callbacks.Serialize.Invoke(GameMain.Instance, slot, "test");

    static void Main(string[] args)
    {
        GameMain.Instance.Root = Path.GetFullPath(args.Length == 0
            ? Path.Combine(AppContext.BaseDirectory, "fixtures-" + Guid.NewGuid().ToString("N")) : args[0]);
        Directory.CreateDirectory(GameMain.Instance.Root);
        var maid = new Maid("same-maid-guid");
        GameMain.Instance.CharacterMgr.Maids.Add(maid);
        ExSaveData.DummyInitialize(GameMain.Instance);
        Load(1);
        ExSaveDataBridge.Set(maid, "isPregnant", "true");
        ExSaveDataBridge.Set(maid, "progress", "0.5");
        ExSaveDataBridge.Set(maid, "birthCount", "2");
        Equal(PregnancyAppearance.Get(maid), .5f, .5f, "legacy migration uses pregnancy and births");
        Check(ExSaveDataBridge.Get(maid, PregnancyAppearance.BellyKey) == "0.5", "migration creates editable belly key");
        Check(ExSaveDataBridge.Get(maid, PregnancyAppearance.BodyKey) == "0.5", "migration creates editable body key");
        PregnancyAppearance.Set(maid, 0, .8f);
        Equal(PregnancyAppearance.Get(maid), 0, .8f, "explicit zero and independent body are retained");
        Equal(PregnancyAppearance.Get(new Maid(maid.status.guid)), 0, .8f, "committed values follow GUID rather than object identity");
        Save(1);

        Load(2);
        Equal(PregnancyAppearance.Get(maid), 0, 0, "same object in empty second slot does not retain first slot data");
        PregnancyAppearance.Set(maid, .9f, .1f);
        Save(2);
        Load(1);
        Equal(PregnancyAppearance.Get(maid), 0, .8f, "slot one restores its own appearance");
        Load(2);
        Equal(PregnancyAppearance.Get(maid), .9f, .1f, "slot two restores its different appearance");

        PregnancyAppearance.BeginPreview(maid);
        PregnancyAppearance.SetPreview(maid, .3f, .4f);
        PregnancyAppearance.BeginPreview(maid);
        Equal(PregnancyAppearance.Get(maid), .3f, .4f, "begin preview is idempotent");
        Equal(PregnancyAppearance.ReadCommitted(maid), .9f, .1f, "preview does not change stored values");
        var neverShown = new Maid("unshown-stock-maid");
        GameMain.Instance.CharacterMgr.Maids.Add(neverShown);
        ExSaveDataBridge.Set(neverShown, "isPregnant", "true");
        ExSaveDataBridge.Set(neverShown, "progress", "0.25");
        PregnancyAppearance.EnsureCommittedForSave();
        Check(ExSaveDataBridge.Get(neverShown, PregnancyAppearance.BellyKey) == "0.25" &&
            ExSaveDataBridge.Get(neverShown, PregnancyAppearance.BodyKey) == "0.05",
            "slot save materializes appearance for stock maid never shown in scene");
        Equal(PregnancyAppearance.ReadCommitted(maid), .9f, .1f, "save preparation never commits current preview");
        Save(2);
        PregnancyAppearance.CancelPreview(maid);
        Equal(PregnancyAppearance.Get(maid), .9f, .1f, "cancel restores committed appearance");
        Load(2);
        Equal(PregnancyAppearance.Get(maid), .9f, .1f, "save during preview excludes preview values");
        PregnancyAppearance.SetPreview(maid, .3f, .4f);
        PregnancyAppearance.CommitPreview(maid);
        Equal(PregnancyAppearance.ReadCommitted(maid), .3f, .4f, "confirm stores both values");
        Save(2);
        PregnancyAppearance.SetPreview(maid, .8f, .9f);
        Load(1);
        Equal(PregnancyAppearance.Get(maid), 0, .8f, "load boundary discards old preview");

        // Exercise the locally installed ExSaveData binary's serializer and
        // loader with synthetic game API objects. User edits appear on reload.
        var path = ExSaveData.makeXmlFilename(GameMain.Instance, 2);
        var xml = new XmlDocument(); xml.Load(path);
        var node = xml.SelectSingleNode($"//maid[@guid='{maid.status.guid}']/plugins/plugin[@name='{PregnancyPlugin.PluginGuid}']");
        Check(node != null, "external save stores appearance inside slot maid GUID namespace");
        var property = node.SelectSingleNode($"prop[@name='{PregnancyAppearance.BellyKey}']");
        if (property == null) property = node.SelectSingleNode($"property[@name='{PregnancyAppearance.BellyKey}']");
        Check(property != null, "external appearance property is addressable");
        property.Attributes["value"].Value = "0.65";
        xml.Save(path);
        Load(2);
        Equal(PregnancyAppearance.Get(maid), .65f, .4f, "external XML edit is read on next slot load");

        ExSaveData.Remove(maid, PregnancyPlugin.PluginGuid, PregnancyAppearance.BodyKey);
        ExSaveDataBridge.Set(maid, "isPregnant", "true");
        ExSaveDataBridge.Set(maid, "progress", "0.5");
        ExSaveDataBridge.Set(maid, "birthCount", "1");
        Equal(PregnancyAppearance.Get(maid), .65f, .3f, "partial migration preserves independently edited existing field");
        PregnancyAppearance.RefreshFromSimulation(maid);
        Equal(PregnancyAppearance.Get(maid), .5f, .3f, "simulation event refreshes both final appearance values");
        ExSaveDataBridge.Set(maid, "isPregnant", "false");
        PregnancyAppearance.RefreshFromSimulation(maid);
        Equal(PregnancyAppearance.Get(maid), 0, .2f, "birth history remains while nonpregnant belly is zero");
        Console.WriteLine($"Appearance tests passed: {checks}; ExternalSaveData: {typeof(ExSaveData).Assembly.GetName().Version}");
    }
}
