Shader "EspoirBossFx/GamoriWall" {
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
      float uT;
      float uAlpha;
      float uSeed;
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
        float3 vW : TEXCOORD2;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float2 vUv;
      static float3 vN;
      static float3 vW;
      static float4 _fc;

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;
        normal = IN.normal;
        uv = IN.uv;
 vUv = uv;
 float4 w = mul(unity_ObjectToWorld, float4(position, 1.0));
 vW = w.xyz;
 vN = normalize(mul((float3x3)unity_ObjectToWorld, normal));
 o.pos = mul(UNITY_MATRIX_VP, w);
        o.vUv = vUv;
        o.vN = vN;
        o.vW = vW;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vUv = i.vUv;
        vN = i.vN;
        vW = i.vW;
        _fc = 0;

  float y = clamp(vUv.y, 0.0, 1.0);
  float n = tex2D(uNoise, float2(vUv.x * 6.0 + uSeed, y * 0.6 - uT * 2.5)).r;
  float streak = tex2D(uNoise, float2(vUv.x * 18.0 + uSeed * 3.0, 0.2)).g;
  float body = pow(max(1.0 - y, 0.0), 1.6) * (0.55 + 0.45 * n) * (0.6 + 0.4 * streak);
  float edge = smoothstep(0.0, 0.08, y);
  float3 col = lerp(uC1, uC3, clamp(body * 1.3, 0.0, 1.0));
  float a = body * edge * uAlpha;
  if (a < 0.01) discard;
  _fc = float4(col * 1.4, a);

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}