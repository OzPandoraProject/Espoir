Shader "EspoirBossFx/GamoriGhostClip" {
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
      float3 uWrist;
      float3 uOff;
      float uK;
      sampler2D uNoise;
      float uT;
      float uAlpha;
      float uSeed;
      float uBoost;
      float uTime;
      float uBodyMode;
      float uNScale;
      float uCutLo;
      float uCutHi;
      float uBase;
      float uFill;
      float3 uC0;
      float3 uC1;
      float3 uC2;
      float3 uC3;
      float3 uClipN;
      float uClipD;
      float uClipGlow;
      struct appdata {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float4 c0 : TEXCOORD1;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float vMask : TEXCOORD0;
        float vH : TEXCOORD1;
        float3 vN : TEXCOORD2;
        float3 vV : TEXCOORD3;
        float3 vP : TEXCOORD4;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float aMask;
      static float vMask;
      static float vH;
      static float3 vN;
      static float3 vV;
      static float3 vP;
      static float4 _fc;

float3 ramp(float i) {
  const float soft = 0.05;
  float3 c = uC0;
  c = lerp(c, uC1, smoothstep(0.18 - soft, 0.18 + soft, i));
  c = lerp(c, uC2, smoothstep(0.40 - soft, 0.40 + soft, i));
  c = lerp(c, uC3, smoothstep(0.80 - soft, 0.80 + soft, i));
  return c;
}

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;
        normal = IN.normal;
        aMask = IN.c0.x;

  vMask = aMask;
  vH = position.y - uWrist.y;
  float3 p = uWrist + (position - uWrist) * uK + uOff;
  vP = p;
  float4 wp = mul(unity_ObjectToWorld, float4(p, 1.0));
  vN = normalize(mul((float3x3)unity_ObjectToWorld, normal));
  vV = normalize(_WorldSpaceCameraPos - wp.xyz);
  o.pos = mul(UNITY_MATRIX_VP, wp);

        o.vMask = vMask;
        o.vH = vH;
        o.vN = vN;
        o.vV = vV;
        o.vP = vP;
        return o;
      }
      float4 frag(v2f i) : SV_Target {
#ifdef ESPOIR_GLOW
        if (EspoirGlowHidden(i.pos)) discard;
#endif
        vMask = i.vMask;
        vH = i.vH;
        vN = i.vN;
        vV = i.vV;
        vP = i.vP;
        _fc = 0;

  float cd = dot(vP, uClipN) - uClipD;
  if (cd < 0.0) discard;
  if (vMask < 0.5) discard;
  float fr = clamp(1.0 - abs(dot(normalize(vN), normalize(vV))), 0.0, 1.0);
  float3 q = vP * uNScale;
  float n = tex2D(uNoise, q.xy + float2(uSeed, q.z * 0.3)).a * 0.7 + tex2D(uNoise, q.zy * 2.2 + uSeed).r * 0.3;

  float h = lerp(distance(vP, uWrist) * 0.3, (vP.y - uWrist.y - uOff.y) * 0.22, uBodyMode);
  float th = uT * 1.25 - 0.15 - h;
  float vis = smoothstep(th, th + 0.04, n);
  float edge = vis * (1.0 - smoothstep(th + 0.04, th + 0.1, n)) * step(0.02, uT);
  float flow = tex2D(uNoise, float2(q.x * 1.5 + uSeed, q.y * 3.0 - uTime * 0.6)).b;
  float cool = lerp(1.08, 0.62, uT);

  float I = (uBase + pow(fr, lerp(1.9, 2.4, uBodyMode)) * (0.68 + 0.3 * uBoost) + (flow - 0.5) * 0.14
      * (1.0 - uBodyMode)) * cool + edge * 0.3;
  float3 col = ramp(clamp(I, 0.0, 0.9 + 0.1 * uBoost));
  float a = (uFill + pow(fr, 1.5) * (1.0 - uFill)) * vis * uAlpha * smoothstep(uCutLo, uCutHi, vH);
  if (a < 0.01) discard;
  _fc = float4(lerp(col, lerp(uC2, uC3, 0.3), (1.0 - smoothstep(0.0, 0.05, cd)) * uClipGlow), a);

        return EspoirOut(_fc);
      }
      ENDCG
    }
  }
}