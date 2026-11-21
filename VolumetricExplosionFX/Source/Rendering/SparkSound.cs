using System;
using UnityEngine;
using VolumetricExplosionFX.Core;
namespace VolumetricExplosionFX.Rendering
{
    // VFX Reconfigured: the sound of a spark jet (venting battery, burning lithium): a hiss with crackling pops, following
    // the jets (Exotic.SparkLevel), and a pop as each jet bursts out. Both sounds are generated here at start-up (filtered
    // noise and short noise bursts): no audio files, nothing taken from the game or other mods. 3D, at the explosion,
    // at KSP's ship volume; silent while the game is paused.
    internal sealed class SparkSound
    {
        const int Rate=44100;
        static AudioClip hiss, pop;
        readonly AudioSource loop, shots;
        SparkSpec spec; int jetsHeard; float pitch;
        internal SparkSound(Transform root)
        {
            Make();
            var go=new GameObject("VFXR spark sound"); go.transform.SetParent(root,false);
            loop=Source(go); loop.clip=hiss; loop.loop=true;
            shots=Source(go);
        }
        static AudioSource Source(GameObject go)
        {
            AudioSource a=go.AddComponent<AudioSource>();
            a.playOnAwake=false; a.spatialBlend=1; a.dopplerLevel=0; a.rolloffMode=AudioRolloffMode.Logarithmic;
            a.minDistance=12; a.maxDistance=2000; a.volume=0; a.priority=96;
            return a;
        }
        static float ShipVolume { get { try { return Mathf.Clamp01(GameSettings.SHIP_VOLUME); } catch(Exception) { return 1; } } }
        internal void Begin(FxPlan plan,uint seed)
        {
            spec=plan!=null?plan.Sparks:null; jetsHeard=0;
            var rnd=new SplitRandom(seed^0x534E44u);
            // (lithium sizzles lower than a battery's sharp hiss)
            pitch=(float)((plan!=null&&plan.Exotic==ExoticKind.Lithium?0.75:1.0)*(0.9+0.25*rnd.Next()));
            loop.Stop(); loop.volume=0; loop.pitch=pitch;
        }
        internal void Tick(float age)
        {
            if(spec==null||hiss==null) return;
            bool paused=FlightDriver.Pause;
            float level=(float)Exotic.SparkLevel(spec,age);
            if(level<=0.001f||paused) { if(loop.isPlaying) loop.Stop(); }
            else
            {
                if(!loop.isPlaying) { loop.time=UnityEngine.Random.Range(0f,hiss.length*0.9f); loop.Play(); }
                loop.volume=Mathf.Clamp01(0.85f*level)*ShipVolume;
            }
            int jet=Exotic.JetAt(spec,age);
            if(!paused&&jet>=jetsHeard&&pop!=null)
            {
                jetsHeard=jet+1;
                shots.pitch=pitch*(0.85f+0.3f*UnityEngine.Random.value);
                shots.PlayOneShot(pop,0.9f*ShipVolume);
            }
        }
        internal void Stop() { spec=null; if(loop!=null) loop.Stop(); }
        // ---- the sounds ----
        static void Make()
        {
            if(hiss!=null) return;
            try
            {
                var rnd=new System.Random(1234);
                // Hiss: white noise, high-passed (no rumble), with a slow flutter; crackle: short bursts of noise at random,
                // ~70 a second, each decaying in a few milliseconds, some much louder (snaps). Two seconds, the ends
                // cross-faded so the loop has no seam.
                int n=Rate*2, fade=Rate/20;
                var data=new float[n+fade];
                float prevIn=0, hp=0, crackle=0, env=0;
                for(int i=0;i<data.Length;i++)
                {
                    float white=(float)(rnd.NextDouble()*2-1);
                    hp=0.92f*(hp+white-prevIn); prevIn=white;
                    float flutter=0.75f+0.25f*Mathf.Sin(i*2*Mathf.PI*7f/Rate)*Mathf.Sin(i*2*Mathf.PI*2.3f/Rate);
                    if(rnd.NextDouble()<70.0/Rate) { env=(float)(0.4+0.6*rnd.NextDouble()); if(rnd.NextDouble()<0.12) env*=2.2f; }
                    env*=0.9975f;   // (~9 ms)
                    crackle=env*(float)(rnd.NextDouble()*2-1);
                    data[i]=0.32f*hp*flutter+0.5f*crackle;
                }
                var clip=new float[n];
                for(int i=0;i<n;i++) clip[i]=data[i];
                for(int i=0;i<fade;i++) { float k=(float)i/fade; clip[i]=data[n+i]*(1-k)+data[i]*k; }
                Normalise(clip,0.8f);
                hiss=AudioClip.Create("VFXR spark hiss",n,1,Rate,false); hiss.SetData(clip,0);
                // Pop: a burst of noise, low-passed, decaying in ~60 ms.
                int m=Rate/4; var p=new float[m]; float lp=0;
                for(int i=0;i<m;i++)
                {
                    float white=(float)(rnd.NextDouble()*2-1);
                    lp+=0.25f*(white-lp);
                    p[i]=lp*Mathf.Exp(-i/(0.06f*Rate))+0.3f*white*Mathf.Exp(-i/(0.004f*Rate));
                }
                Normalise(p,0.95f);
                pop=AudioClip.Create("VFXR spark pop",m,1,Rate,false); pop.SetData(p,0);
            }
            catch(Exception ex) { Debug.LogWarning("[VFXR] Spark sounds could not be made ("+ex.Message+"): sparks are silent"); hiss=null; pop=null; }
        }
        static void Normalise(float[] d,float peak)
        {
            float max=1e-6f; for(int i=0;i<d.Length;i++) max=Mathf.Max(max,Mathf.Abs(d[i]));
            float k=peak/max; for(int i=0;i<d.Length;i++) d[i]*=k;
        }
    }
}
