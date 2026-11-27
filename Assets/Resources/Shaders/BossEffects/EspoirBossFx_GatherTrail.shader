Shader "EspoirBossFx/GatherTrail" {
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
      float3 uHead;
      float3 uTail;
      float uAlpha;
      float uSand;
      struct appdata {
        float4 vertex : POSITION;
        float4 c0 : TEXCOORD1;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float vT : TEXCOORD0;
        float vS : TEXCOORD1;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float aT;
      static float aS;
      static float vT;
      static float vS;
      static float4 _fc;

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;
        aT = IN.c0.x;
        aS = IN.c0.y;
 vT = aT; vS = aS; o.pos = UnityObjectToClipPos(float4(position, 1.0)); 
        o.vT = vT;
        o.vS = vS;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vT = i.vT;
        vS = i.vS;
        _fc = 0;

  float a = pow(max(1.0 - vT, 0.0), 1.4) * uAlpha  * (1.0 - smoothstep(0.6, 1.0, abs(vS)) * (uSand > 0.5 ? 0.6 : 1.0));
  float3 col = lerp(uHead, uTail, vT);
  if (a < 0.01) discard;
  _fc = float4(col, a);

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}