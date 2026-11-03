Shader "Espoir/Player/Effects/Flare"
{
    Properties
    {
        _Lifetime ("수명 (초)", Float) = 0.35
        _Size ("크기 (반폭)", Float) = 9.6
        _Peak ("밝기", Float) = 1.1
        _Hot ("Hot (Linear HDR)", Vector) = (6, 2.6, 2.1, 0)
        _Red ("Red (Linear HDR)", Vector) = (2.6, 0.07, 0.09, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+40" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend One One, Zero One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            float4 _Hot, _Red;
            float _Size, _Peak;

            struct Appdata { float4 vertex : POSITION; float4 uvAge : TEXCOORD0; float3 center : TEXCOORD1; };
            struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float a : TEXCOORD1; };

            Vary vert(Appdata v)
            {
                Vary o;
                float k = v.uvAge.z;
                float size = _Size * (0.8 + 0.4 * k);
                float2 q = v.uvAge.xy * 2.0 - 1.0;
                float3 wp = v.center + UNITY_MATRIX_V[0].xyz * q.x * size + UNITY_MATRIX_V[1].xyz * q.y * size;
                o.pos = UnityWorldToClipPos(wp);
                o.uv = q;
                o.a = _Peak * pow(1.0 - k, 1.6);
                return o;
            }

            float4 frag(Vary i) : SV_Target
            {
                float r = length(i.uv);
                float g = exp(-r * r * 14.0);
                float halo = exp(-r * 4.5) * (1.0 - smoothstep(0.5, 1.0, r));
                float mask = 1.0 - smoothstep(0.7, 1.0, r);
                float3 c = (_Hot.rgb * g + _Red.rgb * halo * 0.7) * i.a * mask;

                return float4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
