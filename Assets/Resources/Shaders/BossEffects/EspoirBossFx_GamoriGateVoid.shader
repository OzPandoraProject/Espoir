Shader "EspoirBossFx/GamoriGateVoid" {
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
      float3 uGP;
      float3 uGN;
      float uSquash;
      float uBack;
      float3 uGR;
      float3 uVoid;
      float4 uShp;
      float uKind;
      float uOpenK;
      float uFogD;
      struct appdata {
        float4 vertex : POSITION;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float3 vW : TEXCOORD0;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float3 vW;
      static float4 _fc;

float3 squash(float3 w) {
  float3 gb = (EspoirStagePoint(uGP)) - (normalize(EspoirStageDir(uGN)))
      * (uBack * length(EspoirStageDir(float3(0.0, 1.0, 0.0)))), rd = w - _WorldSpaceCameraPos;
  float den = dot(rd, (normalize(EspoirStageDir(uGN))));
  if (dot(w - gb, (normalize(EspoirStageDir(uGN)))) < 0.0 && den < -1e-4)
  {
      float3 q = _WorldSpaceCameraPos + rd * (dot(gb - _WorldSpaceCameraPos, (normalize(EspoirStageDir(uGN)))) / den);
      return lerp(q, w, uSquash);
  }
  return w;
}

static float2 gQ;
bool inOpen(float2 q) {
  float k = uOpenK;
  if (uKind < 0.5) {
    float hw = uShp.x * k;
    if (q.y < 0.0 || q.y > uShp.z) return false;
    float w = hw;
    if (q.y > uShp.y)
    {
        float t = (q.y - uShp.y) / max(uShp.z - uShp.y, 0.001) * 1.7320508 * uShp.x;
        w = min(hw, max(-uShp.x + sqrt(max(4.0 * uShp.x * uShp.x - t * t, 0.0)), 0.0));
    }
    return abs(q.x) < w;
  }
  if (uKind < 1.5) {
    float2 d = float2(q.x / max(uShp.x * k, 0.001), (q.y - uShp.z) / max(uShp.y * k, 0.001));
    return dot(d, d) < 1.0 && q.y > 0.0;
  }
  if (uKind < 2.5) {
    float y = q.y / uShp.y * 2.0 - 1.0;
    if (abs(y) > 1.0) return false;
    return abs(q.x) < uShp.x * k * pow(max(1.0 - pow(abs(y), 1.8), 0.0), 0.75);
  }
  if (q.y < 0.0 || q.y > uShp.y) return false;
  return abs(q.x) < uShp.x * k * (1.0 - q.y / uShp.y);
}
float portal(float3 P) {
  float sd = dot(P - (EspoirStagePoint(uGP)), (normalize(EspoirStageDir(uGN))));
  sd /= length(EspoirStageDir(float3(0.0, 1.0, 0.0))); if (sd >= 0.0) return 0.0;
  float3 rd = P - _WorldSpaceCameraPos; float den = dot(rd, (normalize(EspoirStageDir(uGN))));
  if (den > -1e-4) discard;
  float3 q = _WorldSpaceCameraPos + rd
      * (dot((EspoirStagePoint(uGP)) - _WorldSpaceCameraPos, (normalize(EspoirStageDir(uGN)))) / den);
  gQ = float2(dot(q - (EspoirStagePoint(uGP)), (normalize(EspoirStageDir(uGR)))), q.y - (EspoirStagePoint(uGP)).y);
  gQ /= length(EspoirStageDir(float3(0.0, 1.0, 0.0))); if (!inOpen(gQ)) discard;
  return smoothstep(0.0, uFogD, -sd);
}

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;
 float4 w = mul(unity_ObjectToWorld, float4(position, 1.0));
 vW = w.xyz;
 o.pos = mul(UNITY_MATRIX_VP, float4(squash(w.xyz), 1.0));
        o.vW = vW;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vW = i.vW;
        _fc = 0;
 portal(vW); _fc = float4(uVoid * (1.0 + 1.6 * exp(-gQ.y / 0.7)), 1.0);
        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}