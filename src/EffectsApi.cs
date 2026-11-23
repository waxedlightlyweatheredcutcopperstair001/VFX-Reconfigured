using System;
using System.Collections.Generic;
using UnityEngine;
using VolumetricExplosionFX.Rendering;

namespace VFXReconfigured
{
    /// <summary>
    /// Effects other mods can ask for (Far Future Weaponry uses it, by reflection, so neither mod needs the other to build).
    ///
    /// Immediate mode: <see cref="Lens"/> and <see cref="Glow"/> draw for the frame they are called in (call them every
    /// frame from Update or LateUpdate, before the camera renders). They use the same pass as the Graviolium lens and the
    /// exotic flashes: drawn over everything, after the volumes and EVE's clouds.
    ///
    /// Looks: <see cref="SetPartLook"/> tags a weapon part so its explosion is drawn differently, whichever way it is seen
    /// (the part's destruction or BDArmory's detonation): "antimatter", "mushroom" (green flames, dark green smoke), or
    /// "none" (nothing drawn: the other mod draws its own effect), or "debris" (a part broken, not blown up: its wreckage
    /// only, unless it holds a warhead or an exotic fuel).
    /// </summary>
    public static class EffectsApi
    {
        /// <summary>
        /// 1: Lens, Glow, SetPartLook. 2: LensCore (the black core's size), SetLookAt (a look for a point).
        /// 3: TintSphere (a sphere tinting what's inside it, with a visible edge), TimeScale (explosions in a zone slowed
        /// or stopped).
        /// </summary>
        public const int Version = 3;

        /// <summary>
        /// A gravitational lens for this frame: the world behind it bent into an Einstein ring round a black core.
        /// radius: its reach (m); strength 0-1 (0: no lens; 1: Einstein ring at 0.4 radius, black core at 0.13 radius);
        /// ringRadius/ringAmp: a stretch-and-squeeze ring rippling outward (m, 0-1.2; 0 for none).
        /// </summary>
        public static bool Lens(Vector3 position, float radius, float strength, float ringRadius, float ringAmp) =>
            LensCore(position, radius, strength, 0.13f, ringRadius, ringAmp);

        /// <summary>
        /// As <see cref="Lens"/>, with the black core's radius as a share of the radius at full strength (0.13 in Lens; 0
        /// for none: bent space without a black hole).
        /// </summary>
        public static bool LensCore(Vector3 position, float radius, float strength, float coreShare, float ringRadius, float ringAmp)
        {
            if (LensPass.Material == null || radius <= 0) return false;
            float k = Mathf.Clamp01(strength);
            if (k < 1e-3f && ringAmp < 1e-3f) return false;
            float q = Mathf.Max(radius * 1.05f, ringRadius + 0.6f * radius);
            LensPass.Register(new LensPass.Lens
            {
                Position = position,
                Shape = new Vector4(q, radius, 0.4f * radius * k, Mathf.Clamp(coreShare, 0, 0.4f) * radius * k),
                Ring = new Vector4(ringRadius, Mathf.Max(ringAmp, 0), k, k)
            });
            return true;
        }

        /// <summary>
        /// A glow for this frame (a flash, the glow of hot or ionised air): radius (m), power (1 bright, 2-3 blinding),
        /// coreShare (0-1: how much of the radius is the white-hot core), edge and core colours.
        /// </summary>
        public static bool Glow(Vector3 position, float radius, float power, float coreShare, Color edge, Color core)
        {
            if (LensPass.GlowMaterial == null || radius <= 0 || power <= 0.003f) return false;
            LensPass.RegisterGlow(new LensPass.Glow { Position = position, Shape = new Vector4(radius, power, Mathf.Clamp01(coreShare), 0), Edge = edge, Core = core });
            return true;
        }

        /// <summary>
        /// A sphere for this frame that tints everything inside it (as seen from anywhere: the ground and craft inside,
        /// not what's beyond), with a crisp rim and a faint lattice on its surface, visible from inside too.
        /// tint: the colour things are multiplied towards, strength 0-1; edge: the edge's colour, edgeStrength (1 normal).
        /// </summary>
        public static bool TintSphere(Vector3 centre, float radius, Color tint, float strength, Color edge, float edgeStrength)
        {
            if (LensPass.TintMaterial == null || radius <= 0) return false;
            LensPass.RegisterTint(new LensPass.Tint
            {
                Centre = centre, Radius = radius,
                Colour = new Vector4(tint.r, tint.g, tint.b, Mathf.Clamp01(strength)),
                Edge = new Vector4(edge.r, edge.g, edge.b, Mathf.Max(0, edgeStrength))
            });
            return true;
        }

        // ---- Time zones ----

        struct Zone { public Vector3 Centre; public float Radius, Scale; }
        static readonly List<Zone> zones = new List<Zone>();
        static int zoneFrame = -1;

