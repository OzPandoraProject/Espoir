Shader "EspoirBossFx/GamoriBurstField" {
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
      float uFlash;
      float uLead;
      float uHole;
      float uOut;
      float uA0;
      float uK;
      float uBody;
      float uRim;
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

  float2 q = float2(vP.x, -vP.y);
  float r = length(q);
  if (r > uR) discard;
  float a = atan2(q.x, q.y);
  float d = glsl_mod(a - uA0 + 12.5663706, 6.2831853);
  float rn = r / uR;
  float dz = tex2D(uNoise, q * 0.22 + float2(uSeed * 1.7, 0.3)).a * 0.65 + tex2D(uNoise, q * 0.07 + uSeed).r * 0.35;
  float e = r - uHole + (dz - 0.5) * 4.0 * uK;
  if (uHole > 0.0 && e < 0.0) discard;
  float burnEdge = uHole > 0.0 ? 1.0 - smoothstep(0.0, 0.8 * uK, e) : 0.0;
  float lead = (1.0 - smoothstep(0.0, 1.4 * uK, uR - r)) * uLead;
  float rim = smoothstep(uR - 2.2 * uK, uR - 0.15 * uK, r);
  float cut = 1.0 - smoothstep(uR - 0.1 * uK, uR, r);
  float lanes = 30.0 * uK;
  float v = rn * lanes + 0.45 * sin(d * 1.7 + rn * 2.3 + uSeed);
  float li = floor(v), lv = frac(v);
  float h1 = frac(sin(li * 91.7 + uSeed * 13.1) * 43758.5453);
  float sp = (0.6 + 0.9 * rn) * (0.75 + 0.5 * h1);
  float arc = d * max(r, 0.5);
  float L = 2.2 + 2.6 * h1;
  float uu = (arc - uT * sp * 3.2) / L + h1 * 7.0;
  float ci = floor(uu), cu = frac(uu);
  float h2 = frac(sin(ci * 17.3 + li * 57.1 + uSeed) * 43758.5453);
  float h3 = frac(sin(ci * 41.9 + li * 11.3 + uSeed * 2.1) * 43758.5453);
  float on = step(0.08, h2);
  float len = 0.55 + 0.37 * h3;
  float cs = cu / len;
  float shape = pow(smoothstep(0.0, 0.9, cs), 1.6) * (1.0 - smoothstep(0.93, 1.0, cs)) * step(cs, 1.0);
  float wdt = 0.05 + 0.17 * h2 * sqrt(shape);
  float band = 1.0 - smoothstep(wdt, wdt + 0.22, abs(lv - 0.5));
  float flowS = on * shape * band * (0.55 + 0.45 * h3) * smoothstep(0.8 * uK, 2.0 * uK, r);
  float sheen = tex2D(uNoise, float2(d * 0.3 - uT * 0.1 + uSeed, rn * 0.45)).r - 0.5;
  float I = 0.1 + uRim * rim * rim + 0.5 * lead + 0.35 * uFlash + 0.5 * flowS * (0.5 + 0.5 * rn) + 0.1 * sheen + 0.5
      * burnEdge;
  float3 col = ramp(clamp(I, 0.0, 1.0));
  float alpha = min(uBody + 0.25 * rim + 0.2 * lead + 0.2 * burnEdge, 1.0) * cut * (1.0 - uOut);
  if (alpha < 0.01) discard;
  _fc = float4(col, alpha);

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}