Shader "Espoir/Effects/SlashDissolve"
{
    Properties
    {
        _Texture01 ("Main Texture", 2D) = "white" {}
        _Noise ("Noise", 2D) = "gray" {}
        _Dissolve ("Dissolve", 2D) = "gray" {}
        [HDR] _Color ("Color", Color) = (0.319, 0.837, 1.55, 1)
        _color_intensity ("Color Intensity", Float) = 1.17
        _EXP ("Intensity Exponent", Float) = 0.63
        [HDR] _EDgecolor ("Edge Color", Color) = (0.647, 1.086, 1.691, 1)
        _EDgecolor_intensity ("Edge Color Intensity", Float) = 3.92
        _EDGe ("Edge Exponent", Float) = 1.41
        _DissovleEdge ("Dissolve Edge", Float) = 0.22
        _DissolveSoftness ("Dissolve Softness", Range(0.001, 0.2)) = 0.02
        _DissolveNoiseStrength ("Dissolve Noise Strength", Range(0, 1)) = 0.1
        _Noise_uspeed ("Noise U Speed", Float) = 0.25
        _Noise_vspeed ("Noise V Speed", Float) = -0.2
        _Noise_Uintensity ("Noise U Intensity", Float) = 0.18
        _Noise_Vintensity ("Noise V Intensity", Float) = 0.4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float2 custom : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 custom : TEXCOORD1;
            };

            sampler2D _Texture01;
            sampler2D _Noise;
            sampler2D _Dissolve;
            float4 _Texture01_ST;
            float4 _Noise_ST;
            float4 _Dissolve_ST;
            half4 _Color;
            half4 _EDgecolor;
            float _color_intensity;
            float _EXP;
            float _EDgecolor_intensity;
            float _EDGe;
            float _DissovleEdge;
            float _Noise_uspeed;
            float _Noise_vspeed;
            float _Noise_Uintensity;
            float _Noise_Vintensity;
            float _DissolveSoftness;
            float _DissolveNoiseStrength;

            v2f Vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv.xy;
                o.custom = float4(v.uv.zw, v.custom.xy);
                return o;
            }

            half4 Frag(v2f i) : SV_Target
            {
                float2 noiseUv = i.uv * _Noise_ST.xy + _Noise_ST.zw +
                    float2(_Noise_uspeed, _Noise_vspeed) * _Time.y;
                float2 noise = tex2D(_Noise, noiseUv).rg - 0.5;
                float2 uv = TRANSFORM_TEX(i.uv, _Texture01) +
                    noise * float2(_Noise_Uintensity, _Noise_Vintensity) + i.custom.zw;
                half4 textureColor = tex2D(_Texture01, uv);
                float2 dissolveUv = TRANSFORM_TEX(i.uv, _Dissolve) + noise * _DissolveNoiseStrength;
                float dissolve = tex2D(_Dissolve, dissolveUv).r;
                float threshold = i.custom.x;
                float visibility = smoothstep(threshold, threshold + _DissolveSoftness, dissolve);
                float edge = visibility *
                    (1 - smoothstep(threshold + _DissolveSoftness, threshold + _DissovleEdge, dissolve));
                float intensity = pow(saturate(textureColor.r), _EXP);
                half3 finalColor = intensity * _Color.rgb * _color_intensity;
                finalColor += edge * pow(saturate(textureColor.a), _EDGe) * _EDgecolor.rgb * _EDgecolor_intensity;
                float alpha = saturate(textureColor.a) * visibility * i.color.a;
                return half4(finalColor * i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
