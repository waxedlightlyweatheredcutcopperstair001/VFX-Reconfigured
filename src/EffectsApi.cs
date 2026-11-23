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
    /// "none" (nothing drawn: the other mod draws its own effect).
    /// </summary>
    public static class EffectsApi
    {
        /// <summary>1: Lens, Glow, SetPartLook.</summary>
        public const int Version = 1;

        /// <summary>
        /// A gravitational lens for this frame: the world behind it bent into an Einstein ring round a black core.
        /// radius: its reach (m); strength 0-1 (0: no lens; 1: Einstein ring at 0.4 radius, black core at 0.13 radius);
        /// ringRadius/ringAmp: a stretch-and-squeeze ring rippling outward (m, 0-1.2; 0 for none).
        /// </summary>
        public static bool Lens(Vector3 position, float radius, float strength, float ringRadius, float ringAmp)
        {
            if (LensPass.Material == null || radius <= 0) return false;
            float k = Mathf.Clamp01(strength);
            if (k < 1e-3f && ringAmp < 1e-3f) return false;
            float q = Mathf.Max(radius * 1.05f, ringRadius + 0.6f * radius);
            LensPass.Register(new LensPass.Lens
            {
                Position = position,
                Shape = new Vector4(q, radius, 0.4f * radius * k, 0.13f * radius * k),
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

        // ---- Looks ----

        sealed class Tag
        {
            public Part Part;
            public string Look;
            public Vector3 Where;
            public double DiedAt = -1;
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

        /// <summary>The look of a tagged part at or near this point (alive, or died in the last 5 s), or null.</summary>
        internal static string LookNear(Vector3 position, float within)
        {
            string best = null;
            float bestD = within * within;
            foreach (Tag t in tags.Values)
            {
                Vector3 at = t.Part != null ? t.Part.transform.position : t.Where;
                float d = (at - position).sqrMagnitude;
                if (d <= bestD) { bestD = d; best = t.Look; }
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

        internal static void Clear() => tags.Clear();
    }
}
