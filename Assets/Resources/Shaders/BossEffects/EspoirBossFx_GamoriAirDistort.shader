Shader "EspoirBossFx/GamoriAirDistort" {
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
      Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest [_ZTest] Cull Off
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.5
      #include "UnityCG.cginc"
      #include "EspoirBossFxCommon.cginc"
      float3 uCenter;
      float uExpand;
      float uRigid;
      float uGrow;
      float uFloor;
      float uHead;
      float uTail;
      float uAmt;
      float uTime;
      float uWaves;
      float uAspect;
      float uFlat;
      struct appdata {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float2 vUv : TEXCOORD0;
        float3 vWorld : TEXCOORD1;
        float4 grabPos : TEXCOORD2;
        float4 scrPos : TEXCOORD3;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float2 vUv;
      static float3 vWorld;
      static float4 _fc;

float4 GamoriDistortOut(float2 off, float4 grabPos, float4 scrPos) {
  if (!(off.x == off.x) || !(off.y == off.y)) off = 0;
  float2 guv = grabPos.xy / grabPos.w, suv = scrPos.xy / scrPos.w;
  float asp = _ScreenParams.x / _ScreenParams.y;
  float2 og = float2(off.x * sign(ddx(guv.x)) / asp, off.y * sign(ddy(guv.y)));
  float2 os = float2(off.x * sign(ddx(suv.x)) / asp, off.y * sign(ddy(suv.y)));
  float here = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, suv));
  float there = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, suv + os));
  if (there < here - 0.5) og = 0;
  float3 c = float3(tex2D(_EspoirBossGrab, guv + og * 1.3).r, tex2D(_EspoirBossGrab, guv + og).g,
      tex2D(_EspoirBossGrab, guv + og * 0.7).b);
  return float4(c, saturate(length(og) * 600.0) * _EspoirGrabOn);
}

      v2f vert(appdata IN) {
        v2f o = (v2f)0;
        position = IN.vertex.xyz;
        uv = IN.uv;

  vUv = uv;
  float3 p = uCenter + (position - uCenter) * (1.0 - uGrow);
  float3 d = p - uCenter;
  p += normalize(d) * uExpand * lerp(0.35 + uv.y, 1.0 + 0.25 * uv.y, uRigid);
  vWorld = mul(unity_ObjectToWorld, float4(p, 1.0)).xyz;
  vWorld.y = max(vWorld.y, EspoirStagePoint(float3(0.0, uFloor, 0.0)).y);
  o.pos = mul(UNITY_MATRIX_VP, float4(vWorld, 1.0));

        o.vUv = vUv;
        o.vWorld = vWorld;
        o.grabPos = ComputeGrabScreenPos(o.pos);
        o.scrPos = ComputeScreenPos(o.pos);
        return o;
      }
      float4 frag(v2f i) : SV_Target {
        vUv = i.vUv;
        vWorld = i.vWorld;
        _fc = 0;

  float s = vUv.x, y = vUv.y, lag = uHead - s;
  if (lag < 0.0) discard;
  float life = clamp(lag / max(uHead - uTail, 0.001), 0.0, 1.0);
  float env = lerp(smoothstep(0.0, 0.06, lag) * (1.0 - life) * smoothstep(uTail, uTail + 0.1, s), 1.0, uFlat);
  env *= sin(3.14159 * y);
  float2 g = float2(ddx(y), ddy(y));
  float gl = length(g);
  if (gl < 1e-7) discard;
  float prof = uWaves < 0.5 ? sin((y - 0.5) * 6.28318) : sin(y * 3.14159 * uWaves - uTime * 16.0);
  float2 off = (g / gl) * prof * env * uAmt;

  return GamoriDistortOut(off, i.grabPos, i.scrPos);

        return 0;
      }
      ENDCG
    }
  }
}