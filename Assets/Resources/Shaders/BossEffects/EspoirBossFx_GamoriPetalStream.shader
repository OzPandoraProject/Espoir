Shader "EspoirBossFx/GamoriPetalStream" {
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
      float uT;
      float uOn;
      float uOff;
      float uLife;
      float uH;
      float3 uA;
      float3 uB;
      float3 uHot;
      float3 uLight;
      float3 uMid;
      float3 uShade;
      struct appdata {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
        float4 c0 : TEXCOORD1;
        float4 c1 : TEXCOORD2;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float2 vUv : TEXCOORD0;
        float3 vN : TEXCOORD1;
        float3 vW : TEXCOORD2;
        float vTint : TEXCOORD3;
        float vHot : TEXCOORD4;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float4 aSeed;
      static float3 aAxis;
      static float2 vUv;
      static float3 vN;
      static float3 vW;
      static float vTint;
      static float vHot;
      static float4 _fc;

float3 rot(float3 p, float3 ax, float a)
{
    return p * cos(a) + cross(ax, p) * sin(a) + ax * dot(ax, p) * (1.0 - cos(a));
}

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;
        normal = IN.normal;
        uv = IN.uv;
        aSeed = IN.c0.xyzw;
        aAxis = IN.c1.xyz;

  float ph = aSeed.x, s = aSeed.w;
  float te = uOn + aSeed.y * (uOff - uOn), life = uLife * (0.75 + 0.5 * frac(ph * 9.7)),
      k = clamp((uT - te) / life, 0.0, 1.0);
  float e = lerp(k * k * (3.0 - 2.0 * k), k * k, 0.6);
  float3 a0 = uA + float3(sin(ph * 43.0) * 0.26, 0.2 + aSeed.z * uH * 0.9, cos(ph * 43.0) * 0.26);
  float3 mid = lerp(a0, uB, 0.45) + float3(sin(ph * 17.0) * 0.42, 1.0 + 0.7 * frac(ph * 5.3), cos(ph * 29.0) * 0.42);
  float3 c = lerp(lerp(a0, mid, e), lerp(mid, uB, e), e);
  s *= step(te, uT) * step(uT, te + life) * smoothstep(0.0, 0.1, k) * (1.0 - 0.72 * smoothstep(0.55, 1.0, k));
  float3 ax = normalize(aAxis); float ang = ph * 6.283 + uT * (1.6 + 3.2 * frac(ph * 7.31));
  float3 p = rot(position * s, ax, ang), n = rot(normal, ax, ang);
  vUv = uv; vTint = frac(ph * 13.7); vHot = smoothstep(0.72, 1.0, k) * 0.8;
  vN = normalize(mul((float3x3)unity_ObjectToWorld, n));
  float4 w = mul(unity_ObjectToWorld, float4(c + p, 1.0)); vW = w.xyz;
  o.pos = mul(UNITY_MATRIX_VP, w);

        o.vUv = vUv;
        o.vN = vN;
        o.vW = vW;
        o.vTint = vTint;
        o.vHot = vHot;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vUv = i.vUv;
        vN = i.vN;
        vW = i.vW;
        vTint = i.vTint;
        vHot = i.vHot;
        _fc = 0;

  float3 n = normalize(vN), V = normalize(_WorldSpaceCameraPos - vW);
  if (dot(n, V) < 0.0) n = -n;
  float l = dot(n, normalize(EspoirStageDir(float3(0.4, 0.8, 0.45))));
  float3 c = lerp(uShade, uMid, smoothstep(-0.12, -0.02, l));
  c = lerp(c, uLight, smoothstep(0.32, 0.42, l));
  c = lerp(c, uLight * 1.25 + float3(0.06, 0.02, 0.03), smoothstep(0.78, 0.84, vUv.y) * 0.7);
  c *= 0.72 + 0.5 * vTint;
  c = invACES(clamp(c, 0.0, 0.95));
  c += uHot * vHot * 0.7;
  _fc = float4(c, 1.0);

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}