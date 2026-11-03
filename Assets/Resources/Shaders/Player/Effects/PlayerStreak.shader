Shader "Espoir/Player/Effects/Streak"
{
    Properties
    {
        [NoScaleOffset] _Data ("줄기 데이터", 2D) = "black" {}
        _Lifetime ("파티클 수명 (초)", Float) = 3
        _FadeAt ("모으기 끝 (경로형이 꺼지기 시작하는 시각)", Float) = 1000
        _FadeLen ("꺼지는 시간", Float) = 0.08
        _Hot ("Hot (Linear HDR)", Vector) = (6, 2.6, 2.1, 0)
        _Red ("Red (Linear HDR)", Vector) = (2.6, 0.07, 0.09, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+30" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off ZWrite Off Blend One One, Zero One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            sampler2D _Data;
            float4 _Data_TexelSize, _Hot, _Red;
            float _Lifetime, _FadeAt, _FadeLen;

            struct Appdata
            {
                float4 vertex : POSITION;
                float4 tangent : TANGENT;
                float4 uvAge : TEXCOORD0;
                float4 idCenter : TEXCOORD1;
                float centerZ : TEXCOORD2;
            };
            struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float a : TEXCOORD1; };

            float3 Bezier(float3 a, float3 c, float3 b, float k)
            {
                return a * (1.0 - k) * (1.0 - k) + c * 2.0 * k * (1.0 - k) + b * k * k;
            }

            Vary vert(Appdata v)
            {
                Vary o;
                float row = (v.idCenter.x + 0.5) * _Data_TexelSize.y;
                float3 center = float3(v.idCenter.zw, v.centerZ);
                float4 d0 = tex2Dlod(_Data, float4(0.125, row, 0.0, 0.0));
                float4 d1 = tex2Dlod(_Data, float4(0.375, row, 0.0, 0.0));
                float4 d2 = tex2Dlod(_Data, float4(0.625, row, 0.0, 0.0));
                float4 d3 = tex2Dlod(_Data, float4(0.875, row, 0.0, 0.0));
                float T = v.uvAge.z * _Lifetime;
                float age = T - d0.w, life = d1.w;
                float k = age / life;
                o.uv = v.uvAge.xy;
                o.a = 0.0;
                o.pos = float4(0.0, 0.0, -2.0, 1.0);
                if (k < 0.0 || k >= 1.0) return o;

                float3 h, t;
                float w = d2.w, alpha = d3.w;
                if (d3.z < 0.0)
                {
                    h = Bezier(d0.xyz, d1.xyz, d2.xyz, pow(k, 2.2));
                    t = Bezier(d0.xyz, d1.xyz, d2.xyz, pow(max(0.0, k - d3.x), 2.2));
                    alpha *= min(1.0, k * 5.0);
                    float f = 1.0 - (T - _FadeAt) / _FadeLen;
                    if (f <= 0.0) return o;
                    alpha *= saturate(f);
                }
                else
                {
                    float D = d3.y, G = d3.z;
                    float e = exp(-D * age);
                    float3 g = float3(0.0, G / D, 0.0);
                    float3 vel = (d1.xyz + g) * e - g;
                    h = d0.xyz + (d1.xyz + g) * (1.0 - e) / D - g * age;
                    t = h - vel * d3.x;
                    alpha *= 1.0 - k * k;
                    w *= 1.0 - 0.6 * k;
                }
                float3 forward = normalize(v.tangent.xyz);
                float3 up = float3(0.0, 1.0, 0.0);
                float3 right = normalize(cross(up, forward));
                h = center + right * h.x + up * h.y + forward * h.z;
                t = center + right * t.x + up * t.y + forward * t.z;
                float3 d = h - t;
                if (dot(d, d) < 1e-8) d = float3(0.0, 1e-4, 0.0);
                float3 mid = lerp(t, h, o.uv.y);
                float3 side = normalize(cross(d, _WorldSpaceCameraPos - mid));
                float3 p = mid + side * (o.uv.x - 0.5) * w * lerp(0.3, 1.0, o.uv.y);
                o.a = alpha;
                o.pos = UnityWorldToClipPos(p);
                return o;
            }

            float4 frag(Vary i) : SV_Target
            {
                float a = max(0.0, 1.0 - abs(i.uv.x * 2.0 - 1.0));
                float3 c = lerp(_Red.rgb, _Hot.rgb, pow(a, 3.0) * i.uv.y);

                c *= pow(a, 1.4) * smoothstep(0.0, 0.7, i.uv.y) * i.a;
                return float4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