        /// <summary>
        /// For this frame: explosions (their volumes and particles) whose centre is within <paramref name="radius"/> of
        /// <paramref name="centre"/> run at <paramref name="scale"/> times their speed (0: stopped). Call it every frame
        /// while it should hold; where zones overlap the slowest wins.
        /// </summary>
        public static void TimeScale(Vector3 centre, float radius, float scale)
        {
            if (Time.frameCount != zoneFrame) { zones.Clear(); zoneFrame = Time.frameCount; }
            zones.Add(new Zone { Centre = centre, Radius = Mathf.Max(radius, 0), Scale = Mathf.Clamp01(scale) });
        }

        /// <summary>The time scale at a point this frame (1 outside every zone).</summary>
        internal static float ScaleAt(Vector3 at)
        {
            if (zoneFrame != Time.frameCount) return 1;
            float s = 1;
            foreach (Zone z in zones)
                if ((at - z.Centre).sqrMagnitude <= z.Radius * z.Radius && z.Scale < s) s = z.Scale;
            return s;
        }

        // ---- Looks ----

        sealed class Tag
        {
            public Part Part;
            public string Look;
            public Vector3 Where;
            public double DiedAt = -1;
            public float Within = -1;     // a point tag's own reach (SetLookAt); parts use the caller's
        }

        static readonly Dictionary<int, Tag> tags = new Dictionary<int, Tag>();
        static readonly List<int> stale = new List<int>();

        /// <summary>Tag a part's explosion with a look (null or "" clears it). Call when the part starts in flight.</summary>
        public static void SetPartLook(Part part, string look)
        {
            if (part == null) return;
            int id = part.GetInstanceID();
            if (string.IsNullOrEmpty(look)) { tags.Remove(id); return; }
            tags[id] = new Tag { Part = part, Look = look.ToLowerInvariant(), Where = part.transform.position };
        }

        /// <summary>
        /// A look for any explosion within <paramref name="within"/> m of a point over the next <paramref name="seconds"/>
        /// (an explosion the other mod sets off itself, e.g. through BDArmory, that it draws its own way).
        /// </summary>
        public static void SetLookAt(Vector3 position, float within, string look, float seconds)
        {
            if (string.IsNullOrEmpty(look)) return;
            int id = --pointIds;
            tags[id] = new Tag { Part = null, Look = look.ToLowerInvariant(), Where = position, Within = Mathf.Max(within, 1),
                                 DiedAt = Time.time - 5 + Mathf.Clamp(seconds, 0.1f, 5) };
        }

        static int pointIds;

        /// <summary>The look a part was tagged with, or null.</summary>
        internal static string LookOf(Part part)
        {
            if (part == null) return null;
            return tags.TryGetValue(part.GetInstanceID(), out Tag t) ? t.Look : null;
        }

        /// <summary>The tagged part died here (FlightController.Die): its look is still found near there for a few seconds.</summary>
        internal static void PartDied(Part part, Vector3 where)
        {
            if (part == null || !tags.TryGetValue(part.GetInstanceID(), out Tag t)) return;
            t.Where = where;
            t.DiedAt = Time.time;
        }

        /// <summary>
        /// The look of a tagged part that died at or near this point in the last 5 s (a part still in one piece, e.g. a
        /// bomb still on its plane, says nothing about explosions near it), or of a point tag, or null.
        /// </summary>
        internal static string LookNear(Vector3 position, float within)
        {
            string best = null;
            float bestD = within * within;
            foreach (Tag t in tags.Values)
            {
                if (t.Part != null && t.DiedAt < 0) continue;
                Vector3 at = t.Part != null ? t.Part.transform.position : t.Where;
                float d = (at - position).sqrMagnitude;
                if (t.Within > 0 && d > t.Within * t.Within) continue;
                if (d <= bestD || t.Within > 0) { bestD = Mathf.Min(bestD, d); best = t.Look; }
            }
            return best;
        }

        /// <summary>Each frame (FlightController.Update): remember where the tagged parts are; forget the long dead.</summary>
        internal static void Refresh()
        {
            if (tags.Count == 0) return;
            stale.Clear();
            double now = Time.time;
            foreach (var kv in tags)
            {
                Tag t = kv.Value;
                if (t.Part != null) { t.Where = t.Part.transform.position; continue; }
                if (t.DiedAt < 0) t.DiedAt = now;          // destroyed without dying (unloaded, or destroyed directly)
                else if (now - t.DiedAt > 5) stale.Add(kv.Key);
            }
            foreach (int id in stale) tags.Remove(id);
        }

        /// <summary>Floating-origin shift (FlightController.OnOriginShift): move the remembered points with the world.</summary>
        internal static void Shift(Vector3 by)
        {
            foreach (Tag t in tags.Values) if (t.Part == null) t.Where -= by;
        }

        /// <summary>Drop a part's tag (its warhead didn't go off: its look must not touch explosions near where it broke).</summary>
        internal static void Forget(Part part)
        {
            if (part != null) tags.Remove(part.GetInstanceID());
        }

        internal static void Clear() => tags.Clear();
    }
}
