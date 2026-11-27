Shader "EspoirBossFx/GamoriPetalsFan" {
  Properties {
    [HideInInspector] _SrcBlend ("", Float) = 5
    [HideInInspector] _DstBlend ("", Float) = 10
    [HideInInspector] _ZWrite ("", Float) = 0
    [HideInInspector] _ZTest ("", Float) = 4
    [HideInInspector] _Cull ("", Float) = 0
  }
  SubShader {
    Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
    Pass {
      Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] ZTest [_ZTest] Cull [_Cull]
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_local __ ESPOIR_GLOW
      #pragma target 3.5
      #include "UnityCG.cginc"
      #include "EspoirBossFxCommon.cginc"
      float uT;
      float uMode;
      float uR;
      float uR0;
      float uH;
      float uOn;
      float uOff;
      float uLife;
      float uEnv;
      float uSpeed;
      float3 uCenter;
      float uDirA;
      float uHalf;
      float3 uHot;
      float3 uLight;
      float3 uMid;
      float3 uShade;
      struct appdata {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
        float4 c0 : TEXCOORD1;
        float4 c1 : TEXCOORD2;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float2 vUv : TEXCOORD0;
        float3 vN : TEXCOORD1;
        float3 vW : TEXCOORD2;
        float vTint : TEXCOORD3;
        float vHot : TEXCOORD4;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float4 aSeed;
      static float3 aAxis;
      static float2 vUv;
      static float3 vN;
      static float3 vW;
      static float vTint;
      static float vHot;
      static float4 _fc;

float3 rot(float3 p, float3 ax, float a)
{
    return p * cos(a) + cross(ax, p) * sin(a) + ax * dot(ax, p) * (1.0 - cos(a));
}

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;
        normal = IN.normal;
        uv = IN.uv;
        aSeed = IN.c0.xyzw;
        aAxis = IN.c1.xyz;

  float ph = aSeed.x, s = aSeed.w, hot = 0.0;
  float3 c;
  if (uMode > 0.5 && uMode < 1.5) {
    float r = lerp(uR0, uR, aSeed.y), a = ph * 6.2832 + uT * uSpeed / (0.35 + aSeed.y);
    c = uCenter + float3(sin(a) * r,
        0.14 + aSeed.z * uH * (0.35 + 0.65 * (1.0 - aSeed.y)) + 0.08 * sin(uT * 2.3 + ph * 20.0), cos(a) * r);
    s *= uEnv;
  } else if (uMode > 4.5 && uMode < 5.5) {
    float tau = uT - uOn, k = clamp(tau / uLife, 0.0, 1.0);
    float rs = uR0 * sqrt(aSeed.y), r = rs + (uR - uR0) * (0.35 + 0.65 * aSeed.y) * (1.0 - exp(-3.2 * k));
    float a = uDirA + (ph * 2.0 - 1.0) * uHalf + 0.3 * uSpeed * k * (aSeed.z - 0.5);
    c = uCenter + float3(sin(a) * r, 0.12 + uH * (0.25 + 0.75 * aSeed.z) * sin(3.14159 * pow(k, 0.55)), cos(a) * r);
    s *= step(0.0, tau) * smoothstep(0.0, 0.05, k) * (1.0 - smoothstep(0.7, 1.0, k));
    hot = exp(-max(tau, 0.0) / 0.16);
  } else if (uMode > 5.5) {
    float r = uR * (0.9 + 0.12 * aSeed.y), a = ph * 6.2832 + uT * uSpeed * (0.75 + 0.5 * aSeed.z),
        k = frac(aSeed.z + uT * 0.16 + ph);
    c = uCenter + float3(sin(a) * r, 0.1 + k * uH, cos(a) * r);
    s *= uEnv * smoothstep(0.0, 0.12, k) * (1.0 - smoothstep(0.7, 1.0, k));
  } else {
    float cyc = uT / uLife + ph, tau = frac(cyc) * uLife, born = uT - tau, k = tau / uLife;
    if (uMode < 0.5) {
      float a = uDirA + (frac(ph * 7.13) * 2.0 - 1.0) * uHalf * 0.92 + 0.08 * sin(tau * 1.3 + ph * 9.0),
          r = lerp(uR0, uR, sqrt(aSeed.y));
      c = uCenter + float3(sin(a) * r, 0.06 + pow(k, 0.8) * uH * (0.45 + 0.55 * aSeed.z), cos(a) * r);
    } else if (uMode < 2.5) {
      float a = ph * 43.0 + tau * 0.35, r = uR * sqrt(aSeed.y);
      c = uCenter + float3(sin(a) * r + 0.2 * sin(tau * 1.7 + ph * 31.0),
          0.08 + (1.0 - k) * uH * (0.55 + 0.45 * aSeed.z), cos(a) * r + 0.2 * cos(tau * 1.3 + ph * 17.0));
    } else {
      float a = ph * 18.85 + k * 6.5 + uT * 0.6, r = uR * (0.25 + 0.75 * aSeed.y) * (1.0 - 0.4 * k);
      c = uCenter + float3(sin(a) * r, 0.1 + k * uH, cos(a) * r);
    }
    s *= smoothstep(0.0, 0.12, k) * (1.0 - smoothstep(0.75, 1.0, k)) * step(uOn, born) * step(born, uOff);
  }
  float3 ax = normalize(aAxis); float ang = ph * 6.283 + uT * (1.4 + 3.0 * frac(ph * 7.31));
  float3 p = rot(position * s, ax, ang), n = rot(normal, ax, ang);
  vUv = uv; vTint = frac(ph * 13.7); vHot = hot;
  vN = normalize(mul((float3x3)unity_ObjectToWorld, n));
  float4 w = mul(unity_ObjectToWorld, float4(c + p, 1.0)); vW = w.xyz;
  o.pos = mul(UNITY_MATRIX_VP, w);

        o.vUv = vUv;
        o.vN = vN;
        o.vW = vW;
        o.vTint = vTint;
        o.vHot = vHot;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vUv = i.vUv;
        vN = i.vN;
        vW = i.vW;
        vTint = i.vTint;
        vHot = i.vHot;
        _fc = 0;

  float3 n = normalize(vN), V = normalize(_WorldSpaceCameraPos - vW);
  if (dot(n, V) < 0.0) n = -n;
  float l = dot(n, normalize(EspoirStageDir(float3(0.4, 0.8, 0.45))));
  float3 c = lerp(uShade, uMid, smoothstep(-0.12, -0.02, l));
  c = lerp(c, uLight, smoothstep(0.32, 0.42, l));
  c = lerp(c, uLight * 1.25 + float3(0.06, 0.02, 0.03), smoothstep(0.78, 0.84, vUv.y) * 0.7);
  c *= 0.72 + 0.5 * vTint;
  c = invACES(clamp(c, 0.0, 0.95));
  c += uHot * vHot * 0.7;
  _fc = float4(c, 1.0);

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}