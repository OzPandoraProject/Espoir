Shader "EspoirBossFx/JinField" {
  Properties {
    [HideInInspector] _SrcBlend ("", Float) = 5
    [HideInInspector] _DstBlend ("", Float) = 1
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
      float uR;
      float uR0;
      float uRev;
      float uT;
      float uFade;
      float uSeed;
      float uFlash;
      float uAlpha;
      float uCharge;
      float uSpin;
      float uOut;
      float3 uC0;
      float3 uC1;
      float3 uC2;
      float3 uC3;
      struct appdata {
        float4 vertex : POSITION;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float2 vP : TEXCOORD0;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float2 vP;
      static float4 _fc;

float3 ramp(float i) {
  float3 c = uC0;
  c = lerp(c, uC1, smoothstep(0.13, 0.23, i));
  c = lerp(c, uC2, smoothstep(0.35, 0.45, i));
  c = lerp(c, uC3, smoothstep(0.75, 0.85, i));
  return c;
}
float hash2(float a, float b) { return frac(sin(a * 91.7 + b * 57.1 + uSeed * 13.1) * 43758.5453); }

float2 streams(float a, float r, float N, float M, float k, float t, float amount) {
  float s = (a + r * k) * N / 6.2831853;
  float li = floor(s), lx = frac(s), lm = glsl_mod(li, N);
  float h1 = hash2(lm, 1.0 + N);
  float v = a * M / 6.2831853 - t * (0.35 + 0.5 * h1) + h1 * 7.0;
  float ci = floor(v), cu = frac(v), cm = glsl_mod(ci, M);
  float h2 = hash2(cm, lm + 3.0), h3 = hash2(cm + 11.0, lm);
  float on = step(h2, amount);
  float len = 0.45 + 0.45 * h3;
  float cs = cu / len, inS = step(cs, 1.0);
  float c2 = lerp(cs, 1.0 - cs, uOut);
  float shape = pow(smoothstep(0.0, 0.9, c2), 1.6) * (1.0 - smoothstep(0.92, 1.0, c2)) * inS;
  float w = (0.07 + 0.16 * h3) * sqrt(shape);
  float band = 1.0 - smoothstep(w, w + 0.14, abs(lx - 0.5));
  float body = on * shape * band;
  return float2(body * (0.6 + 0.4 * h3), body * smoothstep(0.62, 0.92, c2));
}

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;

  vP = position.xy;
  o.pos = UnityObjectToClipPos(float4(position, 1.0));

        o.vP = vP;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vP = i.vP;
        _fc = 0;

  float2 q = float2(vP.x, -vP.y);
  float r = length(q);
  float shownR = lerp(uR0, uR, uRev);
  if (r > shownR || r < uR0) discard;
  float a = atan2(q.x, q.y) - uSpin;
  float rn = (r - uR0) / (uR - uR0);
  float e = shownR - r, ei = r - uR0;
  float cut = smoothstep(0.0, 0.1, min(e, ei));
  float K = 0.22;
  float arm = 0.5 + 0.5 * sin((a + r * K) * 5.0);
  float arm2 = 0.5 + 0.5 * sin((a + r * K) * 13.0 + 1.7);
  float amount = 0.3 + 0.62 * uCharge;
  float2 s1 = streams(a, r, 26.0, 6.0, K, uT, amount);
  float2 s2 = streams(a + 2.1, r, 40.0, 9.0, K, uT * 1.35, amount * 0.8);
  float S = s1.x + 0.6 * s2.x, Hd = s1.y + 0.4 * s2.y;
  float wave = 0.75 + 0.25 * sin(r * 0.5 + uT * 1.6);
  float rimHot = 1.0 - smoothstep(0.1, 0.55, e);
  float rimGlow = exp(-e / 1.6) * (0.55 + 0.45 * arm2);
  float inner = exp(-ei / 2.4) * (0.16 + 0.34 * uCharge);
  float I = (0.07 + 0.1 * arm) * lerp(0.55, 1.0, smoothstep(0.0, 0.5, rn))
    + S * (0.34 + 0.2 * uCharge) * wave + 0.18 * Hd
    + 0.9 * rimHot + 0.3 * rimGlow + inner
    + uFlash * (0.35 + 0.9 * S);

  float dz = rn * 0.72 + 0.28 * arm, fz = uFade * 1.12;
  if (dz < fz) discard;
  float burn = (1.0 - smoothstep(0.0, 0.08, dz - fz)) * step(0.001, uFade);
  I += 0.75 * burn;
  float3 col = ramp(clamp(I, 0.0, 1.0));
  float alpha = (0.72 + 0.2 * S + 0.25 * rimHot + 0.2 * burn) * cut * uAlpha;
  alpha = max(alpha, rimHot * cut * min(1.0, uAlpha * 4.0));
  if (alpha < 0.01) discard;
  _fc = float4(col, min(alpha, 1.0));

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}