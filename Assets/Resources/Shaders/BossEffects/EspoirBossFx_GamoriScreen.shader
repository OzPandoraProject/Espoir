// 패스 인덱스는 CEspoirBossFxScreen에서 사용한다.
Shader "Hidden/EspoirBossFx/GamoriScreen" {
  Properties { _MainTex ("", 2D) = "black" {} }
  CGINCLUDE
  #include "UnityCG.cginc"
  sampler2D _MainTex;
  sampler2D _EspoirInkMask;
  float4 _MainTex_TexelSize;
  float _CaAmt, _Aspect, _Expo, _InkMode, _InkSeed;
  float2 _CaCenter, _InkCenter;
  float _BloomFeed, _BloomStrength, _BlurRadius;
  float4 _BlurStep;
  sampler2D _B0, _B1, _B2, _B3, _B4;
  #include "EspoirBossFxCommon.cginc"

  static const float SCENE_EXPOSURE = 1.05, SCENE_SATURATION = 1.05, SCENE_CONTRAST = 1.05, HIGHLIGHT_ROLLOFF = 0.85;
  float3 GamoriLinearToSRGB(float3 c)
  {
      return lerp(c * 12.92, 1.055 * pow(max(c, 0.0), 1.0 / 2.4) - 0.055, step(0.0031308, c));
  }
  float3 GamoriSRGBToLinear(float3 c)
  {
      return lerp(c / 12.92, pow((max(c, 0.0) + 0.055) / 1.055, 2.4), step(0.04045, c));
  }
  float3 CompressHighlights(float3 c) {
    float pk = max(max(c.r, c.g), c.b);
    float pkSoft = pk > 0.7 ? 0.7 + 0.3 * (1.0 - exp(-(pk - 0.7) / 0.3)) : pk;
    return lerp(c / (1.0 + max(c - 0.9, 0.0) * 0.6), c * (pkSoft / max(pk, 1e-5)), HIGHLIGHT_ROLLOFF);
  }
  float3 ApplyColorGrade(float3 c) {
    c *= SCENE_EXPOSURE;
    float l = dot(c, float3(0.2126, 0.7152, 0.0722));
    c = max(l + (c - l) * SCENE_SATURATION, 0.0);
    c = CompressHighlights(c);
    return GamoriSRGBToLinear(saturate((GamoriLinearToSRGB(c) - 0.5) * SCENE_CONTRAST + 0.5));
  }
  float3 RemoveColorGrade(float3 y) {
    float3 lin = GamoriSRGBToLinear(saturate((GamoriLinearToSRGB(y) - 0.5) / SCENE_CONTRAST + 0.5));
    float3 c = lin;
    for (int k = 0; k < 6; k++) c = clamp(c * (lin / max(CompressHighlights(c), 1e-5)), 0.0, 3.0);
    float l = dot(c, float3(0.2126, 0.7152, 0.0722));
    return max(l + (c - l) / SCENE_SATURATION, 0.0) / SCENE_EXPOSURE;
  }
  ENDCG
  SubShader {
    ZTest Always Cull Off ZWrite Off
    Pass {
      CGPROGRAM
      #pragma vertex vert_img
      #pragma fragment frag
      float4 frag(v2f_img i) : SV_Target {
        float4 c = tex2D(_MainTex, i.uv);
        if (_CaAmt >= 0.0002) {
          float2 d = (i.uv - 0.5) * _CaAmt;
          c.r = tex2D(_MainTex, i.uv - d * 2.0).r;
          c.g = tex2D(_MainTex, i.uv - d).g;
        }
        if (abs(_Expo - 1.0) > 0.002) c.rgb = EspoirAces(invACES(min(saturate(c.rgb), 0.97)) * _Expo);
        return c;
      }
      ENDCG
    }
    Pass {
      CGPROGRAM
      #pragma vertex vert_img
      #pragma fragment frag
      float4 frag(v2f_img i) : SV_Target {
        float v = smoothstep(0.3, 0.6, tex2D(_EspoirInkMask, i.uv).r);
        v = _InkMode < 1.5 ? v : 1.0 - v;
        return float4(v, v, v, 1.0);
      }
      ENDCG
    }
    Pass {
      CGPROGRAM
      #pragma vertex vert_img
      #pragma fragment frag
      float4 frag(v2f_img i) : SV_Target {
        float3 c = tex2D(_MainTex, i.uv).rgb;
        if (!(c.r == c.r) || !(c.g == c.g) || !(c.b == c.b)) c = 0;
        float v = dot(c, float3(0.299, 0.587, 0.114));
        return float4(max(c, 0.0) * smoothstep(1.0, 1.01, v), 1);
      }
      ENDCG
    }
    Pass {
      CGPROGRAM
      #pragma vertex vert_img
      #pragma fragment frag
      float4 frag(v2f_img i) : SV_Target {
        float R = _BlurRadius;
        float w0 = 0.39894 / R;
        float ws = w0;
        float3 sum = tex2D(_MainTex, i.uv).rgb * w0;
        for (int k = 1; k < 11; k++) {
          if (k >= R) break;
          float w = 0.39894 * exp(-0.5 * k * k / (R * R)) / R;
          float2 o = _BlurStep.xy * k;
          sum += (tex2D(_MainTex, i.uv + o).rgb + tex2D(_MainTex, i.uv - o).rgb) * w;
          ws += 2.0 * w;
        }
        return float4(sum / ws, 1);
      }
      ENDCG
    }
    Pass {
      CGPROGRAM
      #pragma vertex vert_img
      #pragma fragment frag
      float4 frag(v2f_img i) : SV_Target {
        float4 c = tex2D(_MainTex, i.uv);
        float3 b = tex2D(_B0, i.uv).rgb * 0.72 + tex2D(_B1, i.uv).rgb * 0.66 + tex2D(_B2, i.uv).rgb * 0.60
            + tex2D(_B3, i.uv).rgb * 0.54 + tex2D(_B4, i.uv).rgb * 0.48;
        b *= _BloomStrength;
        float3 y = saturate(c.rgb);
        if (max(b.r, max(b.g, b.b)) < 1e-4) return float4(y, c.a);
        float3 before = RemoveColorGrade(y);
        c.rgb = y + max(ApplyColorGrade(before + b) - ApplyColorGrade(before), 0.0);
        return c;
      }
      ENDCG
    }
    Pass {
      CGPROGRAM
      #pragma vertex vert_img
      #pragma fragment frag
      float _WebLinear;
      float4 frag(v2f_img i) : SV_Target {
        float3 y = saturate(tex2D(_MainTex, i.uv).rgb);
        return float4(_WebLinear > 0.5 ? invACES(y) : y, 1);
      }
      ENDCG
    }
    Pass {
      CGPROGRAM
      #pragma vertex vert_img
      #pragma fragment frag
      sampler2D _WebBuf;
      float _WebLinear;
      float4 frag(v2f_img i) : SV_Target {
        float4 c = tex2D(_MainTex, i.uv);
        float3 y = saturate(c.rgb);
        float3 w = tex2D(_WebBuf, i.uv).rgb;
        if (!(w.r == w.r) || !(w.g == w.g) || !(w.b == w.b)) return float4(y, c.a);
        c.rgb = _WebLinear > 0.5 ? saturate(y + EspoirAces(max(w, 0.0)) - EspoirAces(invACES(y))) : saturate(w);
        return c;
      }
      ENDCG
    }
  }
}
