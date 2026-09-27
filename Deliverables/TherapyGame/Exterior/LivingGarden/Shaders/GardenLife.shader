Shader "Therapy Game/Garden Life"
{
    Properties { _Tint ("Gentle palette variation", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "GardenLifeForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half3 color : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS); o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color.rgb * _Tint.rgb; return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                Light main = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1,1,1,1));
                half3 light = max(SampleSH(n), half3(.025,.025,.025)) + main.color * saturate(dot(n, main.direction)) * main.shadowAttenuation;
                half3 color = input.color * light;
                float depth = max(-TransformWorldToView(input.positionWS).z - _ProjectionParams.y, 0);
                return half4(MixFog(color, ComputeFogFactorZ0ToFar(depth)), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
