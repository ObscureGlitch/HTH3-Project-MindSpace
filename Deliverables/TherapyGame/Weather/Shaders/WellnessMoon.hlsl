#ifndef WELLNESS_MOON_INCLUDED
#define WELLNESS_MOON_INCLUDED
// A small analytic lunar face: broad dark maria and soft crater rims. Evaluated
// only on the moon's pixels; no texture, extra mesh, light, bloom or render pass.
float WellnessMoonBasin(float2 uv, float2 center, float2 size)
{
    float2 p=(uv-center)/size;
    return exp2(-dot(p,p)*2.4);
}
float WellnessMoonCrater(float2 uv, float2 center, float radius, float pixel)
{
    float2 delta=uv-center;
    float d=length(delta), aa=max(pixel,.018);
    float hollow=1-smoothstep(radius*.60,radius,d);
    float rim=1-smoothstep(aa,aa*2.4,abs(d-radius));
    float rimLight=dot(delta/max(d,.001),float2(-.6,.8));
    return rim*(.026+.042*rimLight)-hollow*.11;
}
half3 WellnessMoonFace(float3 direction, float3 moonDirection, float radius, float pixel)
{
    // The lunar orbit never aligns with the Z axis. This basis stays stable at
    // the zenith and keeps surface features fixed on the moving moon's face.
    float3 right=normalize(cross(float3(0,0,1),moonDirection));
    float3 up=cross(moonDirection,right);
    float2 uv=float2(dot(direction,right),dot(direction,up))/radius;
    float z=sqrt(saturate(1-dot(uv,uv)));
    float maria=WellnessMoonBasin(uv,float2(-.28,.36),float2(.35,.46))*.29
        +WellnessMoonBasin(uv,float2(.20,.42),float2(.28,.32))*.21
        +WellnessMoonBasin(uv,float2(-.47,-.02),float2(.31,.34))*.25
        +WellnessMoonBasin(uv,float2(.12,-.15),float2(.25,.20))*.12;
    float craters=WellnessMoonCrater(uv,float2(.34,-.47),.15,pixel)
        +WellnessMoonCrater(uv,float2(-.14,-.64),.10,pixel)
        +WellnessMoonCrater(uv,float2(.55,.18),.13,pixel)
        +WellnessMoonCrater(uv,float2(-.47,.43),.09,pixel);
    float grain=sin(uv.x*37+sin(uv.y*19))*sin(uv.y*43-uv.x*11)*.025
        *saturate(1-pixel*18);
    float limb=.68+.32*z;
    float albedo=clamp(.96-maria+craters+grain,.38,1.08);
    return half3(.97,.965,.92)*albedo*limb;
}
#endif
