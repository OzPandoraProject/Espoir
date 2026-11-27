Shader "EspoirBossFx/GamoriPetalBurst" {
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
      float uLife;
      float uSpeed;
      float uDrag;
      float uGrav;
      float uStag;
      float3 uCenter;
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
        float4 c2 : TEXCOORD3;
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
      static float3 aDir;
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
        aDir = IN.c2.xyz;

  float ph = aSeed.x, tau = uT - uOn - aSeed.z * uStag, tt = max(tau, 0.0),
      k = clamp(tau / (uLife * (0.55 + 0.45 * ph)), 0.0, 1.0);
  float v = uSpeed * (0.2 + 0.8 * aSeed.y);
  float3 c = uCenter + aDir * (v * (1.0 - exp(-uDrag * tt)) / uDrag);
  c.y -= 0.5 * uGrav * tt * tt * (0.3 + 0.7 * frac(ph * 5.3));
  c.xz += 0.3 * float2(sin(tt * 2.1 + ph * 40.0), cos(tt * 1.7 + ph * 23.0)) * smoothstep(0.15, 1.0, tt);
  c.y = max(c.y, 0.05);
  float s = aSeed.w * step(0.0, tau) * smoothstep(0.0, 0.03, k) * (1.0 - smoothstep(0.7, 1.0, k));
  float3 ax = normalize(aAxis);
  float ang = ph * 6.283 + tt * (1.6 + 4.0 * frac(ph * 7.31)) + 5.0 * (1.0 - exp(-tt * 7.0));
  float3 p = rot(position * s, ax, ang), n = rot(normal, ax, ang);
  p += aDir * dot(p, aDir) * 1.0 * exp(-tt / 0.04);
  vUv = uv; vTint = frac(ph * 13.7); vHot = exp(-tt / 0.07);
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