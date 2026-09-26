Shader "Therapy Game/Photographic Cloud"
{
    Properties
    {
        _BaseMap ("Transparent cloud atlas", 2D) = "white" {}
        _Tint ("Weather illumination", Color) = (1,.98,.95,1)
        _HorizonColor ("Atmospheric haze", Color) = (.69,.80,.89,1)
        _Opacity ("Density", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _HorizonColor;
                half _Opacity;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 localUV:TEXCOORD1;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 localUV:TEXCOORD1;half horizon:TEXCOORD2;};
            Varyings Vert(Attributes v)
            {
                Varyings o;float3 world=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(world);
                // Sky-only depth: mountains, the room and trees always occlude clouds,
                // including geometry beyond the small projection dome's physical radius.
                #if UNITY_REVERSED_Z
                    o.positionCS.z=0;
                #else
                    o.positionCS.z=o.positionCS.w;
                #endif
                o.uv=v.uv;o.localUV=v.localUV;
                o.horizon=1-smoothstep(.05,.5,normalize(world-_WorldSpaceCameraPos).y);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half4 photo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                // Neutralize any tiny chromatic fringe in the authored cutout.
                half detail=dot(photo.rgb,half3(.2126,.7152,.0722));
                half3 color=lerp(detail*_Tint.rgb,_HorizonColor.rgb,i.horizon*.48);
                float2 border=min(i.localUV,1-i.localUV);
                half padding=smoothstep(0,.045,min(border.x,border.y));
                return half4(color,photo.a*_Opacity*padding);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
