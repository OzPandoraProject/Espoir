Shader "EspoirBossFx/GamoriWarn" {
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
      float3 uO;
      float2 uDir;
      float2 uHalf;
      float2 uCen;
      sampler2D uNoise;
      float uShape;
      float uA1;
      float uA2;
      float uRimW;
      float uP;
      float uT;
      float uA;
      float uMode;
      float uBeat;
      float uFlash;
      float3 uC1;
      float3 uC2;
      float3 uC3;
      struct appdata {
        float4 vertex : POSITION;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float2 vQ : TEXCOORD0;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float2 vQ;
      static float4 _fc;

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;

  vQ = position.xy * uHalf + uCen;
  float3 w = uO + float3(uDir.y, 0.0, -uDir.x) * vQ.x + float3(uDir.x, 0.0, uDir.y) * vQ.y;
  o.pos = UnityObjectToClipPos(float4(w, 1.0));

        o.vQ = vQ;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vQ = i.vQ;
        _fc = 0;

  float2 q = vQ;
  float r = length(q), d, s;
  if (uShape < 0.5) { d = uA1 - r; s = r / uA1; }
  else if (uShape < 1.5)
  {
      float an = abs(atan2(q.x, q.y)); d = min(uA1 - r, sin(clamp(uA2 - an, -1.57, 1.57)) * r); s = r / uA1;
  }
  else { d = min(min(q.y, uA1 - q.y), uA2 - abs(q.x)); s = q.y / uA1; }
  float n2 = tex2D(uNoise, q * 0.14 + float2(uT * 0.02, -uT * 0.015)).r;
  float n4 = tex2D(uNoise, q * 0.43 - float2(uT * 0.03, uT * 0.02)).r;

  float inside = smoothstep(0.0, 0.03, d);
  if (inside < 0.003) discard;
  float rim = exp(-max(d, 0.0) / uRimW) * inside;
  float base = 0.16 + 0.14 * n2 + 0.10 * smoothstep(0.4, 1.0, s);
  float fill = 0.0, lead = 0.0;
  if (uMode < 1.5) {
    float f = uP * 1.04;
    fill = 1.0 - smoothstep(f - 0.03, f + 0.01, s);
    lead = exp(-abs(s - f) / 0.035) * step(0.02, uP) * (1.0 - smoothstep(0.92, 1.0, uP));
  } else if (uMode < 2.5) {
    float f = (1.0 - uP) * 1.02;
    fill = smoothstep(f - 0.01, f + 0.03, s);
    lead = exp(-abs(s - f) / 0.035) * (1.0 - smoothstep(0.9, 1.0, uP));
  } else {
    fill = uBeat * (0.55 + 0.45 * n2);
  }
  float grain = 0.85 + 0.3 * n2 * n4;
  float I = base * 0.3 * grain + fill * (0.2 + 0.34 * n2) * grain + rim * 0.42 + lead * 0.5 + uFlash * 0.3;
  float3 col = lerp(uC1, uC2, clamp(I * 1.5, 0.0, 1.0));
  col = lerp(col, uC3, clamp((I - 0.62) * 1.6, 0.0, 1.0));
  float al = clamp(0.34 + 0.24 * fill + 0.26 * rim + 0.22 * lead + 0.12 * uFlash, 0.0, 0.8) * inside * uA;
  if (al < 0.01) discard;
  _fc = float4(col, al);

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}