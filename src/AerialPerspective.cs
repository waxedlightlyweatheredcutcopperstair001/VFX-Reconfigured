using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace VFXReconfigured
{
    /// <summary>
    /// Aerial perspective for the volumetric effects: the air between the camera and an explosion (or a ground cloud) dims
    /// it and veils it in the sky's colour, as Scatterer (or stock KSP's terrain) does for everything else. Without it,
    /// distant explosions stayed crisp and saturated over hazy blue land.
    ///
    /// Scatterer applies its scattering in a screen pass before the transparent queue, from the depth buffer; our effects
    /// are composited after it (on purpose: a translucent cloud would otherwise take the haze of the ground far behind it),
    /// and Scatterer has no way to apply its pass to other mods' effects. Its shaders are GPL and closed in recent versions,
    /// so this is an original analytic model (after Bruneton & Neyret 2008):
    ///   - optical depth integrated along the view ray through the body's real air density (KSP's pressure and
    ///     temperature curves): Rayleigh (wavelength-dependent: distance turns things blue) and Mie (grey haze, a
    ///     thinner, lower layer);
    ///   - out = colour x transmittance + sky colour x (1 - transmittance) x alpha (premultiplied);
    ///   - the sky colour follows the sun's height at the effect (dimmer and warmer near sunset, dark at night).
    /// Strength: the "Atmospheric haze" setting, times Scatterer's own post-processing strength for the body when found.
    /// </summary>
    public static class AerialPerspective
    {
        /// <summary>Overall strength (settings: Atmospheric haze). 0: off.</summary>
        public static float Strength = 1f;

        // Earth's sea-level scattering coefficients (1/m): Rayleigh per channel, Mie extinction.
        static readonly Vector3 BetaR = new Vector3(5.8e-6f, 1.35e-5f, 3.31e-5f);
        const float BetaM = 2.2e-5f;
        const float MieScaleRatio = 6.7f;   // Rayleigh scale height / Mie scale height (8 km / 1.2 km on Earth)
        const int Samples = 10;

        static CelestialBody cachedBody;
        static double seaLevelDensity;

        /// <summary>
        /// Transmittance (rgb; w = 1 when anything applies) and in-scattered sky colour (rgb) between the camera and a world
        /// point. False when there's nothing to apply (no atmosphere, too close, out of flight).
        /// </summary>
        public static bool Compute(Vector3 target, out Vector4 transmittance, out Vector4 inscatter)
        {
            transmittance = new Vector4(1, 1, 1, 0); inscatter = Vector4.zero;
            try
            {
                if (!HighLogic.LoadedSceneIsFlight || Strength <= 0) return false;
                CelestialBody body = FlightGlobals.currentMainBody;
                Camera cam = FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : null;
                if (body == null || !body.atmosphere || cam == null) return false;
                Vector3d a = cam.transform.position, b = target;
                double dist = (b - a).magnitude;
                if (dist < 30) return false;
                if (body != cachedBody) { cachedBody = body; seaLevelDensity = Density(body, 0); }
                if (seaLevelDensity <= 0) return false;

                double odR = 0, odM = 0;
                for (int i = 0; i < Samples; ++i)
                {
                    Vector3d p = Vector3d.Lerp(a, b, (i + 0.5) / Samples);
                    double h = (p - body.position).magnitude - body.Radius;
                    if (h >= body.atmosphereDepth) continue;
                    double r = Density(body, Math.Max(h, 0)) / seaLevelDensity;
                    odR += r; odM += Math.Pow(r, MieScaleRatio);
                }
                odR *= dist / Samples; odM *= dist / Samples;
                float k = Strength * ScattererStrength(body);
                Vector3 tau = BetaR * (float)(odR * k) + Vector3.one * (BetaM * (float)(odM * k));
                transmittance = new Vector4(Mathf.Exp(-tau.x), Mathf.Exp(-tau.y), Mathf.Exp(-tau.z), 1);
                if (transmittance.x > 0.995f && transmittance.z > 0.995f) { transmittance = new Vector4(1, 1, 1, 0); return false; }

                // The sky's colour at the effect: blue for clean (Rayleigh) air, whiter where the haze (Mie) dominates;
                // dimmer and warmer near sunset, a faint blue-grey at night.
                float mie = Mathf.Clamp01((float)(BetaM * odM / (BetaR.y * odR + BetaM * odM + 1e-12)));
                Color sky = Color.Lerp(new Color(0.56f, 0.69f, 0.92f), new Color(0.84f, 0.85f, 0.88f), mie);
                Vector3d up = (b - body.position).normalized;
                CelestialBody sun = Planetarium.fetch != null ? Planetarium.fetch.Sun : null;
                float sunUp = sun != null ? (float)Vector3d.Dot(up, (sun.position - b).normalized) : 1f;
                float day = Mathf.Clamp01(sunUp * 3 + 0.35f);
                float dusk = Mathf.Clamp01(1 - Mathf.Abs(sunUp) * 5) * 0.55f;
                sky = Color.Lerp(sky, new Color(0.95f, 0.62f, 0.42f), dusk) * Mathf.Lerp(0.04f, 1f, day);
                inscatter = new Vector4(sky.r, sky.g, sky.b, 1);
                return true;
            }
            catch (Exception) { transmittance = new Vector4(1, 1, 1, 0); inscatter = Vector4.zero; return false; }
        }

        static double Density(CelestialBody body, double altitude)
        {
            double pressure = body.GetPressure(altitude);
            return pressure > 0 ? body.GetDensity(pressure, body.GetTemperature(altitude)) : 0;
        }

        // ---- Scatterer's own strength for the body (reflection; 1 when not installed or not readable) ----
        static bool scattererLooked;
        static object scattererInstanceHolder;   // the Scatterer.Scatterer type (Instance is static)
        static Type scattererType;
        static CelestialBody scattererBody;
        static float scattererStrength = 1f;
        static float scattererCheckedAt = -100f;

        static float ScattererStrength(CelestialBody body)
        {
            if (!scattererLooked)
            {
                scattererLooked = true;
                foreach (var a in AssemblyLoader.loadedAssemblies)
                    if (a.name != null && a.name.Equals("scatterer", StringComparison.OrdinalIgnoreCase))
                    { scattererType = a.assembly.GetType("Scatterer.Scatterer"); break; }
            }
            if (scattererType == null) return 1f;
            if (body == scattererBody && Time.unscaledTime - scattererCheckedAt < 2f) return scattererStrength;
            scattererBody = body; scattererCheckedAt = Time.unscaledTime; scattererStrength = 1f;
            try
            {
                object instance = Get(scattererType, null, "Instance");
                object reader = Get(instance?.GetType(), instance, "planetsConfigsReader");
                if (Get(reader?.GetType(), reader, "scattererCelestialBodies") is IEnumerable bodies)
                    foreach (object entry in bodies)
                    {
                        if (entry == null || !(Get(entry.GetType(), entry, "celestialBody") is CelestialBody cb) || cb != body) continue;
                        if (Get(entry.GetType(), entry, "active") is bool active && !active) break;
                        object manager = Get(entry.GetType(), entry, "prolandManager");
                        object sky = Get(manager?.GetType(), manager, "skyNode");
                        object settings = Get(sky?.GetType(), sky, "interpolatedSettings");
                        if (Get(settings?.GetType(), settings, "postProcessAlpha") is float alpha && alpha > 0)
                            scattererStrength = Mathf.Clamp(alpha, 0.25f, 2f);
                        break;
                    }
            }
            catch (Exception) { scattererStrength = 1f; }
            return scattererStrength;
        }

        static object Get(Type type, object instance, string name)
        {
            if (type == null) return null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            FieldInfo f = type.GetField(name, flags);
            if (f != null) return f.GetValue(instance);
            PropertyInfo p = type.GetProperty(name, flags);
            return p != null && p.GetIndexParameters().Length == 0 ? p.GetValue(instance, null) : null;
        }
    }
}
