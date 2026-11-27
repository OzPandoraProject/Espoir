Shader "EspoirBossFx/GamoriRingDistort" {
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
      float uAmt;
      float uR;
      float uW;
      float uAspect;
      struct appdata {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };
      struct v2f {
        float4 pos : SV_POSITION;
        float2 vUv : TEXCOORD0;
        float3 vW : TEXCOORD1;
        float4 grabPos : TEXCOORD2;
        float4 scrPos : TEXCOORD3;
      };
      static float3 position; static float3 normal; static float2 uv;
      static float2 vUv;
      static float3 vW;
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
 vUv = uv; float4 w = mul(unity_ObjectToWorld, float4(position, 1.0)); vW = w.xyz; o.pos = mul(UNITY_MATRIX_VP, w);
        o.vUv = vUv;
        o.vW = vW;
        o.grabPos = ComputeGrabScreenPos(o.pos);
        o.scrPos = ComputeScreenPos(o.pos);
        return o;
      }
      float4 frag(v2f i) : SV_Target {
        vUv = i.vUv;
        vW = i.vW;
        _fc = 0;

  float2 p = vUv * 2.0 - 1.0;
  float r = length(p);
  if (r > 1.0 || r < 1e-4) discard;
  float ring = exp(-pow((r - uR) / uW, 2.0)) * (1.0 - smoothstep(0.78, 1.0, r));
  float2 gr = float2(ddx(r), ddy(r)); float2 off = (gr / max(length(gr), 1e-7)) * ring * uAmt;

  return GamoriDistortOut(off, i.grabPos, i.scrPos);

        return 0;
      }
      ENDCG
    }
  }
}