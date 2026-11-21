using System;
namespace VolumetricExplosionFX.Core
{
    // VFX Reconfigured: explosions of exotic fuels, found by resource name in any destroyed part (no dependency on the mods
    // that define them: without them the resources never appear).
    //
    // Antimatter (Far Future Technologies; KSP Interstellar uses the same name). FFT rates it at 36 GJ per unit
    // (DetonationKJPerUnit = 36,000,000 on its tanks). Drawn at a tenth of that: about half of a proton-antiproton
    // annihilation leaves as neutrinos, and the rest is scaled down for the look. In air the energy comes out as
    // ~100+ MeV gamma rays and pions that travel hundreds of metres before they are absorbed (a radiation length is ~300 m
    // of sea-level air), so unlike a nuclear fireball (soft X-rays absorbed within metres) it lights a huge, soft-edged
    // sphere of air: a violet-blue glow of ionised nitrogen around a smaller white-hot core, a weaker shock, and almost no
    // smoke (no casing, no fission products): a pale, clean cloud that rises and fades. Thinner air: larger, fainter glow.
    //
    // Graviolium (Blueshift): fictional. An electric-blue flash, then for a few seconds a gravitational lens (the world
    // behind it bent into an Einstein ring and arcs around a small black core with a bright blue rim), a ring of
    // stretch-and-squeeze rippling outward like a gravitational wave, and a collapse with a last blue burst.
    //
    // Lithium (Near Future Propulsion): a metal fire. Burning lithium is blindingly white with a crimson tint (lithium's
    // flame colour), throws molten droplets, and makes a dense white smoke of lithium oxide and hydroxide; metal fires are
    // hard to put out, so it burns on for a long time.
    //
    // Nuclear salt water (Far Future Technologies): uranium salt solution kept subcritical in neutron-absorbing tubes. A
    // burst tank lets it pool into a critical mass: a criticality excursion, as in real solution accidents. A blue flash of
    // ionised air (a fraction of a second, as eyewitnesses of criticality accidents described it), repeated as the boiling
    // solution falls back together, a violent steam explosion of tiny nuclear yield, and a white steam plume with a faint
    // grey haze of fallout.
    //
    // Fission pulse units (Far Future Technologies, Orion-style): small nuclear charges (Orion designs: about 0.1 to
    // 2.5 kt). Drawn as a chain detonation: a few of them going off one after another in blinding flashes (1 kt each),
    // around a dusty, grey-brown mushroom of their combined yield.
    public enum ExoticKind { None, Antimatter, Graviolium, Lithium, SaltWater, PulseUnits }
    public struct ExoticInfo
    {
        public ExoticKind Kind;
        public double Units;   // the resource's amount in the part
        public double MassKg;  // its mass
    }
    // A stream of sparks, flames and smoke over time (BlastParticles.TickSparks): a battery's venting jets, lithium's
    // molten droplets.
    public sealed class SparkSpec
    {
        public int Jets;                        // 0: a fountain all round; else directional jets, one after another
        public double Start, Seconds;           // when it starts and how long it lasts (real s)
        public double Rate, Speed, Spread, Size, Life;   // sparks per second, m/s, cone spread, size (m), lifetime (s)
        public Vec3 Hot, Cool;                  // spark colours (each spark between them)
        public double FlameRate, FlameSize; public Vec3 Flame;
        public double SmokeRate, SmokeSize, SmokeAlpha; public Vec3 Smoke;
        public double PopFlash;                 // size of the flash as each jet bursts out (0: none)
        public double End { get { return Start+Seconds; } }
    }
    public struct LensState
    {
        public bool Alive;
        public double Radius;        // the lens's reach (m)
        public double Einstein;      // Einstein ring radius (m), 0 when the lens is off
        public double Core;          // black core radius (m)
        public double RingRadius, RingAmp;   // the gravitational-wave ring: radius (m) and strength (0-1)
        public double Flash, FlashRadius;    // the blue flash: intensity and radius (m)
    }
    public static class Exotic
    {
        public const string AntimatterName="Antimatter", GravioliumName="Graviolium", LithiumName="Lithium",
            SaltWaterName="NuclearSaltWater", PulseUnitsName="FissionPulses";
        static bool Is(ResourceSnapshot r,string name) { return string.Equals(r.Name,name,StringComparison.OrdinalIgnoreCase); }
        public const double AntimatterJoulesPerUnit=36e9;   // FFT's tanks
        public const double AntimatterVisibleShare=0.1;     // drawn at a tenth of FFT's yield
        const double TntJoules=4.184e6;
        const double MinUnits=1e-4;

        static double Smooth(double a,double b,double x) { double t=Numbers.Clamp((x-a)/(b-a),0,1); return t*t*(3-2*t); }

