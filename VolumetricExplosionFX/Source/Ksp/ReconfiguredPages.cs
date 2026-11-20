using System;
using VolumetricExplosionFX.Core;
namespace VolumetricExplosionFX.Rendering
{
    /// <summary>
    /// VFX Reconfigured's settings pages in the (shared) settings window: the explosion look and timing, and the ground
    /// effects (Volumetric Ground FX's window, which no longer has its own toolbar button).
    /// </summary>
    internal static class ReconfiguredPages
    {
        internal static UiPage Look(UiModel m,FxSettings s,UiText text,Func<UiModel,string,UiGlyph,UiPage> page,
            Action<UiPage,string,string,float,float,float,Func<float>,Action<float>,Func<float,string>> slider,
            Action<UiPage,string> section,Action<UiPage,FxSettings,UiText,string,string,int,int,int,Func<int>,Action<int>> limit)
        {
            string L(string key,string fallback) { return text.Get(key,fallback); }
            string Sec(float v) { return v.ToString("0.0",text.Culture)+" s"; }
            string Times(float v) { return v.ToString("0.00",text.Culture)+" ×"; }
            var p=page(m,L("ReconfLook","Explosion look"),UiGlyph.Spark);
            section(p,L("ReconfTiming","Timing"));
            slider(p,L("ReconfSpeed","Explosion speed"),L("ReconfSpeedHint","How fast fireballs and their smoke play out (1.00: VEFX's original pace). The shockwave and flying debris keep their own pace; nuclear clouds keep real time."),
                0.5f,3,0.05f,()=>(float)s.ExplosionSpeed,v=>s.ExplosionSpeed=v,Times);
            slider(p,L("ReconfGlow","Glow time"),L("ReconfGlowHint","How long a medium explosion glows. Small ones flash briefly, big ones glow up to three times longer; only fuel and battery fires glow beyond that."),
                0.3f,6,0.1f,()=>(float)s.GlowSeconds,v=>s.GlowSeconds=v,Sec);
            slider(p,L("ReconfHalfLife","Smoke half-life"),L("ReconfHalfLifeHint","Once the glow is gone, the smoke's opacity halves this often."),
                0.5f,30,0.5f,()=>(float)s.SmokeHalfLife,v=>s.SmokeHalfLife=v,Sec);
            slider(p,L("ReconfMaxSmoke","Longest smoke"),L("ReconfMaxSmokeHint","An explosion's smoke is gone after this long at most."),
                5,180,5,()=>(float)s.SmokeMaxSeconds,v=>s.SmokeMaxSeconds=v,v=>v.ToString("0",text.Culture)+" s");
            section(p,L("ReconfAppearance","Appearance"));
            slider(p,L("ReconfBrightness","Explosion brightness"),L("ReconfBrightnessHint","How bright and white-hot the fireball's flash is (1.00: VEFX's original, a dimmer, redder flash)."),
                0.5f,2.5f,0.05f,()=>(float)s.Brightness,v=>s.Brightness=v,Times);
            slider(p,L("ReconfOpacity","Smoke opacity"),L("ReconfOpacityHint","Lower: more transparent smoke. 1.00: VEFX's original density."),
                0.1f,2,0.05f,()=>(float)s.SmokeOpacity,v=>s.SmokeOpacity=v,Times);
            slider(p,L("ReconfEdgeGlow","Glow at the plume's edges"),L("ReconfEdgeGlowHint","How much the glow gathers at the top and outer edges of the rising plume, over a darker, smoky body. 0: VEFX's original glow."),
                0,1,0.05f,()=>(float)s.EdgeGlow,v=>s.EdgeGlow=v,v=>(v*100).ToString("0",text.Culture)+" %");
            p.Rows.Add(new UiRow { Kind=UiRowKind.Toggle, Label=L("ReconfShadows","Smoke shadows"),
                Hint=L("ReconfShadowsHint","Explosion smoke shades the ground under it. Never drawn on things close to the camera, such as your own craft."),
                GetBool=()=>s.SmokeShadows, SetBool=v=>s.SmokeShadows=v });
            slider(p,L("ReconfHaze","Atmospheric haze"),L("ReconfHazeHint","Distant explosions and ground clouds are veiled in the sky's colour by the air in between, like the land (Scatterer's own strength for the planet is followed). 0: off."),
                0,3,0.05f,()=>(float)s.AtmosphericHaze,v=>s.AtmosphericHaze=v,Times);
            section(p,L("ReconfNuclear","Nuclear explosions"));
            slider(p,L("ReconfNukeGlow","Nuclear glow time"),L("ReconfNukeGlowHint","How long a 20 kt nuclear fireball and its rolling cloud glow; bigger bombs glow longer (a megaton: about a minute)."),
                2,60,1,()=>(float)s.NukeGlowSeconds,v=>s.NukeGlowSeconds=v,v=>v.ToString("0",text.Culture)+" s");
            slider(p,L("ReconfNukeHalfLife","Nuclear smoke half-life"),L("ReconfNukeHalfLifeHint","How often a nuclear cloud's opacity halves once its glow is gone."),
                5,300,5,()=>(float)s.NukeSmokeHalfLife,v=>s.NukeSmokeHalfLife=v,v=>v.ToString("0",text.Culture)+" s");
            section(p,L("ReconfLimits","Limits"));
            limit(p,s,text,L("MaxEvents","Simultaneous explosions"),L("MaxEventsHint","Beyond this limit, weaker effects give way to stronger ones."),1,32,1,()=>s.MaxEvents,v=>s.MaxEvents=v);
            limit(p,s,text,L("Particles","Particles per explosion"),L("ParticlesHint","Sparks, flames and smoke in one explosion."),16,256,16,()=>s.ParticlesPerEvent,v=>s.ParticlesPerEvent=v);
            return p;
        }

