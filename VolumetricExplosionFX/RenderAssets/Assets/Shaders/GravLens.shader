Shader "VolumetricExplosionFX/GravLens"
{
    // VFX Reconfigured: the gravitational lens of a Graviolium explosion (Exotic.cs, LensPass.cs). Drawn by a command
    // buffer after the volumes and the clouds are composited: it reads a copy of the image (_VefxLensSrc) and writes it back
    // bent, on a camera-facing quad round the lens, so it bends the land, the sky, the clouds, craft and explosions.
    //  - Point-mass lens: what is seen at offset x from the centre is what lies at x(1 - rE^2/|x|^2) without the lens. Just
    //    outside the Einstein radius rE the world is stretched into arcs round it; inside, it is seen magnified and turned
    //    over (from the far side). The deflection fades out towards the lens's reach (no seam at the quad's edge).
    //  - A small black core with a bright blue rim.
    //  - A gravitational-wave ring running outward: space stretched along one axis and squeezed along the other,
    //    alternating as the wave passes (far stronger than a shock wave's refraction).
    Properties
    {
        _Lens ("Quad half-size, reach, Einstein radius, core radius (m)", Vector) = (100,100,40,13)
        _LensRing ("Wave ring radius (m), strength, rim, blue tint", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma only_renderers d3d11 glcore metal
            #include "UnityCG.cginc"
            sampler2D _VefxLensSrc;
            float4 _Lens, _LensRing;
            struct v2f
            {
                float4 pos:SV_POSITION;
                float2 local:TEXCOORD0;     // offset from the centre on the camera-facing plane (m)
                float f:TEXCOORD1;          // radii scale (the eye inside the reach)
            };
            v2f vert(float4 vertex:POSITION)
            {
                v2f o;
                float3 centre=mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;
                float3 view=mul(UNITY_MATRIX_V,float4(centre,1)).xyz;
                float dist=max(-view.z,0.5);
                // (the eye inside the lens: the whole lens shrinks to stay in front of it. VFX Reconfigured: by its reach,
                // not the quad's size, which grows with the ripple ring: the lens jumped to twice its size when a ring ended)
                float f=min(1,0.95*dist/max(_Lens.y*1.05,1e-3));
                float q=_Lens.x*f;
                float3 v=view; v.xy+=vertex.xy*q;
                o.pos=mul(UNITY_MATRIX_P,float4(v,1));
                o.local=vertex.xy*q; o.f=f;
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float f=i.f;
                float R=max(_Lens.y*f,1e-3), rE=_Lens.z*f, rc=_Lens.w*f;
                float2 x=i.local; float r=max(length(x),1e-3);
                // The lens: deflection rE^2/r, faded out towards the reach.
                float w=1-smoothstep(0.55*R,R,r);
                float2 beta=x*(1-w*rE*rE/(r*r));
                // The gravitational-wave ring: + polarisation strain (stretch x, squeeze y), oscillating across the ring,
                // and a radial ripple.
                float ringR=_LensRing.x*f, amp=_LensRing.y;
                if(amp>0.001)
                {
                    float d=r-ringR, env=exp(-(d*d)/(0.0324*R*R))*amp;   // (width 0.18 R)
                    float wave=sin(6.2832*d/(0.3*R));
                    float h=0.35*env*wave;
                    beta+=0.5*h*float2(x.x,-x.y)+x/r*(0.06*R*env*cos(6.2832*d/(0.3*R)));
                }
                // Metres on the plane to pixels, from the screen-space derivatives (the plane faces the eye: the map is
                // affine), so the copy is read where this very pixel is when nothing bends (no flip to get wrong).
                float2 lx=ddx(i.local), ly=ddy(i.local), delta=beta-x;
                float det=lx.x*ly.y-ly.x*lx.y;
                float2 px=abs(det)>1e-12?float2(ly.y*delta.x-ly.x*delta.y,-lx.y*delta.x+lx.x*delta.y)/det:0;
                float2 uv=(i.pos.xy+px)/_ScreenParams.xy;
                float3 c=tex2D(_VefxLensSrc,clamp(uv,0.001,0.999)).rgb;
                // A faint blue cast over the lens while it holds.
                c+=float3(0.03,0.1,0.3)*_LensRing.w*exp(-3*r/R);
                // The black core and its blue rim.
                if(rc>1e-3)
                {
                    c*=smoothstep(0.92*rc,1.02*rc,r);
                    float rim=(r-1.05*rc)/(0.12*rc);
                    c+=float3(0.35,0.65,1)*3*_LensRing.z*exp(-rim*rim);
                }
                return float4(c,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
