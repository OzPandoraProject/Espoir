Shader "Espoir/Effects/Slash"
{
    Properties
    {
        _Texture01 ("Main Texture", 2D) = "white" {}
        _EXP ("Alpha Exponent", Float) = 1.68
        _C0 ("Dark", Color) = (0.03, 0.0, 0.01, 1)
        _C1 ("Burgundy", Color) = (0.3, 0.0, 0.03, 1)
        [HDR] _C2 ("Crimson", Color) = (2.0, 0.04, 0.06, 1)
        [HDR] _C3 ("Hot Edge", Color) = (4.5, 1.6, 1.2, 1)
        _Steps ("Color Thresholds", Vector) = (0.18, 0.4, 0.8, 0)
        _ColorSoftness ("Color Softness", Range(0.001, 0.2)) = 0.05
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _Texture01;
            float4 _Texture01_ST;
            half4 _C0;
            half4 _C1;
            half4 _C2;
            half4 _C3;
            float _EXP;
            float4 _Steps;
            float _ColorSoftness;

            v2f Vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _Texture01);
                return o;
            }

            half4 Frag(v2f i) : SV_Target
            {
                half4 textureColor = tex2D(_Texture01, i.uv);
                float alpha = pow(saturate(textureColor.a), _EXP) * i.color.a;

                float intensity = textureColor.r;
                half3 rampColor = _C0.rgb;
                rampColor = lerp(rampColor, _C1.rgb,
                    smoothstep(_Steps.x - _ColorSoftness, _Steps.x + _ColorSoftness, intensity));
                rampColor = lerp(rampColor, _C2.rgb,
                    smoothstep(_Steps.y - _ColorSoftness, _Steps.y + _ColorSoftness, intensity));
                rampColor = lerp(rampColor, _C3.rgb,
                    smoothstep(_Steps.z - _ColorSoftness, _Steps.z + _ColorSoftness, intensity));
                half3 finalColor = rampColor * i.color.rgb;

                return half4(finalColor, alpha);
            }
            ENDCG
        }
    }
}
