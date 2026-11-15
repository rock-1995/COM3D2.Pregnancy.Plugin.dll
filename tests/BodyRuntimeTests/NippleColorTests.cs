using COM3D2.Pregnancy.Plugin;

static class NippleColorTests
{
    public static void Run(Action<bool,string> check)
    {
        void Apply(Maid m,float p) { var next=BodyChanges.Prepare(m,p);if(next!=null)BodyChanges.Apply(next); }
        int Brightness(Maid m)=>m.Parts.GetPartsColorTableTex(MaidParts.PARTS_COLOR.NIPPLE).Color.m_nMainBrightness;
        var dark=new Maid(13,35){Births=2};dark.Parts.Color.m_nMainBrightness=43;
        var light=new Maid(13,35){Births=2};light.Parts.Color.m_nMainBrightness=403;
        string darkSaved=dark.SaveShape(),lightSaved=light.SaveShape();
        Apply(dark,.5f);Apply(light,.5f);
        check(Brightness(dark)==103 && Brightness(light)==283,"each maid blends from her own original color at 50%, not the base preset");
        check(dark.body0.LastColors[MaidParts.PARTS_COLOR.EYE_L].Color.m_nMainBrightness==72,"other color groups retain their original delta behavior");
        int writes=light.body0.ColorUpdates;var texture=light.body0.LastColor;
        for(int i=0;i<100;i++)Apply(light,.5f);
        check(Brightness(light)==283 && light.body0.LastColor==texture && light.body0.ColorUpdates==writes,"repeated Apply neither accumulates tint nor rebuilds unchanged color tables");

        PregnancyPlugin.CfgNippleColorLimit.Value=25;
        Apply(dark,.5f);Apply(light,.5f);
        check(Brightness(dark)==73 && Brightness(light)==343,"changed F1 limit is applied with otherwise identical pregnancy state");
        check(light.body0.LastColors[MaidParts.PARTS_COLOR.EYE_L].Color.m_nMainBrightness==72,"nipple cap does not cap eye color");
        check(Math.Abs(light.body0.Values["Hara"]-.31f)<.00001,"nipple cap does not cap body sliders");
        light.Births=0;Apply(light,.5f);
        check(Brightness(light)==379,"a 25% ceiling does not rescale existing 10% progress");
        light.Births=5;Apply(light,0);
        check(Brightness(light)==343,"postpartum progress remains capped at 25%");
        PregnancyPlugin.CfgNippleColorLimit.Value=0;Apply(light,0);
        check(Brightness(light)==403,"zero cap restores the maid's own original color");
        PregnancyPlugin.CfgNippleColorLimit.Value=100;Apply(light,0);
        check(Brightness(light)==163,"100% reaches the reference target even for a different starting color");
        light.Parts.Color.m_nMainBrightness=523;Apply(light,0);
        check(Brightness(light)==163 && light.Parts.Color.m_nMainBrightness==523,"live native setting edits do not shift the final target");
        PregnancyPlugin.CfgNippleColorLimit.Value=50;Apply(light,0);
        check(Brightness(light)==343,"partial blend uses edited native setting, never the previously applied tint");
        light.Parts.Color.m_nMainBrightness=403;
        check(dark.SaveShape()==darkSaved && light.SaveShape()==lightSaved,"color blending never alters saved appearance settings");
        BodyChanges.Reset(dark);BodyChanges.Reset(light);
        check(light.Parts.GetPartsColorTableTex(MaidParts.PARTS_COLOR.NIPPLE)==light.Parts.Original,"reset restores the native color table");

        // A base preset change must not affect the nipple destination or baseline.
        string basePath=Path.Combine(AppContext.BaseDirectory,"base.preset");
        Maid.PresetBaseBrightness=163;
        using(var stream=File.Open(basePath,FileMode.Append))stream.WriteByte(0);
        Apply(dark,.5f);
        check(Brightness(dark)==103,"equal base/reference brightness still blends the maid's own brightness");
        Maid.PresetBaseBrightness=903;
        using(var stream=File.Open(basePath,FileMode.Append))stream.WriteByte(0);
        Apply(dark,.5f);
        check(Brightness(dark)==103,"changing base preset color cannot change nipple interpolation");
        Maid.PresetBaseBrightness=303;
        using(var stream=File.Open(basePath,FileMode.Append))stream.WriteByte(0);
        PregnancyPlugin.CfgNippleColorLimit.Value=100;
        BodyChanges.Forget(dark);BodyChanges.Forget(light);

        var start=new MaidParts.PartsColor {m_bUse=true,m_nMainHue=10,m_nMainChroma=200,m_nMainBrightness=400,m_nMainContrast=20,m_nShadowRate=30,m_nShadowHue=40,m_nShadowChroma=50,m_nShadowBrightness=60,m_nShadowContrast=70};
        var target=new MaidParts.PartsColor {m_nMainHue=30,m_nMainChroma=100,m_nMainBrightness=200,m_nMainContrast=40,m_nShadowRate=50,m_nShadowHue=60,m_nShadowChroma=70,m_nShadowBrightness=80,m_nShadowContrast=90};
        var half=BodyChangeProfile.BlendColor(start,target,.8,50);
        check(BodyChangeProfile.ColorValues(half).SequenceEqual(new[]{20,150,300,30,40,50,60,70,80}),"all nine native channels interpolate under the same cap");
        check(half.m_bUse && start.m_nMainBrightness==400,"non-numeric use flag and source value stay unchanged");
        foreach(int cap in new[]{-1,0,25,50,100,150})
        {
            int previous=400;
            for(int i=0;i<=100;i++)
            {
                int value=BodyChangeProfile.BlendColor(start,target,i/100d,cap).m_nMainBrightness;
                check(value<=previous && value>=200 && value<=400,"gradual interpolation stays between original and target");
                previous=value;
            }
            int bounded=Math.Clamp(cap,0,100);
            check(previous==400-2*bounded,"out-of-range cap is bounded to 0-100%");
        }
    }
}
