#ifndef WELLNESS_DISTANT_LANDSCAPE_INCLUDED
#define WELLNESS_DISTANT_LANDSCAPE_INCLUDED

// Direction-only, seamless 360-degree scenery. No mountain meshes, textures,
// capture cameras, extra lights or per-frame object allocation. Also used by water.
float WellnessRidge(float turn, float frequency, float phase)
{
    return abs(frac(turn * frequency + phase) * 2.0 - 1.0);
}

half3 WellnessDistantLandscape(float3 direction, half3 sky, half3 horizon, float night, float storm)
{
    // All silhouettes lie below this elevation; leave clouds and the night sky alone.
    if (direction.y > .34) return sky;
    float turn = atan2(direction.z, direction.x) / 6.28318530718 + .5;
    float angle = turn * 6.28318530718;
    // Integer frequencies give identical values on both sides of the panorama seam.
    float farRidge = .16 + .04 * sin(angle * 3 + .8) + .03 * sin(angle * 7 + 2.1)
        + .055 * WellnessRidge(turn, 13, .27) + .014 * WellnessRidge(turn, 31, .11);
    float middleRidge = .105 + .022 * sin(angle * 4 + 1.7)
        + .028 * WellnessRidge(turn, 11, .37) + .011 * WellnessRidge(turn, 23, .4);
    float nearRidge = .035 + .014 * sin(angle * 3 + 1.4) + .011 * sin(angle * 8 + .6);
    // Tiny irregular treetops, painted into the nearest ridge rather than modelled.
    float treeCell = floor(turn * 384);
    float treeSeed = frac(sin(fmod(treeCell, 384) * 127.1 + 19.3) * 43758.5453);
    nearRidge += pow(1 - abs(frac(turn * 384) * 2 - 1), 1.6) * (.004 + treeSeed * .008);

    float darkness = smoothstep(.28, .97, night);
    half3 farColor = lerp(half3(.40, .53, .54), half3(.027, .041, .061), darkness);
    half3 middleColor = lerp(half3(.28, .43, .40), half3(.018, .031, .042), darkness);
    half3 nearColor = lerp(half3(.22, .36, .27), half3(.013, .025, .031), darkness);
    farColor = lerp(farColor, horizon, .64 + storm * .21);
    middleColor = lerp(middleColor, horizon, .49 + storm * .29);
    nearColor = lerp(nearColor, horizon, .33 + storm * .38);
    // Quiet faceting and a few distant snow tips fit the existing low-poly garden.
    farColor *= .965 + .035 * WellnessRidge(turn, 13, .27);
    float snow = smoothstep(.245, .280, farRidge)
        * smoothstep(farRidge - .015, farRidge - .003, direction.y) * (1 - darkness);
    farColor = lerp(farColor, lerp(half3(.82, .85, .80), horizon, .78), snow * .60);

    float aa = max(fwidth(direction.y), .00035);
    sky = lerp(sky, farColor, 1 - smoothstep(farRidge - aa, farRidge + aa, direction.y));
    sky = lerp(sky, middleColor, 1 - smoothstep(middleRidge - aa, middleRidge + aa, direction.y));
    sky = lerp(sky, nearColor, 1 - smoothstep(nearRidge - aa, nearRidge + aa, direction.y));
    // The ground apron fades to the same camera fog color near its far-clip limit.
    // Matching that below the horizon prevents a dark band/visible terrain cutoff.
    return lerp(sky, horizon, 1 - smoothstep(-.12, .018, direction.y));
}
#endif
