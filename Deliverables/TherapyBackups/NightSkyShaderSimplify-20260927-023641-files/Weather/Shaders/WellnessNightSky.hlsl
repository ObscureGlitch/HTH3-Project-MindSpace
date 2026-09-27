#ifndef WELLNESS_NIGHT_SKY_INCLUDED
#define WELLNESS_NIGHT_SKY_INCLUDED
// Analytic angular ribbons, not volumes: two curtains, three short trails at most.
// These exact functions are shared by the sky and the rippled pond reflection.
half3 WellnessAurora(float3 direction, float seconds, float azimuth)
{
    float angle=atan2(direction.x,direction.z)-azimuth;
    float u=atan2(sin(angle),cos(angle));
    float vista=smoothstep(.02,.48,cos(u))*smoothstep(.04,.16,direction.y)
        *(1-smoothstep(.75,.91,direction.y));
    half3 light=0;
    [unroll] for(int ribbon=0;ribbon<2;ribbon++)
    {
        float t=seconds+ribbon*49.0;
        float bend=u+.042*sin(u*7+t*.021)+.022*sin(u*13-t*.014);
        float edge=.17+ribbon*.13+.047*sin(u*3+t*.013)+.018*sin(u*9-t*.021);
        float height=.29+.045*sin(u*4-t*.009);
        float h=(direction.y-edge)/height;
        float curtain=smoothstep(-.025,.085,h)*exp2(-max(0,h)*3.6)*(1-smoothstep(.70,1.15,h));
        // Filter vertical rays with angular derivatives, including in water. This
        // avoids noisy narrow stripes when viewed small or through moving ripples.
        float broadPhase=bend*31+t*.037, finePhase=bend*93-t*.053;
        float broad=.5+.5*sin(broadPhase)*saturate(1-fwidth(broadPhase)*.4);
        float fine=.5+.5*sin(finePhase)*saturate(1-fwidth(finePhase)*.4);
        float rays=.22+.53*broad*broad+.25*fine*fine;
        float fold=.64+.36*sin(bend*5+t*.011);
        half3 tint=lerp(half3(.10,.80,.40),half3(.12,.50,.67),saturate(h*1.6));
        tint=lerp(tint,half3(.49,.20,.63),smoothstep(.34,.92,h)*.64);
        float hem=exp2(-abs(h-.035)*30)*.08;
        light+=(tint*curtain*rays*fold*.44+half3(.15,.62,.37)*hem)*lerp(1,.60,ribbon);
    }
    return light*vista;
}
half3 WellnessMeteors(float3 direction, float4 heads[3], float4 tangents[3], float4 sides[3])
{
    half3 light=0;
    [unroll] for(int i=0;i<3;i++)
    {
        // Uniform active-slot check skips the work on almost every frame.
        [branch] if(heads[i].w>.0001)
        {
            float facing=dot(direction,heads[i].xyz);
            float along=dot(direction,tangents[i].xyz)/max(.1,facing);
            float across=dot(direction,sides[i].xyz);
            float tail=max(.001,tangents[i].w), width=max(.0001,sides[i].w);
            float aa=max(fwidth(across),.00012), alongAA=max(fwidth(along),.00012);
            float position=saturate(1+along/tail);
            float taper=width*lerp(.15,1,position);
            float core=1-smoothstep(max(0,taper-aa),taper+aa,abs(across));
            float halo=exp2(-abs(across)/max(aa,width*3.5))*.14;
            float tailMask=smoothstep(-tail,-tail*.78,along)*(1-smoothstep(0,alongAA*2,along));
            float headDistance=length(float2(along,across));
            float headAA=max(fwidth(headDistance),.00012);
            float head=1-smoothstep(max(0,width*1.7-headAA),width*1.7+headAA,headDistance);
            half3 tint=lerp(half3(.72,.86,1),half3(1,.88,.70),i*.33);
            light+=tint*((core+halo)*tailMask*position*position+head*.55)*heads[i].w*step(.9,facing);
        }
    }
    return light*smoothstep(.02,.15,direction.y);
}
half3 WellnessNightDisplay(float3 direction, float4 effects, float4 heads[3], float4 tangents[3], float4 sides[3])
{
    half3 light=0;
    [branch] if(effects.x>.0001)
    {
        [branch] if(effects.y>.0001)light+=WellnessAurora(direction,effects.z,effects.w)*effects.y;
        light+=WellnessMeteors(direction,heads,tangents,sides);
    }
    return light*effects.x;
}
#endif
