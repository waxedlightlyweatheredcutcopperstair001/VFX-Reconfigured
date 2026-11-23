using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace VolumetricExplosionFX.Rendering
{
    // VFX Reconfigured: draws the gravitational lenses of Graviolium explosions (GravLens.shader) on the flight camera,
    // after everything else of the transparent pass is composited (the volumes, EVE's clouds): one command buffer kept
    // last at AfterForwardAlpha. Per lens: the image is copied, then the lens quad writes it back bent. Lenses register
    // every frame (ExoticFx.Tick); a frame without any leaves the buffer empty.
    // It also draws the short flashes of exotic explosions and batteries (ExoticGlow.shader) over everything: in the
    // transparent pass they were covered by the explosion's own volume, composited after it (a battery's blue arc flash
    // never showed).
    internal static class LensPass
    {
        internal struct Lens { public Vector3 Position; public Vector4 Shape, Ring; }
        internal struct Glow { public Vector3 Position; public Vector4 Shape; public Color Edge, Core; }
        // VFX Reconfigured: a sphere tinting what is inside it (TintSphere.shader), for other mods (EffectsApi.TintSphere).
        internal struct Tint { public Vector3 Centre; public float Radius; public Vector4 Colour, Edge; }
        static readonly List<Tint> tints=new List<Tint>();
        static int tintFrame=-1;
        internal static Material TintMaterial;
        static readonly int TsSphereId=Shader.PropertyToID("_TsSphere"), TsTintId=Shader.PropertyToID("_TsTint"), TsEdgeId=Shader.PropertyToID("_TsEdge");
        internal static void RegisterTint(Tint tint)
        {
            if(Time.frameCount!=tintFrame) { tints.Clear(); tintFrame=Time.frameCount; }
            tints.Add(tint);
        }
        static readonly List<Lens> lenses=new List<Lens>();
        static readonly List<Glow> glows=new List<Glow>();
        static int listFrame=-1, glowFrame=-1;
        internal static Material GlowMaterial;
        static readonly int GlowShapeId=Shader.PropertyToID("_ExGlow"), EdgeId=Shader.PropertyToID("_ExEdge"), CoreId=Shader.PropertyToID("_ExCore");
        internal static void RegisterGlow(Glow glow)
        {
            if(Time.frameCount!=glowFrame) { glows.Clear(); glowFrame=Time.frameCount; }
            glows.Add(glow);
        }
        static CommandBuffer buffer;
        static Camera attached;
        static readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        static readonly int SrcId=Shader.PropertyToID("_VefxLensSrc"), LensId=Shader.PropertyToID("_Lens"), RingId=Shader.PropertyToID("_LensRing");
        static Mesh quad;
        internal static Material Material;
        internal static void Register(Lens lens)
        {
            if(Time.frameCount!=listFrame) { lenses.Clear(); listFrame=Time.frameCount; }
            lenses.Add(lens);
        }
        // From FxPool's pre-cull of the flight camera, after the volumes' buffers are filled.
        internal static void PreCull(Camera cam)
        {
            if(cam==null||cam!=CameraRig.Near) return;
            bool anyLens=Material!=null&&listFrame==Time.frameCount&&lenses.Count>0;
            bool anyGlow=GlowMaterial!=null&&glowFrame==Time.frameCount&&glows.Count>0;
            bool anyTint=TintMaterial!=null&&tintFrame==Time.frameCount&&tints.Count>0;
            bool any=anyLens||anyGlow||anyTint;
            if(!any) { if(buffer!=null) buffer.Clear(); return; }
            if(buffer==null) buffer=new CommandBuffer { name="VFXR gravitational lens" };
            if(attached!=cam)
            {
                if(attached!=null) attached.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha,buffer);
                attached=cam; cam.AddCommandBuffer(CameraEvent.AfterForwardAlpha,buffer);
            }
            else
            {
                // Kept last: other mods (and the volumes' composite) add theirs later.
                CommandBuffer[] all=cam.GetCommandBuffers(CameraEvent.AfterForwardAlpha);
                if(all.Length==0||all[all.Length-1]!=buffer) { cam.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha,buffer); cam.AddCommandBuffer(CameraEvent.AfterForwardAlpha,buffer); }
            }
            if(quad==null)
            {
                quad=new Mesh { name="VFXR lens quad" };
                quad.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
                quad.triangles=new[]{0,1,2,0,2,3}; quad.bounds=new Bounds(Vector3.zero,Vector3.one*2e5f);
            }
            buffer.Clear();
            if(anyGlow||anyTint)
            {
                // (the buffers before this one may have left a full-screen projection and another target)
                buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                buffer.SetViewProjectionMatrices(cam.worldToCameraMatrix,cam.projectionMatrix);
            }
            // Tinting spheres first (the flashes and lenses are not tinted): multiply, then the edge added.
            if(anyTint)
                for(int i=0;i<tints.Count;i++)
                {
                    Tint t=tints[i];
                    block.Clear(); block.SetVector(TsSphereId,new Vector4(t.Centre.x,t.Centre.y,t.Centre.z,t.Radius));
                    block.SetVector(TsTintId,t.Colour); block.SetVector(TsEdgeId,t.Edge);
                    buffer.DrawMesh(quad,Matrix4x4.identity,TintMaterial,0,0,block);
                    buffer.DrawMesh(quad,Matrix4x4.identity,TintMaterial,0,1,block);
                }
            if(anyGlow)
                for(int i=0;i<glows.Count;i++)
                {
                    Glow g=glows[i];
                    block.Clear(); block.SetVector(GlowShapeId,g.Shape); block.SetColor(EdgeId,g.Edge); block.SetColor(CoreId,g.Core);
                    buffer.DrawMesh(quad,Matrix4x4.Translate(g.Position),GlowMaterial,0,0,block);
                }
            if(!anyLens) return;
            buffer.GetTemporaryRT(SrcId,-1,-1,0,FilterMode.Bilinear,RenderTextureFormat.DefaultHDR);
            Matrix4x4 view=cam.worldToCameraMatrix, projection=cam.projectionMatrix;
            for(int i=0;i<lenses.Count;i++)
            {
                Lens l=lenses[i];
                buffer.Blit(BuiltinRenderTextureType.CameraTarget,SrcId);
                buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                buffer.SetViewProjectionMatrices(view,projection);   // (the copy left a full-screen projection)
                buffer.SetGlobalTexture(SrcId,SrcId);
                block.Clear(); block.SetVector(LensId,l.Shape); block.SetVector(RingId,l.Ring);
                buffer.DrawMesh(quad,Matrix4x4.Translate(l.Position),Material,0,0,block);
            }
            buffer.ReleaseTemporaryRT(SrcId);
        }
        internal static void Dispose()
        {
            if(attached!=null&&buffer!=null) attached.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha,buffer);
            attached=null;
            if(buffer!=null) { buffer.Release(); buffer=null; }
            if(quad!=null) { Object.Destroy(quad); quad=null; }
            lenses.Clear(); glows.Clear(); tints.Clear(); Material=null; GlowMaterial=null; TintMaterial=null;
        }
    }
}
