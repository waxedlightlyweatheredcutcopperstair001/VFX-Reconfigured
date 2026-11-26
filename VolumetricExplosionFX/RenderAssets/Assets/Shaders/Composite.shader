Shader "VolumetricExplosionFX/Composite"
{
    // Lays the reduced-resolution volume image (premultiplied colour, alpha) over the scene.
    Properties { _MainTex ("Reduced-resolution volumes", 2D) = "black" {} }
    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #pragma only_renderers d3d11 glcore metal
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _VefxCompositeSpread;   // tap offset in texels (0.6 reduced resolution, wider at full resolution)
            sampler2D _VefxOverlay, _VefxOverlayZ; float _VefxOverlayOn;   // 1.1: engine flames in front of the clouds, drawn again over them
            // 1.1: hidden behind another mod's clouds (LowResVolumes).
            // VFX Reconfigured 0.11.1: EVE's own cloud image (alpha: the light that gets through the clouds on the pixel,
            // white where there are none) and the layers' altitudes: the share of the ray's cloud in front of the volume's
            // front is the share of the ray's length inside the layers that lies before the front (up to the scene's depth,
            // as EVE marches), and the volume is dimmed by alpha ^ share (Beer-Lambert, even density along the ray).
            sampler2D _VefxLowResFront, scattererReconstructedCloud; float4 _VefxLowResFront_TexelSize;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float _VefxCloudOcc; float4 _VefxCloudLo, _VefxCloudHi, _VefxEyeUp, _VefxPlanetRadius; float4x4 _VefxFrustum;
            // Length of the ray [0, len] inside the sphere of altitude h (radius R + h); the eye at altitude alt, b = the
            // eye's distance from the centre times the cosine of the ray to the zenith. (The altitude difference is exact:
            // the sphere's equation written from it keeps float precision at a planet's radius.)
            float InSphere(float b,float alt,float h,float R,float len)
            {
                float q=(alt-h)*(2*R+alt+h);
                float disc=b*b-q;
                if(disc<=0) return 0;
                float s=sqrt(disc);
                return max(min(-b+s,len)-max(-b-s,0),0);
            }
            float InLayers(float b,float alt,float R,float len)
            {
                float sum=0;
                [unroll] for(int k=0;k<4;k++)
                    if(_VefxCloudHi[k]>_VefxCloudLo[k]) sum+=max(InSphere(b,alt,_VefxCloudHi[k],R,len)-InSphere(b,alt,_VefxCloudLo[k],R,len),0);
                return sum;
            }
            // How much of the volume still shows through EVE's clouds in front of it (1: none in front).
            float CloudThrough(float2 uv)
            {
                // (the thin fringe has no front of its own: the nearest of its neighbours' is taken, or it showed as an outline)
                float4 f=tex2D(_VefxLowResFront,uv);
                float2 o=_VefxLowResFront_TexelSize.xy*2;
                float4 g=tex2D(_VefxLowResFront,uv+o); if(g.a>f.a) f=g;
                g=tex2D(_VefxLowResFront,uv-o); if(g.a>f.a) f=g;
                g=tex2D(_VefxLowResFront,uv+float2(o.x,-o.y)); if(g.a>f.a) f=g;
                g=tex2D(_VefxLowResFront,uv+float2(-o.x,o.y)); if(g.a>f.a) f=g;
                if(f.a<0.05) return 1;
                float through=saturate(tex2Dlod(scattererReconstructedCloud,float4(uv,0,0)).a);
                if(through>0.995) return 1;
                float3 ray=lerp(lerp(_VefxFrustum[0].xyz,_VefxFrustum[3].xyz,uv.x),lerp(_VefxFrustum[1].xyz,_VefxFrustum[2].xyz,uv.x),uv.y);
                float k=max(length(ray),1e-4);
                float front=clamp(f.r/f.a,0,2e6)*k;
                // (the sky's depth is the far plane: kept finite)
                float scene=max(min(LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture,float4(uv,0,0))),2e6)*k,front);
                float R=_VefxPlanetRadius.x, alt=_VefxEyeUp.w;
                float b=(R+alt)*dot(_VefxEyeUp.xyz,ray/k);
                float total=InLayers(b,alt,R,scene);
                if(total<1) return 1;
                float share=saturate(InLayers(b,alt,R,front)/total);
                return exp2(share*log2(max(through,1e-3)));
            }
            float4 Volumes(v2f_img i);
            float4 frag(v2f_img i):SV_Target
            {
                float4 c=Volumes(i);
                if(_VefxCloudOcc>0.5) c*=CloudThrough(i.uv);
                // What the cloud hid of a flame in front of it comes back (the flame was drawn into the image before), where
                // the flame is nearer than the cloud's front.
                if(_VefxOverlayOn>0.5)
                {
                    float oz=tex2D(_VefxOverlayZ,i.uv).r;
                    float4 f=tex2D(_VefxLowResFront,i.uv);
                    float front=f.a>0.05?f.r/f.a:1e7;
                    c.rgb+=tex2D(_VefxOverlay,i.uv).rgb*c.a*saturate((front-oz)/(0.03*front+2)+0.5);
                }
                return c;
            }
            // Centre plus four diagonal bilinear taps: hides the coarser ray-march dither at reduced resolution.
            float4 Volumes(v2f_img i)
            {
                // Full resolution (1.1): a small Gaussian over 3x3 bilinear taps (about 5x5 pixels). Four diagonal taps
                // alone left a regular cross-hatch, like a halftone screen, over the blue-noise dither.
                if(_VefxCompositeSpread>1)
                {
                    float2 g=_MainTex_TexelSize.xy*_VefxCompositeSpread;
                    float4 c=tex2D(_MainTex,i.uv)*0.25;
                    c+=(tex2D(_MainTex,i.uv+float2(g.x,0))+tex2D(_MainTex,i.uv-float2(g.x,0))+
                        tex2D(_MainTex,i.uv+float2(0,g.y))+tex2D(_MainTex,i.uv-float2(0,g.y)))*0.125;
                    c+=(tex2D(_MainTex,i.uv+g)+tex2D(_MainTex,i.uv-g)+
                        tex2D(_MainTex,i.uv+float2(g.x,-g.y))+tex2D(_MainTex,i.uv+float2(-g.x,g.y)))*0.0625;
                    return c;
                }
                float2 o=_MainTex_TexelSize.xy*(_VefxCompositeSpread>0?_VefxCompositeSpread:0.6);
                float4 c=tex2D(_MainTex,i.uv)*0.4;
                c+=(tex2D(_MainTex,i.uv+float2(o.x,o.y))+tex2D(_MainTex,i.uv+float2(-o.x,o.y))+
                    tex2D(_MainTex,i.uv+float2(o.x,-o.y))+tex2D(_MainTex,i.uv+float2(-o.x,-o.y)))*0.15;
                return c;
            }
            ENDCG
        }
        Pass
        {
            // 1.1: the cloud's front into the depth buffer, where it is nearly opaque (drawn before the transparent pass).
            // VFX Reconfigured 0.11.1: only with a shader bundle that has no FrontDepth shader (FrontDepth.shader does it
            // at queue 2999 now).
            ZTest LEqual ZWrite On Cull Off ColorMask 0
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #pragma only_renderers d3d11 glcore metal
            #include "UnityCG.cginc"
            sampler2D _VefxLowResFront;
            float4 frag(v2f_img i,out float depth:SV_Depth):SV_Target
            {
                float4 f=tex2D(_VefxLowResFront,i.uv);
                clip(f.a-0.6);
                float eye=max(f.r/max(f.a,1e-4),0.05);
                depth=(1/eye-_ZBufferParams.w)/_ZBufferParams.z;   // linear eye depth back to the depth buffer's value
                return 0;
            }
            ENDCG
        }
        Pass
        {
            // 1.1: the eye depth of the flames and trails drawn again over the clouds, nearest kept (drawn with
            // each overlay's own mesh). Pass 0 shows an overlay only where it is nearer than the cloud's front. (Their own
            // depth test did not hold: the flames and contrails behind a nuclear cloud showed through it, in the game and at
            // the bench.)
            ZTest Always ZWrite Off Cull Off
            BlendOp Min
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma only_renderers d3d11 glcore metal
            #include "UnityCG.cginc"
            struct v2f { float4 pos:SV_POSITION; float eye:TEXCOORD0; };
            v2f vert(float4 vertex:POSITION)
            {
                v2f o; o.pos=UnityObjectToClipPos(vertex); o.eye=-UnityObjectToViewPos(vertex.xyz).z; return o;
            }
            float4 frag(v2f i):SV_Target { return float4(i.eye,0,0,0); }
            ENDCG
        }
        Pass
        {
            // The same, over each flame's bounding box at the depth of its centre: a flame whose shader moves its
            // vertices (PlumeDynamics bends Waterfall's flames on the GPU, from a mesh laid out for it) is not where its mesh
            // is, and its pixels stayed "behind" every cloud. Nearest kept with pass 2.
            ZTest Always ZWrite Off Cull Off
            BlendOp Min
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma only_renderers d3d11 glcore metal
            #include "UnityCG.cginc"
            float _VefxOverlayCentre;   // eye depth of the flame's centre
            float4 vert(float4 vertex:POSITION):SV_POSITION { return UnityObjectToClipPos(vertex); }
            float4 frag():SV_Target { return float4(_VefxOverlayCentre,0,0,0); }
            ENDCG
        }
    }
    Fallback Off
}