        // Antimatter first (it outshines anything else in the tank), then Graviolium.
        public static ExoticInfo Of(PartSnapshot p)
        {
            var info=new ExoticInfo();
            if(p==null||p.Resources==null) return info;
            double anti=0, grav=0, li=0, liKg=0, nsw=0, nswKg=0, pulse=0;
            for(int i=0;i<p.Resources.Length;i++)
            {
                ResourceSnapshot r=p.Resources[i]; if(r==null||r.Name==null) continue;
                if(Is(r,AntimatterName)) anti+=r.Amount;
                else if(Is(r,GravioliumName)) grav+=r.Amount;
                else if(Is(r,LithiumName)) { li+=r.Amount; liKg+=r.MassKg; }
                else if(Is(r,SaltWaterName)) { nsw+=r.Amount; nswKg+=r.MassKg; }
                else if(Is(r,PulseUnitsName)) pulse+=r.Amount;
            }
            // (the most violent one decides)
            if(anti>=MinUnits) { info.Kind=ExoticKind.Antimatter; info.Units=anti; }
            else if(pulse>=0.5) { info.Kind=ExoticKind.PulseUnits; info.Units=pulse; }
            else if(nsw>=1) { info.Kind=ExoticKind.SaltWater; info.Units=nsw; info.MassKg=nswKg>0?nswKg:nsw*1.05; }
            else if(grav>=MinUnits) { info.Kind=ExoticKind.Graviolium; info.Units=grav; }
            else if(li>=1) { info.Kind=ExoticKind.Lithium; info.Units=li; info.MassKg=liKg>0?liKg:li*0.53; }
            return info;
        }

        // ---------------- Antimatter ----------------
        public static double AntimatterTntKg(double units) { return Numbers.Positive(units)*AntimatterJoulesPerUnit*AntimatterVisibleShare/TntJoules; }
        // A nuclear-sized event from 1 kt up (FFT's own tanks: about 10 kt to 260 kt drawn); smaller amounts a bomb-sized one.
        public static WarheadInfo AntimatterWarhead(double units)
        {
            double tnt=Numbers.Clamp(AntimatterTntKg(units),0.02,1e10);
            if(tnt>=1e6) return new WarheadInfo { Kind=WarheadKind.Nuke, TntKg=tnt, RangeM=Numbers.Clamp(900*Math.Pow(tnt/2e7,0.4),150,20000) };
            return new WarheadInfo { Kind=tnt>=80?WarheadKind.Bomb:WarheadKind.HighExplosive, TntKg=tnt, RangeM=Numbers.Clamp(7*Math.Pow(tnt,1.0/3),2,400) };
        }
        // After Warhead.Apply: the clean, pale cloud (no soil, little soot), shorter glow (see Reconfig).
        public static void ApplyAntimatter(FxPlan plan,double units)
        {
            if(plan==null) return;
            plan.Exotic=ExoticKind.Antimatter; plan.ExoticUnits=units;
            plan.Soot=Math.Min(plan.Soot,0.04); plan.OreKg=0; plan.WhiteFlame=Math.Max(plan.WhiteFlame,0.6);
            plan.SmokeR=0.84; plan.SmokeG=0.84; plan.SmokeB=0.88;
            if(plan.Warhead!=WarheadKind.Nuke) plan.BurnableKg=Math.Min(plan.BurnableKg,AntimatterTntKg(units)*1.5);
        }
        // The ionised-air glow: radius as a multiple of the fireball's (larger and fainter in thin air; in vacuum only a
        // flash round the core), and its intensity over real time (the gamma flash, then the air's afterglow).
        public static double AntimatterGlowScale(double air)
        {
            if(air<0.01) return 2;
            return Numbers.Clamp(3.5/Math.Sqrt(air),3.5,12);
        }
        public static double AntimatterGlow(double age) { return age<0?0:Math.Exp(-age/0.12)+0.3*Math.Exp(-age/0.9); }
        public static double AntimatterGlowEnd { get { return 5; } }

