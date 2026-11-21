using UnityEngine;
using VolumetricExplosionFX.Core;
namespace VolumetricExplosionFX.Rendering
{
    // VFX Reconfigured: the parts of an exotic fuel explosion (Exotic.cs) that the ordinary explosion has not: the glow
    // (ExoticGlow.shader: antimatter's ionised air, Graviolium's blue flashes) and Graviolium's gravitational lens
    // (registered with LensPass each frame). One per explosion slot, idle for ordinary explosions.
    internal sealed class ExoticFx
    {
        static Mesh quad;
        readonly MeshRenderer glow;
        readonly MaterialPropertyBlock props=new MaterialPropertyBlock();
        static readonly int GlowId=Shader.PropertyToID("_ExGlow"), EdgeId=Shader.PropertyToID("_ExEdge"), CoreId=Shader.PropertyToID("_ExCore");
        ExoticKind kind; double units; float fireball, air, end, intensity;
        Transform root;
        internal bool Active { get { return kind!=ExoticKind.None; } }
        internal ExoticKind Kind { get { return kind; } }
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
            var g=new GameObject("VFXR exotic glow"); g.layer=EffectLayer.Value; g.transform.SetParent(root,false);
            g.AddComponent<MeshFilter>().sharedMesh=quad;
            glow=g.AddComponent<MeshRenderer>(); glow.sharedMaterial=assets.ExoticGlowMaterial;
            glow.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; glow.receiveShadows=false; glow.enabled=false;
        }
        internal void Begin(FxPlan plan,double fireballRadius,float air)
        {
            kind=plan!=null?plan.Exotic:ExoticKind.None; units=plan!=null?plan.ExoticUnits:0;
            fireball=Mathf.Max((float)fireballRadius,1); this.air=air; intensity=0; LightBoost=0;
            end=kind==ExoticKind.Antimatter?(float)Exotic.AntimatterGlowEnd:kind==ExoticKind.Graviolium?(float)Exotic.LensEnd(units):0;
            if(glow!=null) glow.enabled=false;
        }
        internal bool Alive(float age) { return Active&&age<end; }
        // age: real seconds since the explosion.
        internal void Tick(float age)
        {
            intensity=0; LightBoost=0;
            if(!Active) return;
            if(age>end) { if(glow!=null) glow.enabled=false; return; }
            if(kind==ExoticKind.Antimatter)
            {
                float scale=(float)Exotic.AntimatterGlowScale(air), R=fireball*scale;
                float dim=air<0.01f?1:Mathf.Clamp(Mathf.Sqrt(air),0.35f,1);
                intensity=(float)Exotic.AntimatterGlow(age)*dim;
                props.SetVector(GlowId,new Vector4(R,intensity,Mathf.Clamp(0.6f/scale,0.05f,0.5f),0));
                props.SetColor(EdgeId,new Color(0.45f,0.38f,1f)); props.SetColor(CoreId,new Color(0.95f,0.92f,1f));
                LightBoost=intensity*8; LightColour=new Color(0.72f,0.62f,1f); LightRange=R*3;
            }
            else
            {
                LensState s=Exotic.Lens(units,age);
                intensity=(float)s.Flash*1.5f;
                props.SetVector(GlowId,new Vector4((float)s.FlashRadius,intensity,0.18f,0));
                props.SetColor(EdgeId,new Color(0.15f,0.45f,1f)); props.SetColor(CoreId,new Color(0.7f,0.88f,1f));
                LightBoost=intensity*5; LightColour=new Color(0.35f,0.6f,1f); LightRange=(float)s.Radius*5;
                if(s.Einstein>1e-3||s.RingAmp>1e-3)
                {
                    float R=(float)s.Radius, k=(float)(s.Einstein/(0.4*s.Radius));
                    float q=Mathf.Max(R*1.05f,(float)s.RingRadius+0.6f*R);
                    LensPass.Register(new LensPass.Lens { Position=root.position,
                        Shape=new Vector4(q,R,(float)s.Einstein,(float)s.Core), Ring=new Vector4((float)s.RingRadius,(float)s.RingAmp,k,k) });
                }
            }
            if(glow!=null) glow.SetPropertyBlock(props);
        }
        internal void ShowFor(Camera cam)
        {
            if(glow==null) return;
            glow.enabled=Active&&intensity>0.003f&&CameraRig.IsFlight(cam)&&(CameraRig.Far==null||cam==CameraRig.Near);
        }
        internal void Clear() { kind=ExoticKind.None; intensity=0; LightBoost=0; if(glow!=null) glow.enabled=false; }
    }
}
