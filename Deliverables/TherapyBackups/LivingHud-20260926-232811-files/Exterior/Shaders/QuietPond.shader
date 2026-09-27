Shader "Therapy Game/Quiet Pond"
{
    Properties
    {
        _BaseColor ("Deep teal", Color) = (0.12, 0.39, 0.42, 1)
        _ShoreColor ("Shallow water", Color) = (0.38, 0.64, 0.57, 1)
        _SkyTint ("Soft sky sheen", Color) = (0.60, 0.76, 0.77, 1)
        _Pond ("Centre XZ / radii XZ", Vector) = (15, -5, 6.7, 5.7)
        _Opacity ("Water tint opacity", Range(0, 1)) = 0.64
        _RippleStrength ("Ripple strength", Range(0, 0.2)) = 0.055
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "QuietPondForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShoreColor;
                half4 _SkyTint;
                float4 _Pond;
                half _Opacity;
                half _RippleStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Two travelling ripple directions; no normal textures, refraction
                // buffer, reflection camera, vertex deformation, or extra passes.
                float2 p = input.positionWS.xz;
                float warp = sin(dot(p, float2(.31, .19)) + _Time.y * .17) * .7;
                float a = dot(p, float2(2.7, 1.8)) + _Time.y * 1.05 + warp;
                float b = dot(p, float2(1.9, 2.3)) - _Time.y * 0.78 + warp * .63;
                float c = dot(p, float2(4.1, 2.9)) + _Time.y * 1.36;
                float2 slope = float2(0.84, 0.56) * cos(a)
                    + float2(0.64, 0.77) * cos(b) * 0.40
                    + float2(0.82, 0.58) * cos(c) * 0.16;
                half3 n = normalize(half3(-slope.x * _RippleStrength, 1, -slope.y * _RippleStrength));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1 - saturate(dot(n, view)), 4);
                float shoreDistance = length((p - _Pond.xy) / _Pond.zw);
                half shore = smoothstep(0.55, 1.0, shoreDistance);
                half3 tint = lerp(_BaseColor.rgb, _ShoreColor.rgb, shore * 0.75);
                // No multiplied crossing sine bands: those made a visible checker
                // whose fixed brightness overwhelmed the nighttime water tint.
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1,1,1,1));
                half lightStrength = saturate(max(sun.color.r, max(sun.color.g, sun.color.b)));
                half lighting = 0.84 + 0.16 * saturate(dot(n, sun.direction)) * sun.shadowAttenuation * lightStrength;
                half3 water = tint * lighting;
                water = lerp(water, _SkyTint.rgb, 0.10 + fresnel * 0.50);
                half3 halfDirection = SafeNormalize(sun.direction + view);
                half glint = pow(saturate(dot(n, halfDirection)), 64) * 0.14 * sun.shadowAttenuation;
                water += min(sun.color, half3(1.5, 1.5, 1.5)) * glint;
                half alpha = lerp(_Opacity, 0.87, fresnel) * (1 - smoothstep(0.97, 1.0, shoreDistance) * 0.48);
                // Evaluate distance per pixel, not across the large fan triangles.
                float viewDepth = max(-TransformWorldToView(input.positionWS).z - _ProjectionParams.y, 0);
                return half4(MixFog(water, ComputeFogFactorZ0ToFar(viewDepth)), alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
