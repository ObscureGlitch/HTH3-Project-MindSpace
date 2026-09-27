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
            #include "WellnessSkySampling.hlsl"
            half4 Frag(Varyings i):SV_Target
            {
                return half4(WellnessSky(normalize(i.direction),_TopColor.rgb,_HorizonColor.rgb,_GroundColor.rgb,
                    _SunDirection.xyz,_Night,_Storm,_StarRotation),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
