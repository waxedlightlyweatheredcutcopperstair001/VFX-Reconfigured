Shader "VolumetricExplosionFX/FrontDepth"
{
    // VFX Reconfigured 0.11.1: the front of the explosion clouds (marched before the transparent pass into
    // _VefxLowResFront: eye depth times alpha, alpha) into the depth buffer where they are nearly opaque, so the
    // transparent things behind them (engine flames, particles) are hidden. Drawn by a renderer at queue 2999
    // (LowResVolumes): after Scatterer's sky and EVE's clouds (2998), which would otherwise not be drawn behind the
    // fireball's front. Draws no colour.
    SubShader
    {
        Tags { "Queue"="Transparent-1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZTest LEqual ZWrite On Cull Off ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma only_renderers d3d11 glcore metal
            #include "UnityCG.cginc"
            sampler2D _VefxLowResFront;
            struct v2f { float4 pos:SV_POSITION; float4 screen:TEXCOORD0; };
            v2f vert(float4 vertex:POSITION)
            {
                v2f o;
                // A triangle over the whole view in view space, twice the near plane away (inside the clip range of any
                // camera); through the camera's own projection, so the screen position matches the volumes' image.
                float k=2*_ProjectionParams.y;
                float3 v=float3(vertex.x*1.2*k/UNITY_MATRIX_P[0][0],vertex.y*1.2*k/UNITY_MATRIX_P[1][1],-k);
                o.pos=mul(UNITY_MATRIX_P,float4(v,1));
                o.screen=ComputeScreenPos(o.pos);
                return o;
            }
            float4 frag(v2f i,out float depth:SV_Depth):SV_Target
            {
                float4 f=tex2D(_VefxLowResFront,i.screen.xy/i.screen.w);
                clip(f.a-0.6);
                float eye=max(f.r/max(f.a,1e-4),0.05);
                depth=(1/eye-_ZBufferParams.w)/_ZBufferParams.z;   // linear eye depth back to the depth buffer's value
                return 0;
            }
            ENDCG
        }
    }
    Fallback Off
}
