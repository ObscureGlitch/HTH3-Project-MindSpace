Shader "Therapy Game/Quiet Sky"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (.22,.53,.75,1)
        _HorizonColor ("Horizon", Color) = (.73,.84,.88,1)
        _GroundColor ("Below horizon", Color) = (.36,.43,.44,1)
        _SunDirection ("Direction toward sun", Vector) = (-.7,.7,.22,0)
        _Night ("Night", Range(0,1)) = 0
        _Storm ("Cloud cover", Range(0,1)) = 0
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
                half4 _TopColor, _HorizonColor, _GroundColor;
                float4 _SunDirection;
                half _Night, _Storm;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.direction=v.positionOS.xyz;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.direction);float3 s=normalize(_SunDirection.xyz);
                half3 sky=lerp(_HorizonColor.rgb,_TopColor.rgb,pow(saturate(d.y),.55));
                sky=lerp(sky,_GroundColor.rgb,saturate(-d.y*4));
                float sunDot=dot(d,s),moonDot=dot(d,-s);
                float sun=smoothstep(.99955,.99982,sunDot);
                float moon=smoothstep(.99960,.99985,moonDot);
                float above=smoothstep(-.02,.03,d.y);
                sky+=half3(1,.79,.48)*(sun*.75+pow(saturate(sunDot),180)*.07)*(1-_Storm*.85)*above;
                sky+=half3(.68,.79,.95)*moon*.8*_Night*(1-_Storm*.85)*above;
                // Sparse fixed stars: one cell hash, no textures, layers, raymarching or twinkle.
                float2 p=d.xz/(max(0,d.y)+1)*160;
                float2 cell=floor(p);float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                float radius=length(frac(p)-.5),aa=max(fwidth(radius),.015);
                float star=(1-smoothstep(.055,.055+aa,radius))*step(.996,seed);
                sky+=star*_Night*(1-_Storm*.92)*smoothstep(.08,.25,d.y)*.55;
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
