#ifndef WELLNESS_MOON_INCLUDED
#define WELLNESS_MOON_INCLUDED
// An analytic lunar face: contrasting dark maria and softly lit crater rims. Evaluated
// only on the moon's pixels; no texture, extra mesh, light, bloom or render pass.
float WellnessMoonBasin(float2 uv, float2 center, float2 size)
{
    float2 p=(uv-center)/size;
    return exp2(-dot(p,p)*2.4);
}
float WellnessMoonCrater(float2 uv, float2 center, float radius, float pixel)
{
    float2 delta=uv-center;
    float d=length(delta), aa=max(pixel,.012);
    float hollow=1-smoothstep(radius*.60,radius,d);
    float rim=1-smoothstep(aa,aa*2.4,abs(d-radius));
    float rimLight=dot(delta/max(d,.001),float2(-.6,.8));
    return rim*(.04+.055*rimLight)-hollow*.13;
}
half3 WellnessMoonFace(float3 direction, float3 moonDirection, float radius, float pixel)
{
    // The lunar orbit never aligns with the Z axis. This basis stays stable at
    // the zenith and keeps surface features fixed on the moving moon's face.
    float3 right=normalize(cross(float3(0,0,1),moonDirection));
    float3 up=cross(moonDirection,right);
    float2 uv=float2(dot(direction,right),dot(direction,up))/radius;
    float z=sqrt(saturate(1-dot(uv,uv)));
    float maria=WellnessMoonBasin(uv,float2(-.28,.36),float2(.35,.46))*.40
        +WellnessMoonBasin(uv,float2(.20,.42),float2(.28,.32))*.27
        +WellnessMoonBasin(uv,float2(-.47,-.02),float2(.31,.34))*.32
        +WellnessMoonBasin(uv,float2(.12,-.15),float2(.25,.20))*.16;
    float craters=WellnessMoonCrater(uv,float2(.34,-.47),.15,pixel)
        +WellnessMoonCrater(uv,float2(-.14,-.64),.10,pixel)
        +WellnessMoonCrater(uv,float2(.55,.18),.13,pixel)
        +WellnessMoonCrater(uv,float2(-.47,.43),.09,pixel)
        +WellnessMoonCrater(uv,float2(.08,-.40),.075,pixel)
        +WellnessMoonCrater(uv,float2(.40,.60),.065,pixel);
    float grain=sin(uv.x*37+sin(uv.y*19))*sin(uv.y*43-uv.x*11)*.025
        *saturate(1-pixel*18);
    float limb=.74+.26*z;
    // Cap the bright highlands below white so the enlarged face retains detail.
    float albedo=clamp(.94-maria+craters+grain,.34,1.0);
    return half3(.97,.965,.92)*albedo*limb;
}
#endif
