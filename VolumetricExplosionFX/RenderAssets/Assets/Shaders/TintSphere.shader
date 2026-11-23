Shader "VolumetricExplosionFX/TintSphere"
{
    // VFX Reconfigured: a sphere that tints what is inside it, for other mods (EffectsApi.TintSphere; Far Future
    // Weaponry's tachyon field). Drawn on a quad over the whole view, in view space (the eye at the origin):
    //  - pass 0 multiplies the image by the tint where the view ray runs through the sphere, by how far it runs inside
    //    (cut where the scene's depth ends it: the ground and craft inside are tinted, things beyond are not);
    //  - pass 1 adds the sphere's edge: a sharp rim where the ray grazes it, and a faint lattice on its surface, seen
    //    from outside and from inside.
    Properties
    {
        _TsSphere ("Centre (world), radius (m)", Vector) = (0,0,0,100)
        _TsTint ("Tint colour, strength", Vector) = (0.6,1,0.65,1)
        _TsEdge ("Edge colour, strength", Vector) = (0.3,1,0.45,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+40" "RenderType"="Transparent" "IgnoreProjector"="True" }
        CGINCLUDE
        #include "UnityCG.cginc"
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        float4 _TsSphere, _TsTint, _TsEdge;
        struct v2f { float4 pos:SV_POSITION; float3 view:TEXCOORD0; float4 screen:TEXCOORD1; };
        v2f vert(float4 vertex:POSITION)
        {
            v2f o;
            // A quad one metre in front of the eye, wider than the view.
            float3 v=float3(vertex.x*1.5/UNITY_MATRIX_P[0][0],vertex.y*1.5/UNITY_MATRIX_P[1][1],-1);
            o.pos=mul(UNITY_MATRIX_P,float4(v,1));
            o.view=v;
            o.screen=ComputeScreenPos(o.pos);
            return o;
        }
        // The ray from the eye through this pixel against the sphere: entry and exit distances (t0 > t1 when it misses),
        // and how far away the scene is along the ray (the sky's depth is the far plane: kept finite).
        void Ray(v2f i,out float3 d,out float3 c,out float t0,out float t1,out float scene,out float h)
        {
            d=normalize(i.view);
            c=mul(UNITY_MATRIX_V,float4(_TsSphere.xyz,1)).xyz;
            float R=max(_TsSphere.w,1e-3);
            float b=dot(c,d);
            float h2=max(dot(c,c)-b*b,0);
            h=sqrt(h2);
            float s=sqrt(max(R*R-h2,0));
            t0=h2<R*R?b-s:1;
            t1=h2<R*R?b+s:0;
            float eye=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
            scene=min(eye/max(-d.z,1e-4),1e7);
        }
        float Line(float x)
        {
            float w=max(0.04,fwidth(x)*1.5);
            float y=abs(frac(x+0.5)-0.5);
            return 1-smoothstep(0,w,y);
        }
        // The edge's light where the ray crosses the surface at distance t (if that is in front of the scene).
        float Surface(float3 d,float3 c,float t,float scene)
        {
            if(t<=0||t>=scene) return 0;
            float R=max(_TsSphere.w,1e-3);
            float3 nv=(d*t-c)/R;
            // (back to world axes, so the lattice stays put when the camera turns: the view matrix's rotation is orthonormal)
            float3 nw=mul(transpose((float3x3)UNITY_MATRIX_V),nv);
            float lat=asin(clamp(nw.y,-1,1))*(18/3.14159265), lon=atan2(nw.z,nw.x)*(18/3.14159265);
            float lines=max(Line(lat),Line(lon)*saturate(1.5-abs(nw.y)));
            float graze=1-abs(dot(nv,d));
            return 0.1+0.35*lines+0.9*pow(graze,4);
        }
        ENDCG
        Pass
        {
            // The tint: multiplies what is behind and inside.
            Cull Off ZWrite Off ZTest Always
            Blend DstColor Zero
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma only_renderers d3d11 glcore metal
            float4 frag(v2f i):SV_Target
            {
                float3 d,c; float t0,t1,scene,h;
                Ray(i,d,c,t0,t1,scene,h);
                float len=max(0,min(t1,scene)-max(t0,0));
                float a=saturate(_TsTint.w)*(1-exp(-len/(0.6*max(_TsSphere.w,1e-3))));
                return float4(lerp(float3(1,1,1),saturate(_TsTint.rgb),a),1);
            }
            ENDCG
        }
        Pass
        {
            // The edge: a sharp rim and a faint lattice, added.
            Cull Off ZWrite Off ZTest Always
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma only_renderers d3d11 glcore metal
            float4 frag(v2f i):SV_Target
            {
                float3 d,c; float t0,t1,scene,h;
                Ray(i,d,c,t0,t1,scene,h);
                if(t0>t1) return 0;
                float R=max(_TsSphere.w,1e-3);
                float e=Surface(d,c,t0,scene)+Surface(d,c,t1,scene);
                // The silhouette, where the ray grazes the sphere: a crisp bright line.
                float b=dot(c,d);
                if(b>0&&b<scene) e+=1.5*smoothstep(0.965,0.998,h/R)*(1-smoothstep(0.998,1,h/R));
                return float4(max(_TsEdge.rgb*_TsEdge.w*e,0),0);
            }
            ENDCG
        }
    }
    Fallback Off
}
