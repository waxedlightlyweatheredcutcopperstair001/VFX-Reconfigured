using System;

namespace VolumetricExplosionFX.Core
{
    /// <summary>
    /// VFX Reconfigured: the explosion timing and look settings (Settings > Explosion look), read by the evolution code.
    ///
    /// - Speed: conventional explosions' fireball and smoke play this many times faster than VEFX 1.1.1 (the shockwave,
    ///   flying debris and their smoke trails keep their own pace). Nuclear clouds keep real time.
    /// - Glow: the burning gas stops glowing after GlowSeconds of real time (nuclear: NukeGlowSeconds); only fires
    ///   (fuel tanks, batteries) glow longer.
    /// - Smoke: once the glow is gone, smoke opacity halves every SmokeHalfLife seconds (nuclear: NukeSmokeHalfLife), and
    ///   a conventional cloud is gone after SmokeMaxSeconds at most.
    /// </summary>
    public static class Reconfig
    {
        /// <summary>The settings in use (set by the settings store whenever they're loaded or changed).</summary>
        public static FxSettings Current;

        static FxSettings S => Current ?? Defaults;
        static readonly FxSettings Defaults = new FxSettings();

        public static double Speed(FxPlan plan) => plan != null && plan.Warhead == WarheadKind.Nuke ? 1 : S.ExplosionSpeed;
        /// <summary>
        /// How long the gas glows (real seconds), by the explosion's size. Conventional: the setting is a medium explosion's
        /// (a fireball about 12 m across), scaling with the fireball radius^0.6 (a small missile's flashes for about half a
        /// second, a big one glows up to three times longer). Nuclear: the setting is a 20 kt bomb's, scaling with
        /// yield^0.4 (Glasstone & Dolan: a 1 Mt fireball glows for about a minute).
        /// </summary>
        public static double GlowSeconds(FxPlan plan)
        {
            if (plan != null && plan.Warhead == WarheadKind.Nuke)
                return S.NukeGlowSeconds * Numbers.Clamp(Math.Pow(Math.Max(plan.WarheadTnt / 1e6, 0.001) / 20, 0.4), 0.4, 5);
            double radius = plan != null ? VolumeEvolution.FireballRadius(plan) : 12;
            return S.GlowSeconds * Numbers.Clamp(Math.Pow(Math.Max(radius, 0.1) / 12, 0.6), 0.15, 3);
        }
        public static double Brightness => S.Brightness;
        public static double SmokeHalfLife(FxPlan plan) => plan != null && plan.Warhead == WarheadKind.Nuke ? S.NukeSmokeHalfLife : S.SmokeHalfLife;
        public static double SmokeOpacity => S.SmokeOpacity;
        public static double EdgeGlow => S.EdgeGlow;

        /// <summary>How much of the fire's glow is left at this real age (1 until 40% of the glow time, 0 at its end).</summary>
        public static double GlowLeft(FxPlan plan, double realAge)
        {
            double g = Math.Max(GlowSeconds(plan), 0.1);
            return 1 - Smooth(0.4 * g, g, realAge);
        }

        /// <summary>Smoke opacity left at this real age: halves every half-life once the glow is gone.</summary>
        public static double SmokeLeft(FxPlan plan, double realAge)
        {
            double after = Math.Max(0, realAge - GlowSeconds(plan));
            return Math.Pow(2, -after / Math.Max(SmokeHalfLife(plan), 0.25));
        }

        /// <summary>Longest a conventional cloud may last, in real seconds (nuclear clouds: unlimited here).</summary>
        public static double MaxSeconds(FxPlan plan) => plan != null && plan.Warhead == WarheadKind.Nuke ? double.MaxValue : S.SmokeMaxSeconds;

        static double Smooth(double a, double b, double x)
        {
            double t = Numbers.Clamp((x - a) / (b - a), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
