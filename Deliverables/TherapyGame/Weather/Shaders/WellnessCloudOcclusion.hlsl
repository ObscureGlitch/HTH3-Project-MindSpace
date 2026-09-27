#ifndef WELLNESS_CLOUD_OCCLUSION_INCLUDED
#define WELLNESS_CLOUD_OCCLUSION_INCLUDED
// Match the existing direction-only landscape silhouette without changing its
// shading or geometry. The large low bank must sit BEHIND the distant hills.
float WellnessCloudRidge(float turn,float frequency,float phase)
{return abs(frac(turn*frequency+phase)*2.0-1.0);}
float WellnessCloudSkyVisibility(float3 direction)
{
    if(direction.y>.34)return 1;
    float turn=atan2(direction.z,direction.x)/6.28318530718+.5;
    float angle=turn*6.28318530718;
    float farRidge=.16+.04*sin(angle*3+.8)+.03*sin(angle*7+2.1)
        +.055*WellnessCloudRidge(turn,13,.27)+.014*WellnessCloudRidge(turn,31,.11);
    float middleRidge=.105+.022*sin(angle*4+1.7)
        +.028*WellnessCloudRidge(turn,11,.37)+.011*WellnessCloudRidge(turn,23,.4);
    float nearRidge=.035+.014*sin(angle*3+1.4)+.011*sin(angle*8+.6);
    float treeCell=floor(turn*384);
    float treeSeed=frac(sin(fmod(treeCell,384)*127.1+19.3)*43758.5453);
    nearRidge+=pow(1-abs(frac(turn*384)*2-1),1.6)*(.004+treeSeed*.008);
    float aa=max(fwidth(direction.y),.00035);
    return smoothstep(farRidge-aa,farRidge+aa,direction.y)
        *smoothstep(middleRidge-aa,middleRidge+aa,direction.y)
        *smoothstep(nearRidge-aa,nearRidge+aa,direction.y)
        *smoothstep(-.12,.018,direction.y);
}
#endif
