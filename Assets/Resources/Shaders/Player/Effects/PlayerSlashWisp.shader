Shader "Espoir/Player/Effects/SlashWisp"
{
    Properties
    {
        [NoScaleOffset] _Noise ("Noise (RGBA 데이터)", 2D) = "gray" {}
        [NoScaleOffset] _Times ("칼끝 시간표", 2D) = "black" {}
        [Toggle(_TIMED)] _HasTimes ("칼끝 시간표 사용", Float) = 0
        _Color ("Color (Linear HDR)", Vector) = (0.9, 0.02, 0.06, 0)
        _Edge ("Edge (Linear HDR)", Vector) = (1.4, 0.12, 0.1, 0)
        [Header(Timing)]
        _Lifetime ("파티클 수명 (초)", Float) = 0.59
        _Duration ("그려지는 시간", Float) = 0.1
        _Linger ("남는 시간", Float) = 0.34
        _HoldTail ("꼬리 유지", Float) = 0
        _TailStart ("꼬리 시작 비율", Float) = 0.3
        [Header(Shape)]
        _Expand ("퍼짐", Float) = 0.18
        _SoftEnds ("붓끝처럼 끝 풀기", Float) = 0
        _Alpha ("불투명도", Float) = 0.85
        _FloorOffset ("바닥 높이 (파티클 중심 기준)", Float) = -1.03
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma shader_feature_local_vertex _TIMED
            #pragma shader_feature_local_vertex _FACE
            #include "PlayerRibbon.cginc"
            #include "PlayerEffectRamp.cginc"

            float4 _Color, _Edge;
            float _SoftEnds;

            struct V2F
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 state : TEXCOORD1;
                float seed : TEXCOORD2;
            };

            V2F vert(FxAppdata v)
            {
                V2F o;
                float age = FxAge(v), D = _Duration;
                float wd = FxSaturate((age - _HoldTail - D * 0.6) / (_Linger + 0.12));
                o.state.x = FxHeadAt(FxSaturate((age - 0.02) / D));
                o.state.y = FxTail(age) * 0.7;
                o.state.z = wd <= 0.0 ? -0.2 : wd;
                o.state.w = FxTime(v, age);
                o.seed = FxSeed(v) + 3.7;
                o.uv = v.uvAge.xy;
                o.pos = UnityWorldToClipPos(FxWorldPos(v,
                    _Expand * 1.6 * FxEaseOut(FxSaturate(age / (D + _Linger + 0.12)))));
                return o;
            }

            float4 frag(V2F i) : SV_Target
            {
                float s = i.uv.x, y = i.uv.y;
                float uHead = i.state.x, uTail = i.state.y, uDissolve = i.state.z, uTime = i.state.w, uSeed = i.seed;
                float lag = uHead - s;
                if (lag < 0.0) discard;
                float life = saturate(lag / max(uHead - uTail, 0.001));
                float2 warp = tex2D(_Noise, float2(s * 2.0 + uTime * 0.25 + uSeed, y * 0.8 - uTime * 0.2)).rg - 0.5;
                float2 uv = float2(s * 3.2 + uTime * 0.9 + uSeed, y * 1.1) + warp * float2(0.18, 0.4);
                float f = tex2D(_Noise, uv).r * 0.65 + tex2D(_Noise, uv * 2.1).g * 0.35;
                float shape = f - (y * 0.85 + 0.05) - life * 0.25;
                float m = smoothstep(0.0, 0.04, shape + 0.3);
                float d = tex2D(_Noise, uv * 0.7 + 0.37).a;
                float vis = smoothstep(uDissolve, uDissolve + 0.02, d);
                float edge = vis * (1.0 - smoothstep(uDissolve + 0.02, uDissolve + 0.22, d));
                float tailCut = smoothstep(uTail, uTail + 0.15, s);
                if (_SoftEnds > 0.0)
                {
                    m *= 1.0 - smoothstep(0.6, 0.95, y + (f - 0.5) * 0.3);
                    tailCut *= smoothstep(0.0, 0.3, s + (f - 0.5) * 0.3)
                        * (1.0 - smoothstep(0.8, 1.0, s + (f - 0.5) * 0.3));
                }
                float core = pow(saturate(f), 0.63);
                float3 col = core * _Color.rgb * 1.17 + edge * pow(m, 1.41) * _Edge.rgb * 3.92;
                float a = m * vis * tailCut * smoothstep(0.0, 0.05, lag) * _Alpha;
                if (a < 0.01) discard;

                return float4(col, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
