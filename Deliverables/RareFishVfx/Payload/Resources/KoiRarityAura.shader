Shader "Therapy Game/Koi Rarity Aura"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "KoiRarityGlow"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float3 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float3 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            { Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.color=input.color;o.uv=input.uv;return o; }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv.xy*2-1;
                float coverage,core;
                if(input.uv.z<.5)
                {
                    coverage=pow(saturate(1-abs(p.y)),1.7);
                    core=pow(saturate(1-abs(p.y)),8);
                }
                else if(input.uv.z<1.5)
                {
                    float cross=max(pow(saturate(1-abs(p.x)),13)*saturate(1-abs(p.y)),pow(saturate(1-abs(p.y)),13)*saturate(1-abs(p.x)));
                    core=exp(-dot(p,p)*24);coverage=saturate(cross+core*.55);
                }
                else if(input.uv.z<2.5)
                {core=0;coverage=exp(-dot(p,p)*4)*saturate(1-dot(p,p));}
                else
                {coverage=saturate((1-abs(p.x)-abs(p.y))*5);core=pow(saturate(1-abs(p.x)-abs(p.y)),3);}
                return half4(lerp(input.color.rgb,half3(1.8,1.9,2),core*.65),coverage*input.color.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
