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
    public enum ExoticKind { None, Antimatter, Graviolium }
    public struct ExoticInfo
    {
        public ExoticKind Kind;
        public double Units;   // the resource's amount in the part
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
        public const string AntimatterName="Antimatter", GravioliumName="Graviolium";
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
            double anti=0, grav=0;
            for(int i=0;i<p.Resources.Length;i++)
            {
                ResourceSnapshot r=p.Resources[i]; if(r==null||r.Name==null) continue;
                if(string.Equals(r.Name,AntimatterName,StringComparison.OrdinalIgnoreCase)) anti+=r.Amount;
                else if(string.Equals(r.Name,GravioliumName,StringComparison.OrdinalIgnoreCase)) grav+=r.Amount;
            }
            if(anti>=MinUnits) { info.Kind=ExoticKind.Antimatter; info.Units=anti; }
            else if(grav>=MinUnits) { info.Kind=ExoticKind.Graviolium; info.Units=grav; }
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
    }
}
