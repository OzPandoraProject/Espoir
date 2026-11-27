Shader "EspoirBossFx/GamoriRoseLine" {
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
      float uPush;
      float3 uLine;
      sampler2D uNoise;
      float uDis;
      float uSeed;
      float uH;
      float uGrow;
      float uFloorY;
      struct appdata {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
        float4 c0 : TEXCOORD1;
        float4 c1 : TEXCOORD2;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float3 vW : TEXCOORD0;
        float3 vL : TEXCOORD1;
        float3 vN : TEXCOORD2;
        float2 vUv : TEXCOORD3;
        float vRing : TEXCOORD4;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float2 uv2;
      static float3 nSm;
      static float3 vW;
      static float3 vL;
      static float3 vN;
      static float2 vUv;
      static float vRing;
      static float4 _fc;

float propDis() {
  float h = clamp(vL.y / uH, 0.0, 1.0);
  return tex2D(uNoise, vL.xz * 0.45 + float2(vL.y * 0.3, uSeed)).a * 0.6 + (1.0 - h) * 0.4;
}

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;
        normal = IN.normal;
        uv = IN.uv;
        uv2 = IN.c0.xy;
        nSm = IN.c1.xyz;

  vUv = uv; vRing = uv2.x;
  float3 p = position, n = normal;
  vL = p; vN = normalize(mul((float3x3)unity_ObjectToWorld, n));
  p += normalize(nSm) * uPush
      * clamp(distance(_WorldSpaceCameraPos, mul(unity_ObjectToWorld, float4(p, 1.0)).xyz) / 22.0, 0.3, 1.0);
  float4 w = mul(unity_ObjectToWorld, float4(p, 1.0));
  vW = w.xyz;
  o.pos = mul(UNITY_MATRIX_VP, w);

        o.vW = vW;
        o.vL = vL;
        o.vN = vN;
        o.vUv = vUv;
        o.vRing = vRing;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vW = i.vW;
        vL = i.vL;
        vN = i.vN;
        vUv = i.vUv;
        vRing = i.vRing;
        _fc = 0;
 if (vW.y < EspoirStagePoint(float3(0.0, uFloorY, 0.0)).y || vUv.x > uGrow) discard;
 if (propDis() < uDis) discard;
 _fc = float4(invACES(uLine), 1.0);
        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}