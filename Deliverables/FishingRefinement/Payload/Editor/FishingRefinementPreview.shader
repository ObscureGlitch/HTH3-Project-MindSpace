Shader "Hidden/Therapy/Fishing Refinement Preview"
{
    Properties { _MainTex("Texture",2D)="white"{} _Color("Color",Color)=(1,1,1,1) _Size("Size",Vector)=(100,100,0,0) _Radius("Radius",Float)=0 _Stroke("Stroke",Float)=0 _Mode("Mode",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" } Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);float4 _Color,_Size;float _Radius,_Stroke,_Mode;
            struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vert(A a){V v;v.p=TransformObjectToHClip(a.p.xyz);v.uv=a.uv;return v;}
            half4 Frag(V v):SV_Target
            {
                if(_Mode>1.5){half a=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,v.uv).a;return half4(_Color.rgb,_Color.a*a);}
                if(_Mode>.5)return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,v.uv)*_Color;
                float2 halfSize=_Size.xy*.5,q=abs((v.uv-.5)*_Size.xy)-(halfSize-_Radius);
                float d=length(max(q,0))+min(max(q.x,q.y),0)-_Radius;
                float edge=max(fwidth(d),.7),alpha=1-smoothstep(-edge,edge,d);
                if(_Stroke>0)alpha*=smoothstep(-_Stroke-edge,-_Stroke+edge,d);
                return half4(_Color.rgb,_Color.a*alpha);
            }
            ENDHLSL
        }
    }
}
