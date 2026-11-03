Shader "Hidden/Espoir/Player/Effects/Distortion"
{
    SubShader
    {
        Tags { "EspoirDistort" = "On" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            float _Lifetime, _Strength, _SecondAge, _SecondStrength;
            float4 _Second;

            struct Appdata { float4 vertex : POSITION; float4 uvAge : TEXCOORD0; float3 center : TEXCOORD1; };
            struct Vary
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float t : TEXCOORD1;
                float4 clip : TEXCOORD2;
                float4 second : TEXCOORD3;
                float fit : TEXCOORD4;
            };

            Vary vert(Appdata v)
            {
                Vary o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uvAge.xy * 2.0 - 1.0;
                o.t = v.uvAge.z;
                o.clip = o.pos;

                o.fit = saturate(abs(UNITY_MATRIX_P[1][1]) / UnityWorldToClipPos(v.center).w * 5.74);

                float4 c = UnityWorldToClipPos(v.center + _Second.xyz);
                o.second = float4(c.xy / c.w,
                    0.5 * _Second.w * float2(UNITY_MATRIX_P[0][0], UNITY_MATRIX_P[1][1]) / c.w);
                return o;
            }

            float2 Ring(float2 q, float t, float strength)
            {
                float r = length(q);
                float k = 1.0 - t;
                float R = lerp(0.08, 0.95, 1.0 - pow(k, 2.5));
                float w = lerp(0.22, 0.06, t);
                float d = (r - R) / w;
                float prof = exp(-d * d);
                float2 dir = r > 0.0001 ? q / r : 0;
                return dir * prof * strength * k;
            }

            float4 frag(Vary i) : SV_Target
            {
                float2 offset = Ring(i.uv, i.t, _Strength) * i.fit;
                if (_Second.w > 0.0 && i.t * _SecondAge < 1.0)
                {
                    float2 q2 = (i.clip.xy / i.clip.w - i.second.xy) / i.second.zw;

                    q2.y *= sign(ddx(q2.y) * ddx(i.uv.y) + ddy(q2.y) * ddy(i.uv.y));
                    offset += Ring(q2, i.t * _SecondAge, _SecondStrength) * i.fit;
                }

                float2 screen = i.clip.xy * float2(1.0, _ProjectionParams.x) / i.clip.w;
                float2 du = ddx(i.uv), dv = ddy(i.uv), dsx = ddx(screen), dsy = ddy(screen);
                offset.x *= sign(du.x * dsx.x + dv.x * dsy.x);
                offset.y *= sign(du.y * dsx.y + dv.y * dsy.y);
                return float4(offset, 0.0, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
