Shader "VolumetricExplosionFX/ExoticGlow"
{
    // VFX Reconfigured: the glow of an exotic explosion (Exotic.cs), anchored in the world on a camera-facing quad.
    //  - Antimatter: the air ionised by the annihilation's gamma rays over hundreds of metres glows violet-blue (nitrogen),
    //    a soft sphere with no edge round a white-hot core.
    //  - Graviolium: the electric-blue flashes as the lens forms and collapses.
    // The glow is a Gaussian ball of light: what the eye sees through it is its column, cut where the land or a part stands
    // inside it (the scene's depth against the ball's), so it sinks into the ground instead of being pasted over it.
    Properties
    {
        _ExGlow ("Radius (m), intensity, core size (share of the radius), -", Vector) = (100,1,0.15,0)
        _ExEdge ("Edge colour", Color) = (0.45,0.38,1,1)
        _ExCore ("Core colour", Color) = (0.95,0.92,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+41" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma only_renderers d3d11 glcore metal
            #include "UnityCG.cginc"
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _ExGlow, _ExEdge, _ExCore;
            struct v2f { float4 pos:SV_POSITION; float2 local:TEXCOORD0; float4 screen:TEXCOORD1; float2 eye:TEXCOORD2; };
            v2f vert(float4 vertex:POSITION)
            {
                v2f o;
                float3 centre=mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;
                float3 view=mul(UNITY_MATRIX_V,float4(centre,1)).xyz;
                float dist=max(-view.z,0.5);
                // (never wider than the view allows: inside the glow the quad would reach behind the eye)
                float size=min(_ExGlow.x,0.95*dist);
                view.xy+=vertex.xy*size;
                o.pos=mul(UNITY_MATRIX_P,float4(view,1));
                o.local=vertex.xy*size;
                o.screen=ComputeScreenPos(o.pos);
                o.eye=float2(dist,size/max(_ExGlow.x,1e-3));
                return o;
            }
            // erf, close enough for a soft edge.
            float Erf(float x) { return tanh(1.2027*x+0.0918*x*x*x); }
            float4 frag(v2f i):SV_Target
            {
                float R=max(_ExGlow.x*i.eye.y,1e-3), sigma=0.45*R;
                float b2=dot(i.local,i.local);
                float sceneEye=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                // Share of the ball's light in front of the scene along this ray.
                float column=0.5*(1+Erf((sceneEye-i.eye.x)/sigma));
                float glow=exp(-b2/(sigma*sigma));
                float sc=max(sigma*_ExGlow.z,1e-3);
                float core=exp(-b2/(sc*sc));
                float3 c=(_ExEdge.rgb*glow+_ExCore.rgb*core*2)*_ExGlow.y*column;
                return float4(c,0);
            }
            ENDCG
        }
    }
    Fallback Off
}