        // ---------------- Graviolium ----------------
        // Lens reach: ~15 m for Blueshift's smallest tank (6.25 units) to ~600 m for its biggest (10,880).
        public static double LensRadius(double units) { return Numbers.Clamp(15*Math.Sqrt(Numbers.Positive(units)/6.25),10,600); }
        // 3 s (smallest tank) to 6 s (biggest).
        public static double LensDuration(double units) { return 3+3*Numbers.Clamp(Math.Log10(Math.Max(units,1e-6)/6.25)/Math.Log10(10880/6.25),0,1); }
        // The Graviolium tank's own blast: the electric-blue flash (an EMP-style burst, no fire).
        public static WarheadInfo GravioliumWarhead(double units)
        {
            return new WarheadInfo { Kind=WarheadKind.Emp, TntKg=0.2, RangeM=LensRadius(units) };
        }
        public static void ApplyGraviolium(FxPlan plan,double units)
        {
            if(plan==null) return;
            plan.Exotic=ExoticKind.Graviolium; plan.ExoticUnits=units;
            plan.Lifetime=Math.Max(plan.Lifetime,LensDuration(units)+1.5);
        }
        const double RingLife=1.4;
        public static LensState Lens(double units,double age)
        {
            var s=new LensState();
            double R=LensRadius(units), D=LensDuration(units);
            s.Radius=R;
            if(age<0||age>D+1.2) return s;
            s.Alive=true;
            // Grows in a third of a second, holds, collapses over the last 0.6 s.
            double k=Smooth(0,0.35,age)*Math.Pow(1-Smooth(D-0.6,D,age),1.5);
            s.Einstein=0.4*R*k; s.Core=0.13*R*k;
            // A ring launched as the lens forms, another at the collapse.
            double a1=age-0.1, a2=age-D;
            double a=a2>=0?a2:a1;
            if(a>=0&&a<=RingLife) { s.RingRadius=R*(0.3+2.2*a/RingLife); s.RingAmp=Math.Pow(1-a/RingLife,1.5)*(a2>=0?1.2:1); }
            // The flash as it forms, a bigger burst at the collapse.
            s.Flash=Math.Exp(-age/0.15)+(a2>=0?1.3*Math.Exp(-a2/0.2):0);
            s.FlashRadius=a2>=0?1.6*R:1.2*R;
            return s;
        }
        public static double LensEnd(double units) { return LensDuration(units)+1.2; }

        // ---------------- Lithium ----------------
        static double Log(double x) { return Math.Log10(1+Math.Max(x,0)); }
        public static double LithiumBurnSeconds(double kg) { return Numbers.Clamp(6+5*Log(kg/20),6,30); }
        // A burst of burning metal (incendiary: it splashes and keeps burning on the ground).
        public static WarheadInfo LithiumWarhead(double kg)
        {
            return new WarheadInfo { Kind=WarheadKind.Incendiary, TntKg=Numbers.Clamp(kg*0.25,0.5,5000), RangeM=Numbers.Clamp(4*Math.Pow(Math.Max(kg,1),1.0/3),3,60) };
        }
        public static void ApplyLithium(FxPlan plan,double units,double kg)
        {
            if(plan==null) return;
            plan.Exotic=ExoticKind.Lithium; plan.ExoticUnits=units; plan.ExoticKg=kg;
            plan.PartKind=PartKind.LiquidTank;   // (a fire is left where it lands: Residue)
            plan.Soot=0.02; plan.WhiteFlame=1; plan.OreKg=0;
            plan.SmokeR=0.94; plan.SmokeG=0.94; plan.SmokeB=0.95;
            plan.Sparks=LithiumSparks(kg);
            plan.Lifetime=Math.Max(plan.Lifetime,plan.Sparks.End+2);
        }
        public static SparkSpec LithiumSparks(double kg)
        {
            double l=Log(kg/10);
            return new SparkSpec { Jets=0, Start=0.05, Seconds=LithiumBurnSeconds(kg),
                Rate=Numbers.Clamp(60+40*l,60,300), Speed=Numbers.Clamp(4+2*l,4,10), Spread=1, Size=Numbers.Clamp(0.08+0.03*l,0.08,0.18), Life=1.4,
                Hot=new Vec3(1,0.95,0.95), Cool=new Vec3(1,0.3,0.4),
                FlameRate=10, FlameSize=Numbers.Clamp(0.6+0.5*l,0.6,3), Flame=new Vec3(1,0.45,0.55),
                SmokeRate=5, SmokeSize=Numbers.Clamp(1.5+l,1.5,6), SmokeAlpha=0.5, Smoke=new Vec3(0.95,0.95,0.96) };
        }

