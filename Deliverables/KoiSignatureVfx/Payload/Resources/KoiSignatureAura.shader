Shader "Therapy Game/Koi Signature Aura"
{
    Properties { [HideInInspector] _DstBlend("Destination blend",Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "KoiSignature" Tags { "LightMode"="UniversalForward" }
            Blend One [_DstBlend], One OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 p:POSITION;half4 color:COLOR;float3 uv:TEXCOORD0;};
            struct V {float4 p:SV_POSITION;half4 color:COLOR;float3 uv:TEXCOORD0;};
            V Vert(A a){V v;v.p=TransformObjectToHClip(a.p.xyz);v.color=a.color;v.uv=a.uv;return v;}
            half4 Frag(V v):SV_Target
            {
                float2 p=v.uv.xy*2-1;float coverage=0,highlight=0;
                if(v.uv.z<.5){coverage=pow(saturate(1-abs(p.y)),1.7);highlight=pow(saturate(1-abs(p.y)),9)*.14;}
                else if(v.uv.z<1.5){float cross=max(pow(saturate(1-abs(p.x)),14)*saturate(1-abs(p.y)),pow(saturate(1-abs(p.y)),14)*saturate(1-abs(p.x)));coverage=saturate(cross+exp(-dot(p,p)*18)*.6);highlight=exp(-dot(p,p)*25)*.28;}
                else if(v.uv.z<2.5)coverage=exp(-dot(p,p)*3)*saturate(1-dot(p,p));
                else if(v.uv.z<3.5){coverage=saturate((1-abs(p.x)-abs(p.y))*12);highlight=saturate(-p.x+p.y)*.3;}
                else if(v.uv.z<4.5){float outer=1-smoothstep(.91,1,length(p)),inner=smoothstep(.70,.77,length(p-float2(.34,.08)));coverage=outer*inner;highlight=.1;}
                else if(v.uv.z<5.5)coverage=1-smoothstep(.68,1,length(p));
                else {float petal=1-abs(p.x)*1.7-p.y*p.y;coverage=saturate(petal*8);highlight=saturate(-p.x)*.16;}
                half alpha=saturate(v.color.a*coverage);half3 color=lerp(v.color.rgb,max(v.color.rgb,half3(1.2,1.2,1.2)),highlight);
                return half4(color*alpha,alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
