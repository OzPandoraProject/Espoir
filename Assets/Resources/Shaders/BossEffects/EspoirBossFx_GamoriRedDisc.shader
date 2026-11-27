Shader "EspoirBossFx/GamoriRedDisc" {
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
      float uA;
      float3 uD;
      float3 uR;
      float3 uH;
      float uCore;
      float uWide;
      float uMid;
      struct appdata {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float2 vUv : TEXCOORD0;
        float3 vW : TEXCOORD1;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float2 vUv;
      static float3 vW;
      static float4 _fc;

float4 redLight(float I) {
  float3 c = lerp(uD, uR, smoothstep(0.03, 0.4, I));
  c = lerp(c, uH, smoothstep(0.62, 1.25, I));
  return float4(c, min(I, 1.0) * 0.46);
}

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;
        uv = IN.uv;
 vUv = uv; float4 w = mul(unity_ObjectToWorld, float4(position, 1.0)); vW = w.xyz; o.pos = mul(UNITY_MATRIX_VP, w);
        o.vUv = vUv;
        o.vW = vW;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vUv = i.vUv;
        vW = i.vW;
        _fc = 0;

  float r = length(vUv * 2.0 - 1.0);
  float I = uA * (uCore * (1.15 * exp(-r * r * 26.0) + 0.5 * exp(-r * r * 7.0)) + uWide * exp(-r * r * 2.6) + uMid
      * exp(-r * r * 6.5)) * (1.0 - smoothstep(0.78, 1.0, r));
  if (I < 0.012) discard;
  _fc = redLight(I);

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}