        // ---------------- Batteries ----------------
        // A battery going into thermal runaway: its cells vent one after another in jets of sparks and flame (a few
        // seconds for a big pack), each bursting out with a pop, under grey-white smoke.
        public static SparkSpec BatterySparks(double ec)
        {
            if(ec<20) return null;
            double l=Log(ec/100);
            return new SparkSpec { Jets=(int)Numbers.Clamp(1+Math.Floor(Log(ec/250)*1.2),1,4), Start=0.15, Seconds=Numbers.Clamp(1.2+1.6*Log(ec/200),1.2,7),
                Rate=Numbers.Clamp(150+120*l,150,600), Speed=Numbers.Clamp(14+5*l,14,30), Spread=0.25, Size=Numbers.Clamp(0.06+0.02*l,0.06,0.12), Life=0.9,
                Hot=new Vec3(1,0.95,0.8), Cool=new Vec3(1,0.6,0.2),
                FlameRate=25, FlameSize=Numbers.Clamp(0.3+0.15*l,0.3,0.9), Flame=new Vec3(1,0.55,0.18),
                SmokeRate=4, SmokeSize=Numbers.Clamp(0.8+0.4*l,0.8,2), SmokeAlpha=0.35, Smoke=new Vec3(0.78,0.78,0.8), PopFlash=Numbers.Clamp(0.5+0.2*l,0.5,1.2) };
        }

        // ---------------- Nuclear salt water ----------------
        public static WarheadInfo SaltWaterWarhead(double kg)
        {
            double tnt=Numbers.Clamp(kg*0.05,5,20000);
            return new WarheadInfo { Kind=WarheadKind.Bomb, TntKg=tnt, RangeM=Numbers.Clamp(7*Math.Pow(tnt,1.0/3),5,250) };
        }
        public static void ApplySaltWater(FxPlan plan,double units,double kg)
        {
            if(plan==null) return;
            plan.Exotic=ExoticKind.SaltWater; plan.ExoticUnits=units; plan.ExoticKg=kg;
            // A steam explosion: hardly any flame, a white cloud of steam with a faint grey haze (Chemistry.SmokeMix).
            plan.FireFraction*=0.12; plan.BurnableKg*=0.12; plan.Soot=0; plan.WhiteFlame=0; plan.OreKg=0;
            plan.VaporFraction=Math.Max(plan.VaporFraction,0.85);
            plan.SmokeR=0.9; plan.SmokeG=0.91; plan.SmokeB=0.93;
        }
        // The blue flashes of the excursion: at once, then weaker as the boiling solution falls back together.
        static readonly double[] SaltFlashTimes={0,0.45,1.2}, SaltFlashPower={2,0.8,0.5};
        public static double SaltWaterFlash(double age)
        {
            double f=0;
            for(int i=0;i<SaltFlashTimes.Length;i++) { double t=age-SaltFlashTimes[i]; if(t>=0) f+=SaltFlashPower[i]*Math.Exp(-t/0.08); }
            return f;
        }
        public static double SaltWaterEnd { get { return 2.5; } }

        // ---------------- Fission pulse units ----------------
        public const double PulseUnitKt=1;
        // A 1 kt unit's fireball radius (m; VolumeEvolution.FireballRadius for 1 kt): where the chain's flashes go off.
        public static double PulseUnitBallRadius { get { return Numbers.Clamp(61*Math.Pow(PulseUnitKt,0.4),30,3000); } }
        // How many go off: 3 for FFT's smallest magazine (1,400 units), up to 7.
        public static int PulseCount(double units)
        {
            if(units<1.5) return 1;
            return (int)Numbers.Clamp(Math.Round(2+Math.Log(units/700,2)),2,7);
        }
        public static WarheadInfo PulseWarhead(double units)
        {
            double tnt=PulseCount(units)*PulseUnitKt*1e6;
            return new WarheadInfo { Kind=WarheadKind.Nuke, TntKg=tnt, RangeM=Numbers.Clamp(900*Math.Pow(tnt/2e7,0.4),150,20000) };
        }
        public static void ApplyPulseUnits(FxPlan plan,double units)
        {
            if(plan==null) return;
            plan.Exotic=ExoticKind.PulseUnits; plan.ExoticUnits=units;
        }
        // When each unit goes off (real s) and where (in fireball radii, x/z across, y up), the same for the same seed.
        public static void PulseSchedule(double units,uint seed,double[] times,Vec3[] offsets,out int count)
        {
            count=Math.Min(PulseCount(units),Math.Min(times.Length,offsets.Length));
            var rnd=new SplitRandom(seed^0x50554C53u);
            double t=0;
            for(int i=0;i<count;i++)
            {
                times[i]=t; t+=0.12+0.28*rnd.Next();
                double a=rnd.Next()*Math.PI*2, r=i==0?0:0.5+0.9*rnd.Next();
                offsets[i]=new Vec3(Math.Cos(a)*r,i==0?0:(rnd.Next()-0.3)*0.8,Math.Sin(a)*r);
            }
        }
        // One unit's flash: blinding, then its fireball's glow.
        public static double PulseFlash(double sinceDetonation) { return sinceDetonation<0?0:2.5*Math.Exp(-sinceDetonation/0.09)+0.5*Math.Exp(-sinceDetonation/0.7); }
    }
}
