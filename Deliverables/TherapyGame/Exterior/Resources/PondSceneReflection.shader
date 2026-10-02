Shader "Therapy Game/Pond Scene Reflection"
{
    Properties
    {
        _BaseMap ("Albedo",2D) = "white" {}
        _BaseColor ("Tint",Color) = (1,1,1,1)
        _Cutoff ("Cutout",Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite On ZTest LEqual Cull Off Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST,_BaseColor;
                float _Cutoff;
            CBUFFER_END
            float4x4 _PondCaptureVP;
            float _PondCaptureLevel;
            float4 _PondCaptureSun,_PondCaptureLight,_PondCaptureAmbient;
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;};
            V Vert(A input)
            {
                V o;o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=mul(_PondCaptureVP,float4(o.positionWS,1));
                o.normalWS=TransformObjectToWorldNormal(input.normalOS);
                o.uv=input.uv*_BaseMap_ST.xy+_BaseMap_ST.zw;return o;
            }
            half4 Frag(V i):SV_Target
            {
                clip(i.positionWS.y-_PondCaptureLevel-.005);
                half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                clip(albedo.a-_Cutoff);
                half diffuse=saturate(dot(normalize(i.normalWS),_PondCaptureSun.xyz));
                half3 lighting=max(.09,_PondCaptureAmbient.rgb*.65)+_PondCaptureLight.rgb*(.18+.65*diffuse);
                return half4(albedo.rgb*lighting,1);
            }
            ENDHLSL
        }
    }
}
