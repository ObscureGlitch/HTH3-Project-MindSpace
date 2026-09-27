#ifndef WELLNESS_SKY_SAMPLING_INCLUDED
#define WELLNESS_SKY_SAMPLING_INCLUDED
// Same directional sky evaluation for the skybox and pond; no capture camera.
            float3 WellnessSkyHash(float2 p)
            {return frac(sin(float3(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3)),dot(p,float2(419.2,371.9))))*43758.5453);}
            half3 WellnessStars(float2 uv,float scale,float threshold)
            {
                float2 p=uv*float2(scale,scale*.5),cell=floor(p);float3 random=WellnessSkyHash(cell);
                float2 center=.14+random.xy*.72;
                float distance=length(frac(p)-center),radius=lerp(.025,.07,pow(random.z,9));
                float aa=max(fwidth(distance),.009);
                float spot=1-smoothstep(max(0,radius-aa),radius+aa,distance);
                float brightness=lerp(.12,.95,pow(random.z,10))*step(threshold,random.z);
                half3 tint=lerp(half3(.72,.82,1),half3(1,.89,.72),random.x);
                return tint*spot*brightness;
            }
            half3 WellnessSky(float3 d,half3 topColor,half3 horizonColor,half3 groundColor,float3 sunVector,float night,float storm,float starRotation)
            {
                float3 sunDirection=normalize(sunVector);
                float day=1-night,up=saturate(d.y),sunDot=saturate(dot(d,sunDirection));
                // Analytic atmospheric gradient/forward haze, not a costly volumetric model.
                half3 sky=lerp(horizonColor,topColor,pow(up,.40));
                float lowSun=1-smoothstep(.03,.4,abs(sunDirection.y));
                half3 scatter=lerp(half3(.97,.85,.67),half3(1,.50,.24),lowSun);
                sky+=scatter*pow(sunDot,18)*(.045+.065*lowSun)*day*(1-storm*.85);
                sky=lerp(sky,groundColor,saturate(-d.y*6));
                float above=smoothstep(-.006,.012,d.y);
                // Roughly half-degree apparent discs, not oversized glowing balls.
                float sunDistance=length(d-sunDirection),moonDistance=length(d+sunDirection);
                float sunAA=max(fwidth(sunDistance),.0001),moonAA=max(fwidth(moonDistance),.0001);
                float sunDisc=1-smoothstep(.00465-sunAA,.00465+sunAA,sunDistance);
                float moonDisc=1-smoothstep(.00450-moonAA,.00450+moonAA,moonDistance);
                sky+=half3(1,.91,.73)*sunDisc*(1-storm*.88)*above;
                float moonTexture=.82+.12*sin(d.x*730)*sin(d.z*590);
                sky+=half3(.71,.77,.88)*moonDisc*moonTexture*night*(1-storm*.88)*above;
                // Uniform branch avoids star-field work in daylight.
                if(night>.70)
                {
                    float cs=cos(starRotation),sn=sin(starRotation);
                    float3 rotated=float3(d.x*cs-d.z*sn,d.y,d.x*sn+d.z*cs);
                    float2 starUV=float2(atan2(rotated.z,rotated.x)/6.2831853+.5,asin(clamp(rotated.y,-1,1))/3.14159265+.5);
                    half3 stars=WellnessStars(starUV,420,.978)+WellnessStars(starUV+float2(.173,.219),170,.994)*1.1;
                    float nightFade=smoothstep(.70,.98,night)*(1-storm*.96)*smoothstep(.04,.35,d.y);
                    sky+=stars*nightFade;
                }
                return sky;
            }
#endif
