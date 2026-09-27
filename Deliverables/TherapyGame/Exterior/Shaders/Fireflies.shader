Shader "Therapy Game/Tiny Fireflies"
{
    Properties { _Visibility ("Night visibility",Range(0,1))=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+8" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha One ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Visibility;
            CBUFFER_END
            struct A {float3 positionOS:POSITION;float2 uv:TEXCOORD0;float2 seed:TEXCOORD1;float2 drift:TEXCOORD2;half4 color:COLOR;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;float fog:TEXCOORD1;};
            V Vert(A a)
            {
                V o;float t=_Time.y,phase=frac(t/a.drift.x+a.seed.y);
                // Soft isolated flashes, separated by several seconds of darkness.
                float blink=smoothstep(.02,.13,phase)*(1-smoothstep(.20,.40,phase));
                float q=a.drift.y;
                float3 center=TransformObjectToWorld(a.positionOS)+float3(sin(t*.37+q)*.24,sin(t*.51+q*2)*.10,cos(t*.29+q)*.23);
                float3 world=center+(UNITY_MATRIX_V[0].xyz*(a.uv.x-.5)+UNITY_MATRIX_V[1].xyz*(a.uv.y-.5))*a.seed.x;
                o.positionCS=TransformWorldToHClip(world);o.uv=a.uv*2-1;
                o.color=half4(a.color.rgb,_Visibility*blink);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(V i):SV_Target
            {
                float r=dot(i.uv,i.uv);float core=exp(-r*19),halo=exp(-r*4)*.18;
                float edge=1-smoothstep(.55,1,r);
                return half4(MixFogColor(i.color.rgb,half3(0,0,0),i.fog),saturate((core+halo)*edge*i.color.a));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
