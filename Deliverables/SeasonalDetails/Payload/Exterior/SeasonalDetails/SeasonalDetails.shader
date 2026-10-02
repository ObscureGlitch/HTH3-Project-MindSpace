Shader "Therapy Game/Seasonal Details"
{
    Properties { _SeasonVisibility ("Seasonal density", Range(0,1)) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Name "SeasonalDetailsForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
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
            half _SeasonVisibility;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(input.normalOS);o.color=input.color;return o;
            }
            half4 Frag(Varyings input,FRONT_FACE_TYPE facing:FRONT_FACE_SEMANTIC):SV_Target
            {
                // Each flower/leaf stores one stable threshold in vertex alpha:
                // season changes thin the patch gradually without transparency sorting.
                clip(_SeasonVisibility-input.color.a);
                half3 n=normalize(input.normalWS)*IS_FRONT_VFACE(facing,1,-1);
                Light main=GetMainLight(TransformWorldToShadowCoord(input.positionWS),input.positionWS,half4(1,1,1,1));
                half3 light=max(SampleSH(n),half3(.035,.035,.035))+main.color*saturate(dot(n,main.direction))*main.shadowAttenuation;
                float depth=max(-TransformWorldToView(input.positionWS).z-_ProjectionParams.y,0);
                return half4(MixFog(input.color.rgb*light,ComputeFogFactorZ0ToFar(depth)),1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
