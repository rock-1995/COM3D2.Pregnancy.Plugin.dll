using COM3D2.Pregnancy.Plugin;

static class NativeColorTests
{
    public static void Run(Action<bool, string> check)
    {
        var nipple = MaidParts.PARTS_COLOR.NIPPLE;
        var maid = new Maid(13, 35);
        string saved = maid.SaveShape();
        BodyChanges.PrepareNative(maid, .25f);
        BodyChanges.CompleteNative(maid);
        int existingMaterialBrightness = maid.body0.RenderedColors[nipple].m_nMainBrightness;
        check(existingMaterialBrightness == 268, "initial nipple render receives independent 25% body progress");

        // InfinityColorTextureCache.AddTexture colors just that new texture.
        // Its palette read cannot certify that older render targets were updated.
        BodyChanges.PrepareNative(maid, .75f);
        int newMaterialBrightness = BodyChanges.RenderColorTexture(maid.Parts,nipple).Color.m_nMainBrightness;
        int writes = maid.body0.ColorUpdates;
        check(newMaterialBrightness == 198 && maid.body0.RenderedColors[nipple].m_nMainBrightness == existingMaterialBrightness,
            "a newly loaded texture can use the new palette while an existing render target still has old pixels");
        BodyChanges.CompleteNative(maid);
        check(maid.body0.ColorUpdates > writes && maid.body0.RenderedColors[nipple].m_nMainBrightness == 198,
            "a partial native texture update cannot suppress the pending whole nipple-color update");

        writes = maid.body0.ColorUpdates;
        BodyChanges.PrepareNative(maid, .75f);
        BodyChanges.RenderColorTexture(maid.Parts,nipple);
        BodyChanges.CompleteNative(maid);
        check(maid.body0.ColorUpdates == writes,
            "unchanged clothing work still reuses the already applied nipple color");

        // UI previews can read the palette before the native completion boundary.
        BodyChanges.PrepareNative(maid, .5f);
        check(maid.Parts.GetPartsColorTableTex(nipple) == maid.Parts.Original,
            "native palette readers keep the original while the renderer receives the progress overlay");
        writes = maid.body0.ColorUpdates;
        BodyChanges.CompleteNative(maid);
        check(maid.body0.ColorUpdates > writes && maid.body0.RenderedColors[nipple].m_nMainBrightness == 233,
            "reading a palette for preview does not consume the pending rendered color change");

        BodyChanges.PrepareNative(maid, 0f);
        check(BodyChanges.RenderColorTexture(maid.Parts,nipple) == maid.Parts.Original,
            "zero progress exposes the native palette immediately");
        writes = maid.body0.ColorUpdates;
        BodyChanges.CompleteNative(maid);
        check(maid.body0.ColorUpdates > writes && maid.body0.LastColor == maid.Parts.Original,
            "a native-palette read cannot suppress restoring the already rendered nipple color");

        // Pure maker edits: no outfit load, table preview, or native color call
        // occurs between PrepareNative and CompleteNative.
        for (int i = 0; i <= 20; i++)
        {
            float progress = i / 20f;
            BodyChanges.PrepareNative(maid, progress);
            BodyChanges.CompleteNative(maid);
            int expected = (int)Math.Round(303 - 140 * (double)progress, MidpointRounding.AwayFromZero);
            check(maid.body0.RenderedColors[nipple].m_nMainBrightness == expected,
                "each pure maker body-progress edit updates the rendered nipple pixels");
            writes = maid.body0.ColorUpdates;
            BodyChanges.PrepareNative(maid, progress);
            BodyChanges.CompleteNative(maid);
            check(maid.body0.ColorUpdates == writes,
                "repeating the same maker progress does not redraw colors");
        }
        BodyChanges.PrepareNative(maid, .25f);
        maid.body0.UpdateInfinityColor(nipple);
        var rendered = maid.body0.RenderedColors[nipple];
        BodyChanges.CompleteNative(maid);
        check(maid.body0.RenderedColors[nipple].Equals(rendered),
            "a genuine native whole-group color update retains its completed pixels");
        BodyChanges.PrepareNative(maid, .75f);
        BodyChanges.CompleteNative(maid);
        BodyChanges.PrepareNative(maid, 0f);
        BodyChanges.PrepareNative(maid, 0f);
        BodyChanges.CompleteNative(maid);
        check(maid.body0.LastColor == maid.Parts.Original
            && maid.body0.RenderedColors[nipple].Equals(maid.Parts.GetPartsColor(nipple)),
            "coalesced zero-progress requests preserve the pending rendered-color restoration");
        check(maid.SaveShape() == saved, "color synchronization leaves serialized maid appearance unchanged");
        BodyChanges.Forget(maid);
    }
}
