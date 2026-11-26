using System;
using System.Reflection;
using UnityEngine;
namespace VolumetricExplosionFX.Ksp
{
    // 1.1: the altitudes of the raymarched cloud layers of Environmental Visual Enhancements over the current body,
    // read through its public members by reflection (nothing of it is copied, and nothing is called but getters).
    // VFX Reconfigured 0.11.1: EVE lays its clouds over the scene at render queue 2998 (start of the transparent pass), and
    // VEFX lays its volumes over the image after the whole pass, so a nuclear cloud behind a cloud deck was drawn over it.
    // LowResVolumes now hides the volumes with EVE's own cloud image (how much light gets through the clouds on each
    // pixel) and these layers (how much of the cloud on that ray lies in front of the explosion). Nothing without EVE.
    internal static class CloudLayers
    {
        internal const int Max=4;
        internal static bool Known { get; private set; }
        // Up to four layers (altitudes, m; overlapping layers merged): bottoms in Lo, tops in Hi, unused ones 0-0.
        internal static Vector4 Lo { get; private set; }
        internal static Vector4 Hi { get; private set; }
        static Type cloudsObject; static FieldInfo bodyField, layerField; static PropertyInfo inner, outer, planet;
        static bool searched, failed; static float nextScan; static string scannedBody, logged;
        internal static void Refresh(CelestialBody body)
        {
            if(failed||body==null) { Known=false; return; }
            if(Time.realtimeSinceStartup<nextScan&&scannedBody==body.bodyName) return;
            nextScan=Time.realtimeSinceStartup+5; scannedBody=body.bodyName;
            try
            {
                if(!searched) { searched=true; Find(); }
                if(cloudsObject==null||bodyField==null||layerField==null) { Known=false; return; }
                if(!reportedDraw) { reportedDraw=true; ReportCloudDraw(); }
                UnityEngine.Object[] objects=UnityEngine.Object.FindObjectsOfType(cloudsObject);
                var layers=new System.Collections.Generic.List<Vector2>(); int skipped=0; string all="";
                for(int i=0;i<objects.Length;i++)
                {
                    if(bodyField.GetValue(objects[i]) as string!=body.bodyName) continue;
                    object layer=layerField.GetValue(objects[i]);
                    if(layer==null) continue;
                    float r=(float)planet.GetValue(layer,null), a=(float)inner.GetValue(layer,null)-r, b=(float)outer.GetValue(layer,null)-r;
                    // Ground-level weather (fog, rain: -20 to 2150 m over Kerbin) is kept now: EVE's image says where it
                    // really is. Very tall layers (one reached 80 km) are not: the share of a ray's cloud in front of an
                    // explosion is taken from the ray's length inside the layers, and a mostly empty 80 km shell would swamp it.
                    if(r>0&&b>a&&b-a<=20000&&b<=30000) layers.Add(new Vector2(a,b)); else skipped++;
                    all+=(all.Length>0?", ":"")+a.ToString("0")+"-"+b.ToString("0")+" m";
                }
                // Overlapping layers are one (their length would count twice); then the closest are merged down to four.
                layers.Sort((x,y)=>x.x.CompareTo(y.x));
                var merged=new System.Collections.Generic.List<Vector2>();
                foreach(Vector2 l in layers)
                {
                    if(merged.Count>0&&l.x<=merged[merged.Count-1].y) merged[merged.Count-1]=new Vector2(merged[merged.Count-1].x,Mathf.Max(merged[merged.Count-1].y,l.y));
                    else merged.Add(l);
                }
                while(merged.Count>Max)
                {
                    int k=0; float gap=float.MaxValue;
                    for(int i=0;i+1<merged.Count;i++) if(merged[i+1].x-merged[i].y<gap) { gap=merged[i+1].x-merged[i].y; k=i; }
                    merged[k]=new Vector2(merged[k].x,Mathf.Max(merged[k].y,merged[k+1].y)); merged.RemoveAt(k+1);
                }
                Known=merged.Count>0;
                Vector4 lo=Vector4.zero, hi=Vector4.zero; string text="";
                for(int i=0;i<merged.Count;i++) { lo[i]=merged[i].x; hi[i]=merged[i].y; text+=(i>0?", ":"")+merged[i].x.ToString("0")+"-"+merged[i].y.ToString("0")+" m"; }
                Lo=lo; Hi=hi;
                text=Known?body.bodyName+" "+text:objects.Length>0&&skipped>0?body.bodyName+" none used":body.bodyName+" none found ("+objects.Length+" EVE cloud object(s) in the scene)";
                if(skipped>0) text+=" ("+skipped+" very tall layer(s) not used)";
                text+="; every layer: "+(all.Length>0?all:"none");
                if(text!=logged) { logged=text; Debug.Log("[VEFX] Cloud layers (EVE): "+text+(Known?": explosions behind EVE's clouds in these layers are hidden by them":"")); }
            }
            catch(Exception ex)
            {
                failed=true; Known=false;
                Debug.LogWarning("[VEFX] Cloud layers unreadable ("+ex.GetType().Name+"): explosions are drawn over the clouds");
            }
        }
        // VFX Reconfigured: when the clouds are drawn (EVE's composite material and its queue), logged once, with the render stack.
        static bool reportedDraw;
        static void ReportCloudDraw()
        {
            var text=new System.Text.StringBuilder("[VFXR] EVE cloud materials (shader, queue):");
            var seen=new System.Collections.Generic.HashSet<string>();
            foreach(Renderer r in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if(r==null) continue;
                foreach(Material m in r.sharedMaterials)
                {
                    if(m==null||m.shader==null) continue;
                    string n=m.shader.name;
                    if(n.IndexOf("cloud",StringComparison.OrdinalIgnoreCase)<0&&n.IndexOf("raymarch",StringComparison.OrdinalIgnoreCase)<0&&n.IndexOf("EVE",StringComparison.Ordinal)<0) continue;
                    string entry=n+" "+m.renderQueue+(r.enabled?"":" (off)");
                    if(seen.Add(entry)) text.Append(" | "+entry);
                }
            }
            if(seen.Count==0) text.Append(" none on renderers (drawn by command buffers: see the render stack)");
            Debug.Log(text.ToString());
        }
        static void Find()
        {
            foreach(AssemblyLoader.LoadedAssembly a in AssemblyLoader.loadedAssemblies)
            {
                if(a.name!="Atmosphere") continue;
                cloudsObject=a.assembly.GetType("Atmosphere.CloudsObject");
                Type volume=a.assembly.GetType("Atmosphere.CloudsRaymarchedVolume");
                if(cloudsObject==null||volume==null)
                {
                    Debug.Log("[VFXR] EVE found, but not its raymarched cloud types (CloudsObject "+(cloudsObject!=null)+", CloudsRaymarchedVolume "+(volume!=null)+"): explosions are drawn over the clouds");
                    cloudsObject=null; return;
                }
                const BindingFlags any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
                bodyField=cloudsObject.GetField("body",any); layerField=cloudsObject.GetField("layerRaymarchedVolumeV5",any);
                inner=volume.GetProperty("InnerSphereRadius",BindingFlags.Public|BindingFlags.Instance);
                outer=volume.GetProperty("OuterSphereRadius",BindingFlags.Public|BindingFlags.Instance);
                planet=volume.GetProperty("PlanetRadius",BindingFlags.Public|BindingFlags.Instance);
                if(inner==null||outer==null||planet==null||bodyField==null||layerField==null)
                {
                    var fields=new System.Text.StringBuilder();
                    foreach(FieldInfo f in cloudsObject.GetFields(any)) fields.Append(f.Name+":"+f.FieldType.Name+" ");
                    Debug.Log("[VFXR] EVE's cloud layers not readable (body "+(bodyField!=null)+", layer "+(layerField!=null)+", radii "+(inner!=null&&outer!=null&&planet!=null)+
                        "): explosions are drawn over the clouds. CloudsObject fields: "+fields);
                    cloudsObject=null;
                }
                return;
            }
            Debug.Log("[VFXR] EVE (Atmosphere.dll) not loaded: no cloud layers to hide explosions behind");
        }
    }
}
