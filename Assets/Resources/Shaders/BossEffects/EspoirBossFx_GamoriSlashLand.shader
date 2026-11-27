Shader "EspoirBossFx/GamoriSlashLand" {
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
      float3 uCenter;
      float uExpand;
      float uRigid;
      float uGrow;
      float uFloor;
      float uLand;
      sampler2D uNoise;
      float uTime;
      float uHead;
      float uTail;
      float uDissolve;
      float uAlpha;
      float uSeed;
      float uThick;
      float uRough;
      float uFreq;
      float uClean;
      float uHot;
      float3 uC0;
      float3 uC1;
      float3 uC2;
      float3 uC3;
      struct appdata {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float2 vUv : TEXCOORD0;
        float3 vWorld : TEXCOORD1;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float2 vUv;
      static float3 vWorld;
      static float4 _fc;

float3 ramp(float i) {
  const float soft = 0.05;
  float3 c = uC0;
  c = lerp(c, uC1, smoothstep(0.18 - soft, 0.18 + soft, i));
  c = lerp(c, uC2, smoothstep(0.40 - soft, 0.40 + soft, i));
  c = lerp(c, uC3, smoothstep(0.80 - soft, 0.80 + soft, i));
  return c;
}

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;
        uv = IN.uv;

  vUv = uv;
  float3 p = uCenter + (position - uCenter) * (1.0 - uGrow);
  float3 d = p - uCenter;
  float3 dn = normalize(d);
  if (dn.y < 0.0) dn.y *= smoothstep(0.0, 1.0, p.y - uLand);
  p += dn * uExpand * lerp(0.35 + uv.y, 1.0 + 0.25 * uv.y, uRigid);
  vWorld = mul(unity_ObjectToWorld, float4(p, 1.0)).xyz;
  vWorld.y = max(vWorld.y, EspoirStagePoint(float3(0.0, uFloor, 0.0)).y);
  o.pos = mul(UNITY_MATRIX_VP, float4(vWorld, 1.0));

        o.vUv = vUv;
        o.vWorld = vWorld;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vUv = i.vUv;
        vWorld = i.vWorld;
        _fc = 0;

  float s = vUv.x, y = vUv.y;
  float lag = uHead - s;
  if (lag < 0.0) discard;
  float span = max(uHead - uTail, 0.001);
  float life = clamp(lag / span, 0.0, 1.0);
  float sn = s * uFreq;
  float n1 = tex2D(uNoise, float2(sn * 1.6 + uSeed, y * 0.35 - uTime * 0.12)).r;
  float n2 = tex2D(uNoise, float2(sn * 4.0 - uTime * 0.35 + uSeed, y * 1.2)).g;
  float streak = tex2D(uNoise, float2(sn * 0.8 + uSeed * 0.3, y * 1.0)).b;

  float width = lerp(uThick, uThick * 0.22, pow(life, 0.7));
  float inner = 1.0 - width + ((n1 - 0.5) * 0.55 * (0.25 + life) - streak * 0.18 * life) * uRough;
  float body = smoothstep(inner, inner + 0.035, y);
  body *= 1.0 - smoothstep(1.0 - 0.035, 1.0, y);
  float tailCut = smoothstep(uTail, uTail + 0.06, s + (n2 - 0.5) * 0.12);
  float headCut = smoothstep(0.0, 0.012, lag);
  float yy = clamp((y - inner) / max(1.0 - inner, 0.001), 0.0, 1.0);
  float I = 0.22 + 0.78 * pow(yy, 1.35);
  I *= lerp(1.08, 0.5, life);
  I += ((n2 - 0.5) * 0.3 + (streak - 0.5) * 0.35 * yy) * (1.0 - uClean);
  I += smoothstep(0.9, 0.99, y) * 0.35 * (1.0 - life);
  I += smoothstep(0.55, 0.95, yy) * uHot * (1.0 - life);
  I += (1.0 - smoothstep(0.0, 0.05, lag)) * 0.5;
  float d = tex2D(uNoise, float2(sn * 2.4 + uSeed, y * 1.4)).a * 0.6 + n2 * 0.4;
  float vis = smoothstep(uDissolve, uDissolve + 0.03, d);
  float dEdge = vis * (1.0 - smoothstep(uDissolve + 0.03, uDissolve + 0.12, d)) * step(0.0, uDissolve);
  float3 col = ramp(clamp(I, 0.0, 1.0));
  col += dEdge * uC2 * 1.2;
  float a = body * tailCut * headCut * vis * uAlpha;
  a *= lerp(smoothstep(0.02, 0.2, I + 0.15), smoothstep(0.14, 0.34, I), uClean);
  if (a < 0.01) discard;
  _fc = float4(col, a);

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}