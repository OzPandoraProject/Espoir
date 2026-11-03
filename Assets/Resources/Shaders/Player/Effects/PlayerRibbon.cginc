#ifndef PLAYER_RIBBON_COMMON
#define PLAYER_RIBBON_COMMON
#include "UnityCG.cginc"

sampler2D _Noise;
#ifdef _TIMED
sampler2D _Times;
#endif

float FxHeadAt(float u)
{
#ifdef _TIMED
    return tex2Dlod(_Times, float4((255.0 * u + 0.5) / 256.0, 0.5, 0.0, 0.0)).r;
#else
    return u;
#endif
}

float _Lifetime, _Duration, _Linger, _HoldTail, _TailStart, _Expand, _FloorOffset, _Alpha;

struct FxAppdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
#if defined(_FACE)
    float4 tangent : TANGENT;
#endif
    float4 uvAge : TEXCOORD0;
    float3 center : TEXCOORD1;
};

float FxSaturate(float v) { return saturate(v); }
float FxEaseOut(float t) { float k = 1.0 - t; return 1.0 - k * k * k; }
float FxAge(FxAppdata v) { return v.uvAge.z * _Lifetime; }
float FxSeed(FxAppdata v) { return v.uvAge.w * 10.0; }
float FxTime(FxAppdata v, float age) { return (v.uvAge.w * 37.0) + age; }

float FxTail(float age)
{
    float D = _Duration;
    return pow(FxSaturate((age - _HoldTail - D * _TailStart) / (D * (1.0 - _TailStart) + _Linger * 0.6)), 1.3);
}

float3 FxWorldPos(FxAppdata v, float expand)
{
#if defined(_FACE)
    float3 axis = normalize(v.tangent.xyz);
    float3 baked = normalize(v.normal);
    float3 q = v.vertex.xyz - v.center;
    float along = dot(q, axis);
    float width = dot(q, baked);
    float3 side = normalize(cross(_WorldSpaceCameraPos - v.center, axis));
    side *= dot(side, baked) < 0.0 ? -1.0 : 1.0;
    return v.center + axis * along + side * (width + (width < 0.0 ? -expand : expand) * (0.35 + v.uvAge.y));
#else
    float3 p = v.vertex.xyz + normalize(v.normal) * expand * (0.35 + v.uvAge.y);
    p.y = max(p.y, v.center.y + _FloorOffset);
    return p;
#endif
}
#endif
