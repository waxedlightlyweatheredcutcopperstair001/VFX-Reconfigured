using System;
using VolumetricExplosionFX.Core;
namespace VolumetricExplosionFX.Rendering
{
    // VFX Reconfigured: the settings window's contents, in three tabs:
    //  - Performance: what costs frame rate, for explosions and ground effects alike (limits, rendering, distances);
    //  - Explosion Effects: how explosions look and behave (general, fire and smoke, debris and impacts, nuclear);
    //  - Ground Effects: the engine plumes (which effects, their look, the marks they leave).
    // Settings no longer in the window (window styles, presets, intensity, the look editor) keep their saved values.
    internal static class SettingsModel
    {
        internal sealed class Hooks
        {
            internal UiText Text;
            internal Action Changed, Close, Reset;
            internal Func<bool> CanTest; internal Func<int,bool> Test;
            internal Func<string> Status;
            internal string Conflict;   // another installed explosion mod (null: none)
        }
        static int testKind;
        internal static UiModel Build(FxSettings s,Hooks h)
        {
            var text=h.Text??new UiText("en-us");
            string L(string key,string fallback) { return text.Get(key,fallback); }
            string Times(float v) { return v.ToString("0.00",text.Culture)+" ×"; }
            string Sec(float v) { return v.ToString(v<10?"0.0":"0",text.Culture)+" s"; }
            string Km(float v) { return v.ToString("0.0",text.Culture)+" km"; }
            string Pct(float v) { return (v*100).ToString("0",text.Culture)+" %"; }
            string Int(float v) { return v.ToString("0",text.Culture); }
            var m=new UiModel { Title="Volumetric FX Reconfigured", Reset=L("VfxrDefaults","Defaults"), Close=h.Close, ResetAll=h.Reset, Changed=h.Changed, Status=h.Status };
            var r=GroundBlastFx.Config.GeSettings.Renderer;
            Action ground=GroundBlastFx.Config.GeSettings.NotifyChanged;

            // ---------------- Performance ----------------
            var perf=Page(m,L("VfxrPerformance","Performance"));
            Section(perf,L("VfxrLimits","Limits"));
            Limit(perf,s,L("VfxrMaxEvents","Simultaneous explosions"),L("VfxrMaxEventsHint","Beyond this, weaker explosions give way to stronger ones."),1,32,1,()=>s.MaxEvents,v=>s.MaxEvents=v,Int);
            Limit(perf,s,L("VfxrMaxVolumes","Volumetric explosions"),L("VfxrMaxVolumesHint","How many explosions are drawn as 3D volumes at once."),0,16,1,()=>s.MaxVolumes,v=>s.MaxVolumes=v,Int);
            Limit(perf,s,L("VfxrParticles","Particles per explosion"),L("VfxrParticlesHint","Flames, smoke and embers (spark jets get several times this)."),16,256,16,()=>s.ParticlesPerEvent,v=>s.ParticlesPerEvent=v,Int);
            Limit(perf,s,L("VfxrMaxLights","Dynamic lights"),L("VfxrMaxLightsHint","Explosion lights active at once."),0,6,1,()=>s.MaxLights,v=>s.MaxLights=v,Int);
            Limit(perf,s,L("VfxrMaxDebris","Micro debris"),L("VfxrMaxDebrisHint","Small flying shards, all explosions together."),0,4096,64,()=>s.MaxDebris,v=>s.MaxDebris=v,Int);
            Limit(perf,s,L("VfxrRetained","Part fragments kept"),L("VfxrRetainedHint","Broken part pieces kept in the world; the oldest go first."),0,48,1,()=>s.MaxDebrisParts,v=>s.MaxDebrisParts=v,Int);
            Limit(perf,s,L("VfxrFragmentDetail","Fragment detail"),L("VfxrFragmentDetailHint","Triangles per broken part."),300,16000,100,()=>s.DebrisTriangles,v=>s.DebrisTriangles=v,v=>(v/1000).ToString("0.0",text.Culture)+" k");
            Slider(perf,L("VfxrGroundSources","Ground effect sources"),L("VfxrGroundSourcesHint","Engine plumes drawn at once."),1,16,1,
                ()=>r.MaxRenderedClusters,v=>{ r.MaxRenderedClusters=(int)Math.Round(v); ground(); },Int);
            Section(perf,L("VfxrRendering","Rendering"));
            Toggle(perf,L("VfxrVolumetric","Volumetric explosions"),L("VfxrVolumetricHint","3D fireballs and smoke. Off: flat sprites, much cheaper."),()=>s.Volumetric,v=>s.Volumetric=v);
            perf.Rows.Add(new UiRow { Kind=UiRowKind.Choice, Label=L("VfxrResolution","Volume resolution"), Hint=L("VfxrResolutionHint","Lower is much faster and slightly softer."),
                Options=new[]{L("VfxrFull","Full"),"1/2","1/3"}, GetChoice=()=>s.VolumeDivisor-1, SetChoice=i=>{ s.VolumeDivisor=i+1; s.Validate(); } });
            Limit(perf,s,L("VfxrSteps","Volume detail"),L("VfxrStepsHint","More steps: finer volumes, more GPU time."),16,56,4,()=>s.VolumeSteps,v=>s.VolumeSteps=v,Int);
            Toggle(perf,L("VfxrDistortion","Heat and shock distortion"),L("VfxrDistortionHint","The image ripples around shockwaves and hot gas."),()=>s.Shockwave,v=>s.Shockwave=v);
            Toggle(perf,L("VfxrLighting","Dynamic lighting"),L("VfxrLightingHint","Explosions and fires light up their surroundings."),()=>s.Lighting,v=>s.Lighting=v);
            Toggle(perf,L("VfxrFlameLight","Engine flame light"),L("VfxrFlameLightHint","Engine plumes light the ground."),
                ()=>r.EnableFlameGroundLight,v=>{ r.EnableFlameGroundLight=v; ground(); });
            Toggle(perf,L("VfxrAdaptive","Adaptive performance"),L("VfxrAdaptiveHint","Lowers detail while the frame rate drops."),()=>s.Adaptive,v=>s.Adaptive=v);
            perf.Rows.Add(new UiRow { Kind=UiRowKind.Choice, Label=L("VfxrMemory","Video memory saver"), Hint=L("VfxrMemoryHint","For graphics cards under 6 GB."),
                Options=new[]{L("VfxrAuto","Auto"),L("VfxrOn","On"),L("VfxrOff","Off")}, GetChoice=()=>s.LowMemory, SetChoice=i=>{ s.LowMemory=i; s.Validate(); } });
            Section(perf,L("VfxrDistance","Distance"));
            Slider(perf,L("VfxrExplosionDistance","Explosion distance"),L("VfxrExplosionDistanceHint","Explosions farther away are not drawn (nuclear: at least 60 km)."),0.5f,30,0.5f,
                ()=>(float)(s.Distance/1000),v=>s.Distance=v*1000,Km);
            Slider(perf,L("VfxrGroundDistance","Ground effect distance"),L("VfxrGroundDistanceHint","Engine plumes farther away are not drawn."),0.5f,20,0.25f,
                ()=>r.MaxRenderDistanceM/1000f,v=>{ r.MaxRenderDistanceM=v*1000f; ground(); },Km);

            // ---------------- Explosion Effects ----------------
            var blast=Page(m,L("VfxrExplosions","Explosion Effects"));
            Section(blast,L("VfxrGeneral","General"));
            if(!string.IsNullOrEmpty(h.Conflict))
                blast.Rows.Add(new UiRow { Kind=UiRowKind.Info, Label=L("VfxrConflict","Another explosion mod is installed"),
                    Text=()=>text.Format("VfxrConflictText","{0} also replaces explosions, so each one is drawn twice. Keep only one.",h.Conflict) });
            Toggle(blast,L("VfxrEnabled","Explosion effects"),L("VfxrEnabledHint","Off: KSP's own explosions."),()=>s.Enabled,v=>s.Enabled=v);
            Toggle(blast,L("VfxrReplace","Hide KSP's explosions"),L("VfxrReplaceHint","Their sound is kept."),()=>s.ReplaceStockVisuals,v=>s.ReplaceStockVisuals=v);
            Toggle(blast,L("VfxrExotic","Exotic fuel explosions"),L("VfxrExoticHint","Antimatter, Graviolium, lithium, nuclear salt water and fission pulse units, when installed."),
                ()=>s.ExoticFuels,v=>s.ExoticFuels=v);
            Toggle(blast,L("VfxrShake","Camera shake"),L("VfxrShakeHint","Nearby blasts shake the camera."),()=>s.CameraShake,v=>s.CameraShake=v);
            blast.Rows.Add(new UiRow { Kind=UiRowKind.Choice, Label=L("VfxrPhoto","Photo mode"), Hint=L("VfxrPhotoHint","Slows or freezes explosions only. Alt+F7."),
                Options=new[]{L("VfxrOff","Off"),L("VfxrSlow","Slow"),L("VfxrFrozen","Frozen")}, GetChoice=()=>s.PhotoMode, SetChoice=i=>s.PhotoMode=i });
            blast.Rows.Add(new UiRow { Kind=UiRowKind.Action, Label=L("VfxrTest","Test explosion"), Hint=L("VfxrTestHint","In front of the camera, without damage."),
                Options=new[]{L("VfxrTank","Tank"),L("VfxrStage","Stage"),L("VfxrBooster","Booster"),L("VfxrInFlight","In flight"),L("VfxrBigCrash","Big crash")},
                GetChoice=()=>testKind, SetChoice=i=>testKind=i, Button=L("VfxrTestButton","Test"), Click=()=>{ if(h.Test!=null) h.Test(testKind); }, Available=h.CanTest });
            Section(blast,L("VfxrFireSmoke","Fire & smoke"));
            Slider(blast,L("VfxrSpeed","Explosion speed"),L("VfxrSpeedHint","How fast fireballs and smoke play out."),0.5f,3,0.05f,()=>(float)s.ExplosionSpeed,v=>s.ExplosionSpeed=v,Times);
            Slider(blast,L("VfxrBrightness","Brightness"),L("VfxrBrightnessHint","How bright and white-hot the flash is."),0.5f,2.5f,0.05f,()=>(float)s.Brightness,v=>s.Brightness=v,Times);
            Slider(blast,L("VfxrGlow","Glow time"),L("VfxrGlowHint","How long a medium explosion glows; small ones less, big ones more."),0.3f,6,0.1f,()=>(float)s.GlowSeconds,v=>s.GlowSeconds=v,Sec);
            Slider(blast,L("VfxrEdgeGlow","Edge glow"),L("VfxrEdgeGlowHint","Glow gathered at the top and edges of the plume."),0,1,0.05f,()=>(float)s.EdgeGlow,v=>s.EdgeGlow=v,Pct);
            Slider(blast,L("VfxrOpacity","Smoke opacity"),"",0.1f,2,0.05f,()=>(float)s.SmokeOpacity,v=>s.SmokeOpacity=v,Times);
            Slider(blast,L("VfxrHalfLife","Smoke half-life"),L("VfxrHalfLifeHint","After the glow, the smoke fades by half this often."),0.5f,30,0.5f,()=>(float)s.SmokeHalfLife,v=>s.SmokeHalfLife=v,Sec);
            Slider(blast,L("VfxrMaxSmoke","Longest smoke"),"",5,180,5,()=>(float)s.SmokeMaxSeconds,v=>s.SmokeMaxSeconds=v,Sec);
            Toggle(blast,L("VfxrShadows","Smoke shadows"),L("VfxrShadowsHint","Smoke shades the ground under it."),()=>s.SmokeShadows,v=>s.SmokeShadows=v);
            Slider(blast,L("VfxrHaze","Atmospheric haze"),L("VfxrHazeHint","Distant explosions fade into the sky like the land. 0: off."),0,3,0.05f,()=>(float)s.AtmosphericHaze,v=>s.AtmosphericHaze=v,Times);
            Toggle(blast,L("VfxrGroundFire","Ground fires"),L("VfxrGroundFireHint","Spilled fuel burns on after a crash."),()=>s.Residue,v=>s.Residue=v);
            Slider(blast,L("VfxrGroundFireTime","Ground fire time"),"",5,180,5,()=>(float)s.ResidueSeconds,v=>s.ResidueSeconds=v,Sec,()=>s.Residue);
            Section(blast,L("VfxrDebrisImpacts","Debris & impacts"));
            Toggle(blast,L("VfxrFragments","Part fragments"),L("VfxrFragmentsHint","Destroyed parts break into pieces."),()=>s.PartDebris,v=>s.PartDebris=v);
            Toggle(blast,L("VfxrMicro","Micro debris"),L("VfxrMicroHint","Small shards thrown by the blast."),()=>s.Debris,v=>s.Debris=v);
            Slider(blast,L("VfxrDensity","Micro debris amount"),"",0,2,0.05f,()=>(float)s.DebrisDensity,v=>s.DebrisDensity=v,Times,()=>s.Debris);
            Toggle(blast,L("VfxrGroundImpacts","Ground impacts"),L("VfxrGroundImpactsHint","Thrown soil and dust."),()=>s.Ground,v=>s.Ground=v);
            Toggle(blast,L("VfxrWater","Water impacts"),L("VfxrWaterHint","Spray, a water column and foam."),()=>s.Water,v=>s.Water=v);
            Toggle(blast,L("VfxrBursts","Bursts and sparks"),L("VfxrBurstsHint","Gas tanks, vapour and battery sparks."),()=>s.Burst,v=>s.Burst=v);
            Section(blast,L("VfxrNuclear","Nuclear"));
            Slider(blast,L("VfxrNukeGlow","Glow time"),L("VfxrNukeGlowHint","For 20 kt; bigger bombs glow longer."),2,60,1,()=>(float)s.NukeGlowSeconds,v=>s.NukeGlowSeconds=v,Sec);
            Slider(blast,L("VfxrNukeHalfLife","Smoke half-life"),"",5,300,5,()=>(float)s.NukeSmokeHalfLife,v=>s.NukeSmokeHalfLife=v,Sec);

            // ---------------- Ground Effects ----------------
            var plume=Page(m,L("VfxrGround","Ground Effects"));
            Section(plume,L("VfxrEffects","Effects"));
            Toggle(plume,L("VfxrDust","Dust"),L("VfxrDustHint","Raised where exhaust hits the ground."),()=>r.EnableDust,v=>{ r.EnableDust=v; ground(); });
            Toggle(plume,L("VfxrSteam","Launch pad steam"),"",()=>r.EnablePadSteam,v=>{ r.EnablePadSteam=v; ground(); });
            Toggle(plume,L("VfxrSpray","Water spray"),"",()=>r.EnableWater,v=>{ r.EnableWater=v; ground(); });
            Toggle(plume,L("VfxrVacuum","Vacuum ejecta"),L("VfxrVacuumHint","Regolith thrown out on airless bodies."),()=>r.EnableVacuumEjecta,v=>{ r.EnableVacuumEjecta=v; ground(); });
            Section(plume,L("VfxrLook","Look"));
            Slider(plume,L("VfxrPlumeIntensity","Intensity"),L("VfxrPlumeIntensityHint","How much dust and steam."),0.25f,2,0.05f,
                ()=>r.GlobalIntensity,v=>{ r.GlobalIntensity=v; ground(); },Times);
            Slider(plume,L("VfxrPlumeBrightness","Brightness"),"",GroundBlastFx.Contracts.VisualTuning.MinBrightness,GroundBlastFx.Contracts.VisualTuning.MaxBrightness,0.05f,
                ()=>r.EffectBrightness,v=>{ r.EffectBrightness=GroundBlastFx.Contracts.VisualTuning.Brightness(v); ground(); },Pct);
            Slider(plume,L("VfxrIgnition","Ignition burst"),L("VfxrIgnitionHint","The cloud thrown out as engines start."),
                GroundBlastFx.Contracts.VisualTuning.MinIgnitionStrength,GroundBlastFx.Contracts.VisualTuning.MaxIgnitionStrength,0.05f,
                ()=>r.IgnitionStrength,v=>{ r.IgnitionStrength=GroundBlastFx.Contracts.VisualTuning.IgnitionStrength(v); ground(); },Pct);
            Section(plume,L("VfxrMarks","Ground marks"));
            Toggle(plume,L("VfxrScorch","Scorch marks"),L("VfxrScorchHint","Engines blacken the ground under them."),()=>r.EnableScorch,v=>{ r.EnableScorch=v; ground(); });
            Toggle(plume,L("VfxrKeepMarks","Keep marks"),L("VfxrKeepMarksHint","Saved with the game."),
                ()=>GroundBlastFx.Config.GeSettings.PersistentMarks,v=>{ GroundBlastFx.Config.GeSettings.PersistentMarks=v; ground(); },()=>r.EnableScorch);
            plume.Rows.Add(new UiRow { Kind=UiRowKind.Action, Label=L("VfxrClearMarks","Clear marks"), Button=L("VfxrClear","Clear"),
                Click=()=>{ var a=GroundBlastFx.Core.GroundBlastFxAddon.Instance; if(a!=null) a.ClearScorchMarks(); },
                Available=()=>GroundBlastFx.Core.GroundBlastFxAddon.Instance!=null });
            return m;
        }
        // The ground effects' defaults (the Defaults button resets them with the explosion settings).
        internal static void ResetGround()
        {
            var r=GroundBlastFx.Config.GeSettings.Renderer; var d=new GroundBlastFx.Contracts.RendererSettings();
            r.EnableDust=d.EnableDust; r.EnablePadSteam=d.EnablePadSteam; r.EnableWater=d.EnableWater; r.EnableVacuumEjecta=d.EnableVacuumEjecta;
            r.EnableScorch=d.EnableScorch; r.EnableFlameGroundLight=d.EnableFlameGroundLight; r.GlobalIntensity=d.GlobalIntensity;
            r.MaxRenderDistanceM=d.MaxRenderDistanceM; r.MaxRenderedClusters=d.MaxRenderedClusters;
            GroundBlastFx.Config.GeSettings.ResetVisualTuning();
            GroundBlastFx.Config.GeSettings.PersistentMarks=true;
            GroundBlastFx.Config.GeSettings.NotifyChanged();
        }
        static UiPage Page(UiModel m,string title) { var p=new UiPage { Title=title }; m.Pages.Add(p); return p; }
        static void Section(UiPage p,string title) { p.Rows.Add(new UiRow { Kind=UiRowKind.Section, Label=title }); }
        static void Toggle(UiPage p,string label,string hint,Func<bool> get,Action<bool> set,Func<bool> available=null)
        { p.Rows.Add(new UiRow { Kind=UiRowKind.Toggle, Label=label, Hint=hint, GetBool=get, SetBool=set, Available=available }); }
        static void Slider(UiPage p,string label,string hint,float min,float max,float step,Func<float> get,Action<float> set,Func<float,string> format,Func<bool> available=null)
        { p.Rows.Add(new UiRow { Kind=UiRowKind.Slider, Label=label, Hint=hint, Min=min, Max=max, Step=step, GetValue=get, SetValue=set, Format=format, Available=available }); }
        // A limit: changing it marks the quality as custom and re-validates the settings.
        static void Limit(UiPage p,FxSettings s,string label,string hint,int min,int max,int step,Func<int> get,Action<int> set,Func<float,string> format)
        { Slider(p,label,hint,min,max,step,()=>get(),v=>{ s.Quality=Quality.Custom; set((int)Math.Round(v)); s.Validate(); },format); }
    }
}
