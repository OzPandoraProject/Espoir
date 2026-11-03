Shader "Hidden/Espoir/Player/Effects/Composite"
{
    Properties { _MainTex ("", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _Map, _Offset;

    static const float3 REFERENCE_GROUND = float3(0.217604, 0.051244, 0.026199);
    struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    Vary vert(appdata_img v) { Vary o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
    float Peak(float3 c) { return max(c.r, max(c.g, c.b)); }

    float4 _Dark, _Bright, _Core;
    float3 Recolor(float3 c)
    {

        float white = min(c.r, min(c.g, c.b));
        float amount = Peak(c - white);
        float3 o = lerp(_Dark.rgb, _Bright.rgb, saturate(amount * 0.8)) * amount + _Core.rgb * white;
        float3 luma = float3(0.2126, 0.7152, 0.0722);
        return o * lerp(1.0, dot(c, luma) / max(dot(o, luma), 0.0001), _Core.a);
    }

    static const float3x3 AcesIn = float3x3(
        0.59719, 0.35458, 0.04823,
        0.07600, 0.90834, 0.01566,
        0.02840, 0.13383, 0.83777);
    static const float3x3 AcesOut = float3x3(
        1.60475, -0.53108, -0.07367,
        -0.10208, 1.10813, -0.00605,
        -0.00327, -0.07276, 1.07602);

    float3 Aces(float3 c)
    {
        float3 v = mul(AcesIn, c / 0.6);
        float3 a = v * (v + 0.0245786) - 0.000090537;
        float3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
        return saturate(mul(AcesOut, a / b));
    }
    struct Layer
    {
        float3 map;
        float3 light;
        float cover;
    };

    Layer Split(float2 uv)
    {
        uv += tex2D(_Offset, uv).rg;
        float4 fx = tex2D(_MainTex, uv);
        Layer l;
        l.map = tex2D(_Map, uv).rgb;
        l.light = Recolor(max(fx.rgb, 0.0));
        l.cover = saturate(1.0 - fx.a);
        return l;
    }
    ENDCG
    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            float _Threshold;
            float4 frag(Vary i) : SV_Target
            {
                Layer l = Split(i.uv);
                if (Peak(l.light) < 0.00001 && l.cover < 0.00001) return 0;
                float4 c = float4(REFERENCE_GROUND * (1.0 - l.cover) + l.light, 1.0);
                float v = dot(c.rgb, float3(0.2126, 0.7152, 0.0722));
                return lerp(float4(0, 0, 0, 0), c, smoothstep(_Threshold, _Threshold + 0.01, v));
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            float2 _Step;
            float _Kernel;
            float _Coef[11];
            float4 frag(Vary i) : SV_Target
            {
                float w0 = _Coef[0];
                float3 sum = tex2D(_MainTex, i.uv).rgb * w0;
                for (int k = 1; k < 11 && k < _Kernel; k++)
                {
                    float w = _Coef[k];
                    float2 o = _Step * k;
                    sum += (tex2D(_MainTex, i.uv + o).rgb + tex2D(_MainTex, i.uv - o).rgb) * w;
                    w0 += 2.0 * w;
                }
                return float4(sum / w0, 1);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            sampler2D _B1, _B2, _B3, _B4, _B5;
            float _Strength, _Radius;
            float F(float f) { return lerp(f, 1.2 - f, _Radius); }
            float4 frag(Vary i) : SV_Target
            {
                return _Strength * (F(1.0) * tex2D(_B1, i.uv) + F(0.8) * tex2D(_B2, i.uv)
                    + F(0.6) * tex2D(_B3, i.uv) + F(0.4) * tex2D(_B4, i.uv) + F(0.2) * tex2D(_B5, i.uv));
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            sampler2D _Bloom;
            float _Impact, _ImpactInvert, _White;

            float4 frag(Vary i) : SV_Target
            {
                Layer l = Split(i.uv);
                float4 bloom = tex2D(_Bloom, i.uv);
                float3 glow = bloom.rgb * bloom.a;
                if (Peak(glow + l.light) < 0.00001 && l.cover < 0.00001 && _Impact <= 0.0 && _White <= 0.0)
                    return float4(l.map, 1);
                float3 base = Aces(REFERENCE_GROUND);
                float3 hdr = REFERENCE_GROUND * (1.0 - l.cover) + l.light + glow;
                float3 ink = Aces(hdr);
                float3 mark = Aces(REFERENCE_GROUND * (1.0 - l.cover) + l.light);
                float contrast = Peak(abs(LinearToGammaSpace(mark) - LinearToGammaSpace(base)));
                float through = pow(1.0 - l.cover, 4.0) * (1.0 - smoothstep(0.0, 0.12, contrast));
                float halo = saturate((ink.r - mark.r) / max(1.0 - mark.r, 0.001));
                float3 scene = ink + (l.map - base) * through * (1.0 - halo);
                if (_Impact <= 0.0 && _White <= 0.0) return float4(max(scene, 0), 1);

                float sceneLuma = dot(l.map, float3(0.2126, 0.7152, 0.0722));

                hdr += REFERENCE_GROUND * (clamp(sceneLuma / 0.30, 0.0, 5.0) - 1.0) * through;
                float3 beforeFlash = Aces(hdr);
                float hi = smoothstep(0.32, 0.38, dot(hdr, float3(0.3, 0.55, 0.15)));
                float3 two = lerp(float3(0.012, 0.0, 0.004), Recolor(float3(3.2, 0.5, 0.42)),
                    lerp(hi, 1.0 - hi, _ImpactInvert));
                hdr = lerp(hdr, two, _Impact) + Recolor(float3(3.0, 1.5, 1.3)) * _White;
                float3 flash = Aces(hdr);
                float flashContrast = Peak(abs(LinearToGammaSpace(flash) - LinearToGammaSpace(beforeFlash)));
                float retention = (1.0 - _Impact) * (1.0 - smoothstep(0.0, 0.20, flashContrast));
                return float4(max(flash + (scene - beforeFlash) * retention, 0), 1);
            }
            ENDCG
        }

    }
}
