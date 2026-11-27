Shader "EspoirBossFx/JinPillar" {
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
      sampler2D uNoise;
      float uT;
      float uAlpha;
      float uSeed;
      float uFade;
      float uH;
      float3 uC1;
      float3 uC2;
      float3 uC3;
      struct appdata {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float2 vUv : TEXCOORD0;
        float3 vN : TEXCOORD1;
        float3 vV : TEXCOORD2;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float2 vUv;
      static float3 vN;
      static float3 vV;
      static float4 _fc;

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;
        normal = IN.normal;
        uv = IN.uv;

  vUv = uv;
  float4 wp = mul(unity_ObjectToWorld, float4(position, 1.0));
  vN = normalize(mul((float3x3)unity_ObjectToWorld, normal));
  vV = normalize(_WorldSpaceCameraPos - wp.xyz);
  o.pos = mul(UNITY_MATRIX_VP, wp);

        o.vUv = vUv;
        o.vN = vN;
        o.vV = vV;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vUv = i.vUv;
        vN = i.vN;
        vV = i.vV;
        _fc = 0;

  float y = clamp(vUv.y, 0.0, 1.0), m = y * uH;
  float n = tex2D(uNoise, float2(vUv.x * 3.0 + uSeed, m * 0.03 - uT * 2.6)).r;
  float streak = tex2D(uNoise, float2(vUv.x * 10.0 + uSeed * 2.0, m * 0.008 - uT * 1.2)).g;
  float face = abs(dot(normalize(vN), normalize(vV)));
  float rim = pow(max(1.0 - face, 0.0), 2.2);
  float base = exp(-m / 6.0);
  float top = 1.0 - smoothstep(0.45, 1.0, y);
  float stroke = smoothstep(0.42, 0.8, streak) * (0.35 + 0.65 * n);

  float tear = smoothstep(uFade * 1.25 - 0.25, uFade * 1.25, n * 0.6 + streak * 0.4);
  float k = top * tear * uAlpha * smoothstep(0.0, 0.01, y);
  float3 col = uC2 * (0.05 + 0.3 * stroke + 0.9 * rim * (0.6 + 0.4 * n)) + uC3 * (0.3 * rim * rim * (0.4 + 0.6 * stroke) + 0.12 * base * rim);
  _fc = float4(col * k, 1.0);

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}