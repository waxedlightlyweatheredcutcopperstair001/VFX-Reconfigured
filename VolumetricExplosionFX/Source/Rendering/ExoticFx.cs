using UnityEngine;
using VolumetricExplosionFX.Core;
namespace VolumetricExplosionFX.Rendering
{
    // VFX Reconfigured: the parts of an exotic explosion (Exotic.cs) that the ordinary explosion has not: glows
    // (ExoticGlow.shader: antimatter's ionised air, Graviolium's blue flashes, lithium's white-crimson fire, nuclear salt
    // water's criticality flashes, the flashes of a chain of fission pulse units) and Graviolium's gravitational lens
    // (registered with LensPass each frame). One per explosion slot, idle for ordinary explosions.
    internal sealed class ExoticFx
    {
        const int MaxGlows=8;
        static Mesh quad;
        readonly MeshRenderer[] glows=new MeshRenderer[MaxGlows];
        readonly MaterialPropertyBlock[] props=new MaterialPropertyBlock[MaxGlows];
        readonly float[] intensity=new float[MaxGlows];
        static readonly int GlowId=Shader.PropertyToID("_ExGlow"), EdgeId=Shader.PropertyToID("_ExEdge"), CoreId=Shader.PropertyToID("_ExCore");
        ExoticKind kind; double units, kg; float fireball, air, end;
        // Fission pulse units: when and where each goes off (fireball radii).
        readonly double[] pulseTimes=new double[MaxGlows]; readonly Vec3[] pulseOffsets=new Vec3[MaxGlows]; int pulses;
        readonly Transform root;
        internal bool Active { get { return kind!=ExoticKind.None; } }
        internal ExoticKind Kind { get { return kind; } }
        internal float End { get { return end; } }
        // For the explosion's light: extra intensity, its colour and reach while the exotic part lasts.
        internal float LightBoost { get; private set; }
        internal Color LightColour { get; private set; }
        internal float LightRange { get; private set; }
        internal ExoticFx(Transform root,VolumeAssets assets)
        {
            this.root=root;
            if(assets.ExoticGlowMaterial==null) return;
            if(quad==null)
            {
                quad=new Mesh { name="VFXR exotic glow quad" };
                quad.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
                quad.triangles=new[]{0,1,2,0,2,3}; quad.bounds=new Bounds(Vector3.zero,Vector3.one*2e5f);   // (sized by the shader)
            }
            for(int i=0;i<MaxGlows;i++)
            {
                var g=new GameObject("VFXR exotic glow "+i); g.layer=EffectLayer.Value; g.transform.SetParent(root,false);
                g.AddComponent<MeshFilter>().sharedMesh=quad;
                glows[i]=g.AddComponent<MeshRenderer>(); glows[i].sharedMaterial=assets.ExoticGlowMaterial;
                glows[i].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; glows[i].receiveShadows=false; glows[i].enabled=false;
                props[i]=new MaterialPropertyBlock();
            }
        }
        internal void Begin(FxPlan plan,double fireballRadius,float air,uint seed)
        {
            kind=plan!=null?plan.Exotic:ExoticKind.None; units=plan!=null?plan.ExoticUnits:0; kg=plan!=null?plan.ExoticKg:0;
            fireball=Mathf.Max((float)fireballRadius,1); this.air=air; LightBoost=0; pulses=0;
            for(int i=0;i<MaxGlows;i++) { intensity[i]=0; if(glows[i]!=null) { glows[i].enabled=false; glows[i].transform.localPosition=Vector3.zero; } }
            switch(kind)
            {
                case ExoticKind.Antimatter: end=(float)Exotic.AntimatterGlowEnd; break;
                case ExoticKind.Graviolium: end=(float)Exotic.LensEnd(units); break;
                case ExoticKind.Lithium: end=(float)(Exotic.LithiumBurnSeconds(kg)+0.5); break;
                case ExoticKind.SaltWater: end=(float)Exotic.SaltWaterEnd; break;
                case ExoticKind.PulseUnits:
                {
                    Exotic.PulseSchedule(units,seed,pulseTimes,pulseOffsets,out pulses);
                    // (each unit's own fireball: 1 kt)
                    float unitBall=(float)Exotic.PulseUnitBallRadius;
                    for(int i=0;i<pulses;i++)
                        if(glows[i]!=null) glows[i].transform.localPosition=new Vector3((float)pulseOffsets[i].X,(float)pulseOffsets[i].Y,(float)pulseOffsets[i].Z)*unitBall;
                    end=(float)pulseTimes[Mathf.Max(pulses-1,0)]+3;
                    break;
                }
                default: end=0; break;
            }
        }
        internal bool Alive(float age) { return Active&&age<end; }
        void Set(int i,float radius,float power,float coreShare,Color edge,Color core)
        {
            intensity[i]=power;
            if(glows[i]==null) return;
            props[i].SetVector(GlowId,new Vector4(radius,power,coreShare,0));
            props[i].SetColor(EdgeId,edge); props[i].SetColor(CoreId,core);
            glows[i].SetPropertyBlock(props[i]);
        }
        // age: seconds since the explosion.
        internal void Tick(float age)
        {
            LightBoost=0;
            for(int i=0;i<MaxGlows;i++) intensity[i]=0;
            if(!Active) return;
            if(age>end) { for(int i=0;i<MaxGlows;i++) if(glows[i]!=null) glows[i].enabled=false; return; }
            switch(kind)
            {
                case ExoticKind.Antimatter:
                {
                    float scale=(float)Exotic.AntimatterGlowScale(air), R=fireball*scale;
                    float dim=air<0.01f?1:Mathf.Clamp(Mathf.Sqrt(air),0.35f,1);
                    float p=(float)Exotic.AntimatterGlow(age)*dim;
                    Set(0,R,p,Mathf.Clamp(0.6f/scale,0.05f,0.5f),new Color(0.45f,0.38f,1f),new Color(0.95f,0.92f,1f));
                    LightBoost=p*8; LightColour=new Color(0.72f,0.62f,1f); LightRange=R*3;
                    break;
                }
                case ExoticKind.Graviolium:
                {
                    LensState s=Exotic.Lens(units,age);
                    float p=(float)s.Flash*1.5f;
                    Set(0,(float)s.FlashRadius,p,0.18f,new Color(0.15f,0.45f,1f),new Color(0.7f,0.88f,1f));
                    LightBoost=p*5; LightColour=new Color(0.35f,0.6f,1f); LightRange=(float)s.Radius*5;
                    if(s.Einstein>1e-3||s.RingAmp>1e-3)
                    {
                        float R=(float)s.Radius, k=(float)(s.Einstein/(0.4*s.Radius));
                        float q=Mathf.Max(R*1.05f,(float)s.RingRadius+0.6f*R);
                        LensPass.Register(new LensPass.Lens { Position=root.position,
                            Shape=new Vector4(q,R,(float)s.Einstein,(float)s.Core), Ring=new Vector4((float)s.RingRadius,(float)s.RingAmp,k,k) });
                    }
                    break;
                }
                case ExoticKind.Lithium:
                {
                    // A white flash as the metal bursts, then the flickering glare of the burning metal, fading as it burns out.
                    float burn=(float)Exotic.LithiumBurnSeconds(kg), left=Mathf.Clamp01(1-age/burn);
                    float flicker=0.8f+0.2f*Mathf.PerlinNoise(age*6,(float)(units%97));
                    float p=1.5f*Mathf.Exp(-age/0.25f)+0.35f*left*flicker*Mathf.Clamp01(age*4);
                    float R=Mathf.Max(fireball*0.9f,(float)(Exotic.LithiumSparks(kg).FlameSize*3));
                    Set(0,R,p,0.25f,new Color(1f,0.35f,0.45f),new Color(1f,0.92f,0.95f));
                    LightBoost=p*4; LightColour=new Color(1f,0.75f,0.8f); LightRange=R*6;
                    break;
                }
                case ExoticKind.SaltWater:
                {
                    // The blue flashes of the criticality excursion.
                    float p=(float)Exotic.SaltWaterFlash(age), R=fireball*2.2f;
                    Set(0,R,p,0.2f,new Color(0.25f,0.45f,1f),new Color(0.75f,0.85f,1f));
                    LightBoost=p*4; LightColour=new Color(0.45f,0.62f,1f); LightRange=R*4;
                    break;
                }
                case ExoticKind.PulseUnits:
                {
                    float R=(float)Exotic.PulseUnitBallRadius*2;
                    for(int i=0;i<pulses;i++)
                    {
                        float p=(float)Exotic.PulseFlash(age-pulseTimes[i]);
                        if(p<=0) continue;
                        Set(i,R,p,0.2f,new Color(1f,0.72f,0.42f),new Color(1f,0.97f,0.9f));
                        LightBoost=Mathf.Max(LightBoost,p*6);
                    }
                    LightColour=new Color(1f,0.93f,0.85f); LightRange=R*20;
                    break;
                }
            }
        }
        internal void ShowFor(Camera cam)
        {
            bool mine=Active&&CameraRig.IsFlight(cam)&&(CameraRig.Far==null||cam==CameraRig.Near);
            for(int i=0;i<MaxGlows;i++) if(glows[i]!=null) glows[i].enabled=mine&&intensity[i]>0.003f;
        }
        internal void Clear()
        {
            kind=ExoticKind.None; LightBoost=0; pulses=0;
            for(int i=0;i<MaxGlows;i++) { intensity[i]=0; if(glows[i]!=null) glows[i].enabled=false; }
        }
    }
}
