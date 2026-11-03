#ifndef PLAYER_EFFECT_RAMP
#define PLAYER_EFFECT_RAMP
float3 LookRamp(float i, float3 c0, float3 c1, float3 c2, float3 c3)
{
    float3 c = lerp(c0, c1, smoothstep(0.13, 0.23, i));
    c = lerp(c, c2, smoothstep(0.35, 0.45, i));
    return lerp(c, c3, smoothstep(0.75, 0.85, i));
}
#endif
