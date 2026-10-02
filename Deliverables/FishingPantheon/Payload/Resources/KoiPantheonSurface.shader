Shader "Therapy Game/Pantheon Koi"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;float2 surface:TEXCOORD0;float3 emission:TEXCOORD1;};
            struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half3 color:TEXCOORD2;half2 surface:TEXCOORD3;half3 emission:TEXCOORD4;};
            V Vert(A a){V o;o.positionWS=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.color=a.color.rgb;o.surface=a.surface;o.emission=a.emission;return o;}
            half4 Frag(V i):SV_Target
            {
                half3 n=normalize(i.normalWS),v=normalize(GetWorldSpaceViewDir(i.positionWS));Light light=GetMainLight();
                half diffuse=saturate(dot(n,light.direction)),metal=saturate(i.surface.x),rough=max(.12,i.surface.y);
                half3 ambient=max(SampleSH(n),half3(.12,.14,.16));
                half3 color=i.color*(ambient+light.color*(.18+diffuse*.82));
                half spec=pow(saturate(dot(n,normalize(v+light.direction))),lerp(72,12,rough));
                color+=light.color*spec*lerp(.18,.7,metal)+i.emission*.65;
                color+=pow(1-saturate(dot(n,v)),3)*i.color*.12;return half4(color,1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
