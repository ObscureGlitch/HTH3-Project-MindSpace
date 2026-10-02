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
        _ReflectionStrength ("Sky reflection",Range(0,1)) = .82
        [HideInInspector] _WaveField ("Height and surface gradient",2D) = "black" {}
        [HideInInspector] _PondReflection ("Scene reflection",2D) = "black" {}
        [HideInInspector] _TopColor ("Sky zenith",Color) = (.19,.42,.72,1)
        [HideInInspector] _HorizonColor ("Sky horizon",Color) = (.69,.80,.89,1)
        [HideInInspector] _GroundColor ("Sky ground",Color) = (.36,.43,.44,1)
        [HideInInspector] _SunDirection ("Sky sun",Vector) = (-.7,.7,.22,0)
        [HideInInspector] _Night ("Night",Float) = 0
        [HideInInspector] _Storm ("Storm",Float) = 0
        [HideInInspector] _StarRotation ("Star rotation",Float) = 0
        [HideInInspector] _NightEffects ("Shared night display",Vector) = (0,0,0,0)
        [HideInInspector] _AuroraShape ("Shared aurora composition",Vector) = (.7,1.1,.5,0)
        [HideInInspector] _CloudAtlas ("Shared cloud atlas",2D) = "black" {}
        [HideInInspector] _PondCloudSingle ("Single full-texture cloud bank",Float) = 0
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
            TEXTURE2D(_CloudAtlas);SAMPLER(sampler_CloudAtlas);
            TEXTURE2D(_WaveField);SAMPLER(sampler_WaveField);
            TEXTURE2D(_PondReflection);SAMPLER(sampler_PondReflection);
            float4 _WaveBounds;
            float _HasWaveField,_WaterTime,_HasPondReflection,_WaterLevel;
            float4x4 _PondReflectionVP;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShoreColor;
                half4 _SkyTint;
                float4 _Pond;
                half _Opacity;
                half _RippleStrength;
                half _ReflectionStrength;
                half4 _TopColor,_HorizonColor,_GroundColor,_CloudTint;
                float4 _SunDirection;
                float _Night,_Storm,_StarRotation;
                float4 _NightEffects,_AuroraShape;
                float4 _MeteorHeads[3],_MeteorTangents[3],_MeteorSides[3];
                int _PondCloudCount;
                float _PondCloudSingle;
                float4 _CloudFacing[16],_CloudRight[16],_CloudUp[16];
            CBUFFER_END
            #include "../../Weather/Shaders/WellnessSkySampling.hlsl"
            #include "../../Weather/Shaders/WellnessCloudOcclusion.hlsl"
            half3 ReflectedSky(float3 direction)
            {
                half3 sky=WellnessSky(direction,_TopColor.rgb,_HorizonColor.rgb,_GroundColor.rgb,
                    _SunDirection.xyz,_Night,_Storm,_StarRotation);
                // At most sixteen cheap angular rectangle tests. Only overlapping cards
                // sample the existing mipmapped atlas; no scene/depth/opaque texture copy.
                [loop] for(int k=0;k<_PondCloudCount;k++)
                {
                    float facing=dot(direction,_CloudFacing[k].xyz);
                    float2 uv=float2(dot(direction,_CloudRight[k].xyz),dot(direction,_CloudUp[k].xyz))/max(.001,facing)+.5;
                    [branch] if(facing>.01&&_CloudFacing[k].w>.001&&all(uv>0)&&all(uv<1))
                    {
                        float2 atlas=_PondCloudSingle>.5?uv:(uv+float2(_CloudRight[k].w,_CloudUp[k].w))*float2(.25,.5);
                        half4 photo=SAMPLE_TEXTURE2D_LOD(_CloudAtlas,sampler_CloudAtlas,atlas,1);
                        half detail=dot(photo.rgb,half3(.2126,.7152,.0722));
                        half haze=1-smoothstep(.05,.5,_CloudFacing[k].y);
                        half3 cloud=lerp(detail*_CloudTint.rgb,_HorizonColor.rgb,haze*.48);
                        float2 border=min(uv,1-uv);
                        half visible=_PondCloudSingle>.5?WellnessCloudSkyVisibility(direction):1;
                        sky=lerp(sky,cloud,photo.a*_CloudFacing[k].w*smoothstep(0,.045,min(border.x,border.y))*visible);
                    }
                }
                return sky;
            }
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 waveUV=(output.positionWS.xz-_WaveBounds.xy)*_WaveBounds.zw;
                float4 wave=SAMPLE_TEXTURE2D_LOD(_WaveField,sampler_WaveField,waveUV,0)*_HasWaveField;
                float wind=sin(dot(output.positionWS.xz,float2(.8,.45))+_WaterTime*.85)*.007
                    +sin(dot(output.positionWS.xz,float2(-.5,1.1))-_WaterTime*.65)*.004;
                output.positionWS.y+=wave.r+wind*wave.a;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Fine capillary detail rides on the simulated, displaced surface.
                float2 p = input.positionWS.xz;
                float warp = sin(dot(p, float2(.31, .19)) + _Time.y * .17) * .7;
                float a = dot(p, float2(2.7, 1.8)) + _Time.y * 1.05 + warp;
                float b = dot(p, float2(1.9, 2.3)) - _Time.y * 0.78 + warp * .63;
                float c = dot(p, float2(4.1, 2.9)) + _Time.y * 1.36;
                float2 slope = float2(0.84, 0.56) * cos(a)
                    + float2(0.64, 0.77) * cos(b) * 0.40
                    + float2(0.82, 0.58) * cos(c) * 0.16;
                float2 waveUV=(p-_WaveBounds.xy)*_WaveBounds.zw;
                float4 wave=SAMPLE_TEXTURE2D(_WaveField,sampler_WaveField,waveUV)*_HasWaveField;
                float2 windSlope=cos(dot(p,float2(.8,.45))+_WaterTime*.85)*.007*float2(.8,.45)
                    +cos(dot(p,float2(-.5,1.1))-_WaterTime*.65)*.004*float2(-.5,1.1);
                slope=slope*_RippleStrength*.4+wave.gb+windSlope*wave.a;
                half3 n = normalize(half3(-slope.x, 1, -slope.y));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // Schlick reflectance for the air/water interface (IOR 1.333).
                half fresnel = .0204+.9796*pow(1 - saturate(dot(n, view)), 5);
                float shoreDistance = length((p - _Pond.xy) / _Pond.zw);
                half shore = smoothstep(0.55, 1.0, shoreDistance);
                half3 tint = lerp(_BaseColor.rgb, _ShoreColor.rgb, shore * 0.75);
                // No multiplied crossing sine bands: those made a visible checker
                // whose fixed brightness overwhelmed the nighttime water tint.
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1,1,1,1));
                half lightStrength = saturate(max(sun.color.r, max(sun.color.g, sun.color.b)));
                half lighting = 0.84 + 0.16 * saturate(dot(n, sun.direction)) * sun.shadowAttenuation * lightStrength;
                half3 water = tint * lighting;
                float3 reflectedDirection=normalize(reflect(-view,n));
                half3 skyReflection;
                if(_HasPondReflection>.5)
                {
                    // Project onto the mirror plane; displaced vertex height must not
                    // slide the captured scene. Distortion follows world space, not UV axes.
                    float3 samplePosition=float3(p.x+n.x*.12,_WaterLevel,p.y+n.z*.12);
                    float4 projection=mul(_PondReflectionVP,float4(samplePosition,1));
                    float2 uv=projection.xy/max(.001,projection.w)*.5+.5;
                    float2 edge=min(uv,1-uv);
                    half valid=smoothstep(0,.04,min(edge.x,edge.y))*step(.001,projection.w);
                    half4 localReflection=SAMPLE_TEXTURE2D(_PondReflection,sampler_PondReflection,saturate(uv));
                    skyReflection=localReflection.rgb;
                    // Local geometry is opaque; the unrendered background has alpha zero
                    // and receives the current sky, rather than a second scene-camera sky.
                    half localWeight=valid*localReflection.a;
                    [branch] if(localWeight<.999)skyReflection=lerp(ReflectedSky(reflectedDirection),skyReflection,localWeight);
                }
                else skyReflection=ReflectedSky(reflectedDirection);
                half reflection=min(.44,(.18+fresnel*.65)*_ReflectionStrength);
                water = lerp(water,skyReflection,reflection);
                half3 halfDirection = SafeNormalize(sun.direction + view);
                half glint = pow(saturate(dot(n, halfDirection)), 220) * .85 * sun.shadowAttenuation;
                water += min(sun.color, half3(1.5, 1.5, 1.5)) * glint;
                half alpha = lerp(max(_Opacity,.76), .98, fresnel) * (1 - smoothstep(0.97, 1.0, shoreDistance) * 0.48);
                // Evaluate distance per pixel, not across the large fan triangles.
                float viewDepth = max(-TransformWorldToView(input.positionWS).z - _ProjectionParams.y, 0);
                return half4(MixFog(water, ComputeFogFactorZ0ToFar(viewDepth)), alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
