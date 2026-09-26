Shader "Therapy Game/Natural Sky and Stars"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (.19,.42,.72,1)
        _HorizonColor ("Horizon", Color) = (.69,.80,.89,1)
        _GroundColor ("Below horizon", Color) = (.36,.43,.44,1)
        _SunDirection ("Direction toward sun", Vector) = (-.7,.7,.22,0)
        _Night ("Night", Range(0,1)) = 0
        _Storm ("Cloud cover", Range(0,1)) = 0
        _StarRotation ("Slow sidereal rotation", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor,_HorizonColor,_GroundColor;
                float4 _SunDirection;
                half _Night,_Storm;
                float _StarRotation;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;};
            Varyings Vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.direction=v.positionOS.xyz;return o;}
            float3 Hash(float2 p)
            {return frac(sin(float3(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3)),dot(p,float2(419.2,371.9))))*43758.5453);}
            half3 Stars(float2 uv,float scale,float threshold)
            {
                float2 p=uv*float2(scale,scale*.5),cell=floor(p);float3 random=Hash(cell);
                float2 center=.14+random.xy*.72;
                float distance=length(frac(p)-center),radius=lerp(.025,.07,pow(random.z,9));
                float aa=max(fwidth(distance),.009);
                float spot=1-smoothstep(max(0,radius-aa),radius+aa,distance);
                float brightness=lerp(.12,.95,pow(random.z,10))*step(threshold,random.z);
                half3 tint=lerp(half3(.72,.82,1),half3(1,.89,.72),random.x);
                return tint*spot*brightness;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.direction),sunDirection=normalize(_SunDirection.xyz);
                float day=1-_Night,up=saturate(d.y),sunDot=saturate(dot(d,sunDirection));
                // Analytic atmospheric gradient/forward haze, not a costly volumetric model.
                half3 sky=lerp(_HorizonColor.rgb,_TopColor.rgb,pow(up,.40));
                float lowSun=1-smoothstep(.03,.4,abs(sunDirection.y));
                half3 scatter=lerp(half3(.97,.85,.67),half3(1,.50,.24),lowSun);
                sky+=scatter*pow(sunDot,18)*(.045+.065*lowSun)*day*(1-_Storm*.85);
                sky=lerp(sky,_GroundColor.rgb,saturate(-d.y*6));
                float above=smoothstep(-.006,.012,d.y);
                // Roughly half-degree apparent discs, not oversized glowing balls.
                float sunDistance=length(d-sunDirection),moonDistance=length(d+sunDirection);
                float sunAA=max(fwidth(sunDistance),.0001),moonAA=max(fwidth(moonDistance),.0001);
                float sunDisc=1-smoothstep(.00465-sunAA,.00465+sunAA,sunDistance);
                float moonDisc=1-smoothstep(.00450-moonAA,.00450+moonAA,moonDistance);
                sky+=half3(1,.91,.73)*sunDisc*(1-_Storm*.88)*above;
                float moonTexture=.82+.12*sin(d.x*730)*sin(d.z*590);
                sky+=half3(.71,.77,.88)*moonDisc*moonTexture*_Night*(1-_Storm*.88)*above;
                // Uniform branch avoids star-field work in daylight.
                if(_Night>.70)
                {
                    float cs=cos(_StarRotation),sn=sin(_StarRotation);
                    float3 rotated=float3(d.x*cs-d.z*sn,d.y,d.x*sn+d.z*cs);
                    float2 starUV=float2(atan2(rotated.z,rotated.x)/6.2831853+.5,asin(clamp(rotated.y,-1,1))/3.14159265+.5);
                    half3 stars=Stars(starUV,420,.978)+Stars(starUV+float2(.173,.219),170,.994)*1.1;
                    float nightFade=smoothstep(.70,.98,_Night)*(1-_Storm*.96)*smoothstep(.04,.35,d.y);
                    sky+=stars*nightFade;
                }
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
