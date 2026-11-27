Shader "EspoirBossFx/GamoriGlint" {
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
      float uRot;
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

  float r = length(vP), a = atan2(vP.x, vP.y) + uRot;
  float rays = pow(abs(cos(a * 2.0)), 46.0) + 0.55 * pow(abs(cos(a * 2.0 + 1.5708)), 80.0);
  float I = uA * (1.5 * exp(-r / 0.11) + rays * exp(-r / 0.34));
  if (I < 0.02) discard;
  _fc = float4(lerp(uC2, uC3, clamp(I - 0.25, 0.0, 1.0)), clamp(I, 0.0, 1.0));

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}