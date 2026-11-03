#include "UnityCG.cginc"
#include "PlayerEffectRamp.cginc"
sampler2D _Noise;
float4 _C0, _C1, _C2, _C3, _Dir;
float _T, _AlphaNow, _SmearNow, _Seed, _GhostGroundY;

#define GHOST_GLOW 0.85

struct Vary
{
    float4 pos : SV_POSITION;
    float3 n : TEXCOORD0;
    float3 v : TEXCOORD1;
    float3 p : TEXCOORD2;
    float smear : TEXCOORD3;
};

Vary vert(appdata_base i)
{
    Vary o;
    float3 wp = mul(unity_ObjectToWorld, float4(i.vertex.xyz, 1)).xyz;
    float3 wn = UnityObjectToWorldNormal(i.normal);
    float back = max(0.0, dot(wn, -_Dir.xyz));
    float n = tex2Dlod(_Noise, float4((wp.y - _GhostGroundY) * 0.45 + _Seed, 0.37, 0, 0)).r;
    float len = _SmearNow * back * back * (0.4 + 0.6 * n);
    wp -= _Dir.xyz * len;
    o.smear = _SmearNow > 0.0 ? len / _SmearNow : 0.0;
    o.p = float3(-wp.x, wp.y - _GhostGroundY, wp.z);
    o.n = wn;
    o.v = normalize(_WorldSpaceCameraPos - wp);
    o.pos = UnityWorldToClipPos(wp);
    return o;
}

float3 Ramp(float i)
{
    return LookRamp(i, _C0.rgb, _C1.rgb, _C2.rgb, _C3.rgb);
}

float4 Shade(Vary i)
{
    float3 dirW = float3(-_Dir.x, _Dir.y, _Dir.z);
    float fr = saturate(1.0 - abs(dot(normalize(i.n), normalize(i.v))));
    float n = tex2D(_Noise, i.p.xy * 0.35 + float2(_Seed, i.p.z * 0.3)).a * 0.7
        + tex2D(_Noise, i.p.zy * 0.8 + _Seed).r * 0.3;
    float th = _T * 1.25 - 0.15 - (i.p.y - 0.9) * 0.15 + i.smear * 0.3;
    float vis = smoothstep(th, th + 0.04, n);
    float edge = vis * (1.0 - smoothstep(th + 0.04, th + 0.1, n)) * step(0.05, _T);
    float flow = tex2D(_Noise, float2(dot(i.p, dirW) * 0.9 - _T * 1.5 + _Seed, i.p.y * 2.2)).b;

    float flash = lerp(1.3, 0.85, saturate(_T * 1.6));
    float rim = smoothstep(0.35, 0.85, fr);
    float I = ((0.36 + rim * 0.6 + (flow - 0.5) * 0.26) * flash + i.smear * 0.35 + edge * 0.6)
        * lerp(0.85, 1.0, GHOST_GLOW);
    float3 col = Ramp(clamp(I, 0.3, 0.88)) * (1.0 + rim * 1.1 * flash * GHOST_GLOW);
    float a = (0.32 + rim * 0.68) * vis * _AlphaNow * (1.0 - i.smear * 0.35);
    return float4(col, a);
}
