Shader "EspoirBossFx/GamoriScorch" {
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
      sampler2D uNoise;
      float uR;
      float uT;
      float uSeed;
      float uHot;
      float uOut;
      float uHalf;
      float uFill;
      float uRag;
      float uCrack;
      float uFine;
      float uLineOn;
      float uEmber;
      float uClose;
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
float2 hash2(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3))); return frac(sin(p) * 43758.5453);
}
float vedge(float2 x) {
  float2 n = floor(x), f = frac(x), mg = ((float2)(0.0)), mr = ((float2)(0.0));
  float md = 8.0;
  for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++) {
    float2 g = float2(float(i), float(j)), o = hash2(n + g), r = g + o - f;
    float d = dot(r, r);
    if (d < md) { md = d; mr = r; mg = g; }
  }
  md = 8.0;
  for (int j = -2; j <= 2; j++) for (int i = -2; i <= 2; i++) {
    float2 g = mg + float2(float(i), float(j)), o = hash2(n + g), r = g + o - f;
    if (dot(mr - r, mr - r) > 0.00001) md = min(md, dot(0.5 * (mr + r), normalize(r - mr)));
  }
  return md;
}

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;
 vP = position.xy; o.pos = UnityObjectToClipPos(float4(position, 1.0));
        o.vP = vP;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vP = i.vP;
        _fc = 0;

  float2 q = vP;
  float r = length(q), a = atan2(q.x, q.y), da = abs(a);
  if (da > uHalf + 0.25) discard;
  float nz = tex2D(uNoise, q * 0.3 + uSeed).r, nz2 = tex2D(uNoise, q * 0.9 - uSeed).g;
  float side = r * sin(clamp(uHalf - da, -1.5708, 1.5708));
  float eb0 = min(uR - r, side);
  float eb = eb0 + (nz - 0.56) * uRag * 2.0 * smoothstep(0.3, 1.6, r);
  if (eb < 0.0) discard;
  float2 cq = float2(a * 5.0, pow(max(r, 0.01), 0.8) * 1.15) + float2(uSeed * 3.1, uSeed);
  float e1 = vedge(cq), e2 = vedge(cq * 2.6 + 7.3);
  float w = (0.03 + 0.036 * nz2) * (1.0 - uClose);
  float c1 = (1.0 - smoothstep(w, w + 0.03, e1)) * step(0.002, w);
  float c2 = (1.0 - smoothstep(w * 0.45, w * 0.45 + 0.03, e2)) * uFine * smoothstep(0.3, 0.62, nz) * step(0.002, w);
  float inb = smoothstep(0.0, 0.35, eb);
  float crack = max(c1, c2) * uCrack * inb;
  float glow = (1.0 - smoothstep(w, w + 0.2, e1)) * uCrack * uHot * 0.3 * inb;
  float core = 1.0 - smoothstep(w * 0.3, w * 0.62, e1);
  float lineV = (1.0 - smoothstep(0.02, 0.05, eb0)) * uLineOn;
  float ember = (1.0 - smoothstep(0.0, 0.32, eb)) * uEmber * (0.55 + 0.45 * sin(uT * 9.0 + nz * 20.0));
  float I = 0.05 + 0.1 * nz;
  I = max(I, glow);
  I = lerp(I, lerp(0.4 + 0.3 * uHot, 0.04, core * c1), crack);
  I = max(I, ember * (0.4 + 0.3 * uHot));
  I = max(I, lineV * 0.9);
  I += uHot * 0.3 * exp(-r / 0.9);
  float3 col = ramp(clamp(I, 0.0, 1.0));
  float alpha = max(uFill * smoothstep(0.0, 0.3, eb), max(max(crack, glow * 0.8), max(ember, lineV))) * (1.0 - uOut);
  if (alpha < 0.01) discard;
  _fc = float4(col, min(alpha, 1.0));

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}