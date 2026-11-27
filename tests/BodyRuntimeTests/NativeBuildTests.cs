using COM3D2.Pregnancy.Plugin;

static class NativeBuildTests
{
    public static void Run(Action<bool, string> check)
    {
        bool Near(float a, float b) => Math.Abs(a - b) < .00001;
        var maid = new Maid(13, 35) { Births = 99 };
        string saved = maid.SaveShape();
        check(BodyChanges.PrepareNative(maid, .5f), "first native build reports a geometry change");
        check(maid.body0.Updates == 0, "preparing appearance does not apply a second geometry pass");
        maid.body0.VertexMorph_FromProcItem("RegMeet", .13f);
        maid.body0.BoneMorph_FromProcItem("west", .35f);
        maid.body0.UpdateInfinityColor(MaidParts.PARTS_COLOR.NIPPLE);
        maid.body0.UpdateInfinityColor(MaidParts.PARTS_COLOR.EYE_L);
        int writes = maid.body0.Updates, colors = maid.body0.ColorUpdates;
        BodyChanges.CompleteNative(maid);
        check(Near(maid.body0.Values["RegMeet"], .31f) && Near(maid.body0.Values["west"], .47f),
            "native input uses the independent body factor, irrespective of birth history");
        check(maid.body0.Updates == writes && maid.body0.ColorUpdates == colors,
            "native processing is not replayed at completion");
        check(!BodyChanges.PrepareNative(maid, .5f), "unchanged clothing build has no body change");
        BodyChanges.CompleteNative(maid);
        check(maid.body0.Updates == writes && maid.body0.ColorUpdates == colors,
            "clothing-only build does not replay untouched but already current body inputs or colors");

        BodyChanges.PrepareNative(maid, 1f);
        maid.body0.VertexMorph_FromProcItem("RegMeet", .13f);
        writes = maid.body0.Updates;
        BodyChanges.CompleteNative(maid);
        check(maid.body0.Updates == writes + 1 && Near(maid.body0.Values["west"], .59f),
            "completion supplies only changed sliders the native build did not process");
        check(Near(maid.body0.Values["RegMeet"], .48f), "processed native input receives one overlay");

        BodyChanges.PrepareNative(maid, 0f);
        maid.body0.VertexMorph_FromProcItem("RegMeet", .13f);
        BodyChanges.CompleteNative(maid);
        check(Near(maid.body0.Values["RegMeet"], .13f) && Near(maid.body0.Values["west"], .35f)
            && maid.body0.LastColor == maid.Parts.Original, "zero body factor restores untouched and processed geometry plus colors");
        writes = maid.body0.Updates; colors = maid.body0.ColorUpdates;
        check(!BodyChanges.PrepareNative(maid, 0f), "zero factor restoration is completed once");
        BodyChanges.CompleteNative(maid);
        check(maid.body0.Updates == writes && maid.body0.ColorUpdates == colors, "later zero-factor builds do not restore again");
        check(maid.SaveShape() == saved, "native build never changes serializable source values");

        BodyChanges.PrepareNative(maid, .5f);
        maid.body0.VertexMorph_FromProcItem("RegMeet", .20f);
        BodyChanges.CompleteNative(maid);
        check(Near(maid.body0.Values["RegMeet"], .38f), "completion respects a native caller's live argument already processed in this build");

        writes = maid.body0.Updates;
        check(!BodyChanges.PrepareNative(maid, .5f), "an unchanged native source preserves an external live input during clothing-only work");
        BodyChanges.CompleteNative(maid);
        check(maid.body0.Updates == writes && Near(maid.body0.Values["RegMeet"], .38f),
            "untouched external live input is not replayed from the serialized source");
        maid.GetProp(MPN.west).value = 40;
        BodyChanges.PrepareNative(maid, .5f);
        BodyChanges.CompleteNative(maid);
        check(maid.body0.Updates == writes + 1 && Near(maid.body0.Values["RegMeet"], .38f)
            && Near(maid.body0.Values["west"], .52f), "a source edit rebases that slider while preserving another slider's live input");
        maid.GetProp(MPN.west).value = 35;
        BodyChanges.PrepareNative(maid, .5f);
        BodyChanges.CompleteNative(maid);
        writes = maid.body0.Updates;
        int colorLimit = PregnancyPlugin.CfgNippleColorLimit.Value;
        PregnancyPlugin.CfgNippleColorLimit.Value = 25;
        check(!BodyChanges.PrepareNative(maid, .5f), "color-only configuration changes do not rebase native geometry");
        BodyChanges.PrepareNative(maid, .5f); // Multiple UI requests before completion.
        BodyChanges.CompleteNative(maid);
        check(maid.body0.Updates == writes && Near(maid.body0.Values["RegMeet"], .38f),
            "repeated pending color requests preserve the live native geometry");
        PregnancyPlugin.CfgNippleColorLimit.Value = colorLimit;
        BodyChanges.PrepareNative(maid, .5f);
        BodyChanges.CompleteNative(maid);
        writes = maid.body0.Updates; colors = maid.body0.ColorUpdates;
        maid.body0.Values.Clear();
        BodyChanges.PrepareNative(maid, .5f);
        BodyChanges.CompleteNative(maid, rebuildBody: true);
        check(maid.body0.Updates == writes + 2 && maid.body0.ColorUpdates == colors + 2,
            "a genuinely rebuilt body receives unchanged native targets and material colors");
        check(Near(maid.body0.Values["RegMeet"], .31f), "rebuilding uses the saved baseline instead of previously deformed output");

        writes = maid.body0.Updates;
        maid.GetProp(MPN.west).value = 40;
        BodyChanges.PrepareNative(maid, .5f);
        BodyChanges.CompleteNative(maid);
        check(maid.body0.Updates == writes + 1 && Near(maid.body0.Values["west"], .52f),
            "source property edit updates only the affected untouched target");

        check(!maid.body0.Values.ContainsKey("Hara"), "preset Hara difference is excluded from pregnancy appearance");
        BodyChanges.PrepareNative(maid, 1f);
        maid.body0.VertexMorph_FromProcItem("Hara", .63f);
        BodyChanges.CompleteNative(maid);
        check(Near(maid.body0.Values["Hara"], .63f) && maid.GetProp(MPN.Hara).value == 17,
            "native Hara remains untouched even when it appears in both presets");
        BodyChanges.Reset(maid);
        check(Near(maid.body0.Values["Hara"], .63f), "reset does not write the native Hara channel");
        BodyChanges.Forget(maid);

        var temporary = new Maid(13, 35);
        var prop = temporary.GetProp(MPN.RegMeet);
        prop.temp_value = 60;
        prop.boTempDut = true;
        BodyChanges.PrepareNative(temporary, .5f);
        prop.boTempDut = false; // Native ProcItem clears dirty before invoking its morph.
        temporary.body0.VertexMorph_FromProcItem("RegMeet", .60f);
        prop.boTempExecuted = true;
        BodyChanges.CompleteNative(temporary);
        check(Near(temporary.body0.Values["RegMeet"], .78f) && prop.value == 13 && prop.temp_value == 60,
            "native temporary appearance receives an overlay without replacing either serialized baseline");
        prop.boTempExecuted = false;
        prop.boDut = true;
        BodyChanges.PrepareNative(temporary, .5f);
        temporary.body0.VertexMorph_FromProcItem("RegMeet", .13f);
        prop.boDut = false;
        BodyChanges.CompleteNative(temporary);
        check(Near(temporary.body0.Values["RegMeet"], .31f), "leaving a temporary appearance restores the permanent native baseline once");
        BodyChanges.Forget(temporary);

        var sourceKind = new Maid(13, 35);
        prop = sourceKind.GetProp(MPN.RegMeet);
        prop.temp_value = 13;
        prop.boTempExecuted = true;
        BodyChanges.PrepareNative(sourceKind, .5f);
        sourceKind.body0.VertexMorph_FromProcItem("RegMeet", .20f);
        BodyChanges.CompleteNative(sourceKind);
        prop.boTempExecuted = false;
        BodyChanges.PrepareNative(sourceKind, .5f);
        BodyChanges.CompleteNative(sourceKind);
        check(Near(sourceKind.body0.Values["RegMeet"], .31f),
            "switching temporary/permanent source rebases even when their saved numeric values match");
        int forgotten = NativeAppearanceRuntime.Forgotten;
        typeof(Patch_BodyChangeUnload).GetMethod("Prefix", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .Invoke(null, new object[] { sourceKind });
        check(NativeAppearanceRuntime.Forgotten == forgotten + 1
            && BodyChanges.RenderColorTexture(sourceKind.Parts,MaidParts.PARTS_COLOR.NIPPLE) == sourceKind.Parts.Original,
            "Maid.Uninit retires the complete runtime transaction, including its body palette");
    }
}
