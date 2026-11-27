Shader "EspoirBossFx/GamoriPetalTrail" {
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
      float uT0;
      float uT1;
      float3 uDir;
      float3 uHot;
      float3 uLight;
      float3 uMid;
      float3 uShade;
      float _GamoriPathAdd;
      struct appdata {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
        float4 c0 : TEXCOORD1;
        float4 c1 : TEXCOORD2;
        float4 c2 : TEXCOORD3;
        float4 c3 : TEXCOORD4;
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
      static float3 aFrom;
      static float3 aTo;
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
        aFrom = IN.c2.xyz;
        aTo = IN.c3.xyz;

  float ph = aSeed.x, u = aSeed.y, s3 = aSeed.z;
  float tb = uT0 + (uT1 - uT0) * u, age = uT - tb, life = 0.32 + 0.38 * frac(ph * 5.3), k = clamp(age / life, 0.0, 1.0);
  float carry = 1.0 - exp(-max(age, 0.0) / 0.09);
  float3 c = float3(lerp(aFrom, aTo, u).xy, aTo.z + (lerp(aFrom, aTo, u).z - aTo.z) * (1.0 + _GamoriPathAdd)) + uDir
      * (0.3 + 0.55 * s3) * carry + float3(sin(ph * 43.0), 0.0, cos(ph * 43.0)) * 0.13 * carry;
  c.y -= (0.3 + 0.6 * frac(ph * 3.1)) * age * age * 2.0;
  c.y = max(c.y, 0.03);
  float s = aSeed.w * step(0.0, age) * (1.0 - smoothstep(0.5, 1.0, k));
  float3 ax = normalize(aAxis); float ang = ph * 6.283 + uT * (5.0 + 7.0 * frac(ph * 7.31));
  float3 p = rot(position * s, ax, ang), n = rot(normal, ax, ang);
  p += uDir * dot(p, uDir) * 3.2 * exp(-max(age, 0.0) / 0.06);
  vUv = uv; vTint = frac(ph * 13.7); vHot = 0.25 * exp(-max(age, 0.0) / 0.1);
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