Shader "EspoirBossFx/GamoriGateProp" {
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
      float uGrow;
      float3 uGP;
      float3 uGN;
      float uSquash;
      float uBack;
      float uGlow;
      float uBase;
      float3 uHot;
      float3 uLight;
      float3 uMid;
      float3 uShade;
      sampler2D uNoise;
      float uDis;
      float uSeed;
      float uH;
      float uFloorY;
      float uNoChunk;
      float3 uGR;
      float3 uVoid;
      float4 uShp;
      float uKind;
      float uOpenK;
      float uFogD;
      struct appdata {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
        float4 c0 : TEXCOORD1;
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
      static float3 vW;
      static float3 vL;
      static float3 vN;
      static float2 vUv;
      static float vRing;
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

float propDis() {
  float h = clamp(vL.y / uH, 0.0, 1.0);
  return tex2D(uNoise, vL.xz * 0.45 + float2(vL.y * 0.3, uSeed)).a * 0.6 + (1.0 - h) * 0.4;
}

void propBody() {
  if (vW.y < EspoirStagePoint(float3(0.0, uFloorY, 0.0)).y || vUv.x > uGrow) discard;
  float dz = propDis();
  if (dz < uDis) discard;
  float burn = uDis > 0.0 ? 1.0 - smoothstep(0.0, 0.07, dz - uDis) : 0.0;
  float3 n = cross(ddx(vW), ddy(vW));
  n = dot(n, n) > 1e-20 ? normalize(n) : normalize(vN);
  float3 V = normalize(_WorldSpaceCameraPos - vW);
  if (dot(n, V) < 0.0) n = -n;
  float l = dot(n, normalize(EspoirStageDir(float3(0.4, 0.8, 0.45))));
  float3 c = lerp(uShade, uMid, smoothstep(-0.12, -0.02, l));
  c = lerp(c, uLight, smoothstep(0.42, 0.52, l));
  c = invACES(c);
  float fr = pow(clamp(1.0 - dot(n, V), 0.0, 1.0), 2.5);
  float bottom = 1.0 - smoothstep(0.0, 0.6, vL.y / uH);
  c += uHot * (uGlow * (0.9 * bottom * bottom + 0.6 * fr) + uBase * bottom * bottom);
  c += uHot * (1.0 - smoothstep(0.0, 0.1, uGrow - vUv.x)) * step(uGrow, 1.0) * 1.3;
  c = lerp(c, uHot * 1.4, burn);
  _fc = float4(c, 1.0);
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
        normal = IN.normal;
        uv = IN.uv;
        uv2 = IN.c0.xy;

  vUv = uv; vRing = uv2.x;
  float3 p = position; float k = 1.0;
  vL = p; vN = normalize(mul((float3x3)unity_ObjectToWorld, normal));
  p += normal * uPush * k;
  float4 w = mul(unity_ObjectToWorld, float4(p, 1.0));
  vW = w.xyz;
  o.pos = mul(UNITY_MATRIX_VP, float4(squash(w.xyz), 1.0));

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
 if (uNoChunk > 0.5 && vRing > 0.9) discard; float fog = portal(vW); propBody(); _fc.rgb = lerp(_fc.rgb, uVoid, fog);
        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}