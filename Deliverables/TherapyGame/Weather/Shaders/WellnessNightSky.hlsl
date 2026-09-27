#ifndef WELLNESS_NIGHT_SKY_INCLUDED
#define WELLNESS_NIGHT_SKY_INCLUDED
// Analytic angular ribbons, not volumes: two curtains, three short trails at most.
// These exact functions are shared by the sky and the rippled pond reflection.
half3 WellnessAurora(float3 direction, float seconds, float azimuth)
{
    // Rotate the whole sky-local curtain, not just its horizontal heading.
    // The tilted frame lets the ribbons cross the zenith without an azimuth
    // singularity directly above the player. Nothing follows camera rotation.
    float tilt=_AuroraShape.x, spread=max(.8,_AuroraShape.y), palette=_AuroraShape.z;
    float phase=_AuroraShape.w;
    float drift=.025*sin(seconds*.003+phase);
    float3 forward=float3(sin(azimuth+drift),0,cos(azimuth+drift));
    float3 right=float3(forward.z,0,-forward.x);
    float3 tiltedForward=forward*cos(tilt)+float3(0,1,0)*sin(tilt);
    float3 tiltedUp=float3(0,1,0)*cos(tilt)-forward*sin(tilt);
    float3 local=float3(dot(direction,right),dot(direction,tiltedUp),dot(direction,tiltedForward));
    float angle=atan2(local.x,local.z), u=angle/spread;
    float vista=smoothstep(-.12,.36,cos(angle))*smoothstep(.035,.15,direction.y)
        *(1-smoothstep(.87,.98,local.y));
    half3 light=0;
    [unroll] for(int ribbon=0;ribbon<2;ribbon++)
    {
        float t=seconds+phase+ribbon*49.0;
        float bend=u+.042*sin(u*7+t*.021)+.022*sin(u*13-t*.014);
        float edge=.13+ribbon*.18+.070*sin(u*3+t*.013)+.022*sin(u*9-t*.021);
        float height=.38+.055*sin(u*4-t*.009);
        float h=(local.y-edge)/height;
        float curtain=smoothstep(-.025,.085,h)*exp2(-max(0,h)*2.5)*(1-smoothstep(.78,1.18,h));
        // Filter vertical rays with angular derivatives, including in water. This
        // avoids noisy narrow stripes when viewed small or through moving ripples.
        float broadPhase=bend*31+t*.037, finePhase=bend*93-t*.053;
        float broad=.5+.5*sin(broadPhase)*saturate(1-fwidth(broadPhase)*.4);
        float fine=.5+.5*sin(finePhase)*saturate(1-fwidth(finePhase)*.4);
        float rays=.22+.53*broad*broad+.25*fine*fine;
        float fold=.64+.36*sin(bend*5+t*.011);
        half3 green=lerp(half3(.16,.88,.43),half3(.16,.76,.68),palette*.60);
        half3 pink=lerp(half3(.92,.22,.52),half3(.61,.32,.92),palette);
        half3 tint=lerp(green,half3(.20,.64,.73),saturate(h*1.35)*.55);
        tint=lerp(tint,pink,smoothstep(.28,.82,h)*.92);
        float hem=exp2(-abs(h-.035)*30)*.09;
        float upperGlow=lerp(1,1.8,smoothstep(.25,.90,h));
        light+=(tint*curtain*rays*fold*.67*upperGlow+green*hem)*lerp(1,.65,ribbon);
    }
    return light*vista;
}
// Read the shared per-material constants directly. Avoid nested array arguments:
// they unnecessarily enlarge the legacy Unity shader compiler's inlining work.
half3 WellnessMeteors(float3 direction)
{
    half3 light=0;
    [unroll] for(int i=0;i<3;i++)
    {
        // Uniform active-slot check skips the work on almost every frame.
        [branch] if(_MeteorHeads[i].w>.0001)
        {
            float facing=dot(direction,_MeteorHeads[i].xyz);
            float along=dot(direction,_MeteorTangents[i].xyz)/max(.1,facing);
            float across=dot(direction,_MeteorSides[i].xyz);
            float tail=max(.001,_MeteorTangents[i].w), width=max(.0001,_MeteorSides[i].w);
            float aa=max(fwidth(across),.00012), alongAA=max(fwidth(along),.00012);
            float position=saturate(1+along/tail);
            float taper=width*lerp(.15,1,position);
            float core=1-smoothstep(max(0,taper-aa),taper+aa,abs(across));
            float halo=exp2(-abs(across)/max(aa,width*3.5))*.18;
            float tailMask=smoothstep(-tail,-tail*.78,along)*(1-smoothstep(0,alongAA*2,along));
            float headDistance=length(float2(along,across));
            float headAA=max(fwidth(headDistance),.00012);
            float head=1-smoothstep(max(0,width*1.7-headAA),width*1.7+headAA,headDistance);
            half3 tint=lerp(half3(.72,.86,1),half3(1,.88,.70),i*.33);
            light+=tint*((core+halo)*tailMask*position*position+head*.55)*_MeteorHeads[i].w*step(.9,facing);
        }
    }
    return light*smoothstep(.02,.15,direction.y);
}
half3 WellnessNightDisplay(float3 direction)
{
    half3 light=0;
    [branch] if(_NightEffects.x>.0001)
    {
        [branch] if(_NightEffects.y>.0001)light+=WellnessAurora(direction,_NightEffects.z,_NightEffects.w)*_NightEffects.y;
        light+=WellnessMeteors(direction);
    }
    return light*_NightEffects.x;
}
#endif