        internal static UiPage Ground(UiModel m,UiText text,Func<UiModel,string,UiGlyph,UiPage> page,
            Action<UiPage,string,string,float,float,float,Func<float>,Action<float>,Func<float,string>> slider,
            Action<UiPage,string,string,Func<bool>,Action<bool>> toggle)
        {
            string L(string key,string fallback) { return text.Get(key,fallback); }
            string G(string key) { return GroundBlastFx.Config.GeStrings.Get(key); }
            var r=GroundBlastFx.Config.GeSettings.Renderer;
            Action changed=GroundBlastFx.Config.GeSettings.NotifyChanged;
            var p=page(m,L("ReconfGround","Ground effects"),UiGlyph.Link);
            p.Rows.Add(new UiRow { Kind=UiRowKind.Info, Label=L("ReconfGroundAbout","Engine plumes"),
                Text=()=>L("ReconfGroundAboutText","Dust, steam and water thrown up where engine exhaust hits the ground. Big plumes only on launch pads; jet engines raise fainter dust.") });
            slider(p,G("labelIntensity"),G("helpIntensity"),0.25f,2,0.05f,()=>r.GlobalIntensity,v=>{ r.GlobalIntensity=v; changed(); },v=>v.ToString("0.00",text.Culture)+" ×");
            slider(p,G("labelDistance"),"",0.5f,20,0.25f,()=>r.MaxRenderDistanceM/1000f,v=>{ r.MaxRenderDistanceM=v*1000f; changed(); },v=>v.ToString("0.0",text.Culture)+" km");
            slider(p,G("labelBrightness"),G("helpBrightness"),GroundBlastFx.Contracts.VisualTuning.MinBrightness,GroundBlastFx.Contracts.VisualTuning.MaxBrightness,0.05f,
                ()=>r.EffectBrightness,v=>{ r.EffectBrightness=GroundBlastFx.Contracts.VisualTuning.Brightness(v); changed(); },v=>(v*100).ToString("0",text.Culture)+" %");
            slider(p,G("labelIgnitionStrength"),G("helpIgnitionStrength"),GroundBlastFx.Contracts.VisualTuning.MinIgnitionStrength,GroundBlastFx.Contracts.VisualTuning.MaxIgnitionStrength,0.05f,
                ()=>r.IgnitionStrength,v=>{ r.IgnitionStrength=GroundBlastFx.Contracts.VisualTuning.IgnitionStrength(v); changed(); },v=>(v*100).ToString("0",text.Culture)+" %");
            toggle(p,G("toggleScorch"),"",()=>r.EnableScorch,v=>{ r.EnableScorch=v; changed(); });
            toggle(p,G("togglePersistentMarks"),G("helpGroundMarks"),()=>GroundBlastFx.Config.GeSettings.PersistentMarks,v=>{ GroundBlastFx.Config.GeSettings.PersistentMarks=v; changed(); });
            p.Rows.Add(new UiRow { Kind=UiRowKind.Action, Label=G("clearGroundMarks"), Button=G("clearGroundMarks"),
                Click=()=>{ var a=GroundBlastFx.Core.GroundBlastFxAddon.Instance; if(a!=null) a.ClearScorchMarks(); },
                Available=()=>GroundBlastFx.Core.GroundBlastFxAddon.Instance!=null });
            p.Rows.Add(new UiRow { Kind=UiRowKind.Action, Label=G("resetVisualSettings"), Button=L("Defaults","Defaults"),
                Click=()=>{ GroundBlastFx.Config.GeSettings.ResetVisualTuning(); changed(); } });
            return p;
        }
    }
}
