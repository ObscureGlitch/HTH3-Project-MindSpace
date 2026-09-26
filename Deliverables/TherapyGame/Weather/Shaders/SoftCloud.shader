Shader "Therapy Game/Soft Low Poly Cloud"
{
    Properties
    {
        _TopColor ("Cloud light", Color) = (.96,.96,.91,1)
        _BottomColor ("Cloud shade", Color) = (.68,.72,.76,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor, _BottomColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; half shade:TEXCOORD0; half fog:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.shade=saturate(TransformObjectToWorldNormal(v.normalOS).y*.4+.55);
                o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {return half4(MixFog(lerp(_BottomColor.rgb,_TopColor.rgb,i.shade),i.fog),1);}
            ENDHLSL
        }
    }
    Fallback Off
}
