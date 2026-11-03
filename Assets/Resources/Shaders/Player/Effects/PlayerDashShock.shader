Shader "Espoir/Player/Effects/DashShock"
{
    Properties
    {
        _Lifetime ("수명 (초)", Float) = 0.3
        _Strength ("세기 (화면 UV)", Float) = 0.035
        [Header(Second Ring)]
        _Second ("같이 터지는 충격파: 위치 차이 (xyz), 크기 (w, 0 = 없음)", Vector) = (0, 0, 0, 0)
        _SecondAge ("같이 터지는 충격파: 수명 비율 (자기 수명 / 둘째 수명)", Float) = 1
        _SecondStrength ("같이 터지는 충격파: 세기", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+50" "RenderType" = "Transparent"
            "IgnoreProjector" = "True" "EspoirDistort" = "On"
        }
        Pass
        {
            ColorMask 0 ZWrite Off ZTest Never Cull Off
        }
    }
    Fallback Off
}
