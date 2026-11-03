Shader "Espoir/Player/Effects/Ghost"
{
    Properties
    {
        [HideInInspector] _GhostGroundY ("Ground Y", Float) = 0
        [NoScaleOffset] _Noise ("Noise (RGBA 데이터)", 2D) = "gray" {}
        _C0 ("Ramp 0 (Linear HDR)", Vector) = (0.03, 0, 0.01, 0)
        _C1 ("Ramp 1", Vector) = (0.3, 0, 0.03, 0)
        _C2 ("Ramp 2", Vector) = (2, 0.04, 0.06, 0)
        _C3 ("Ramp 3", Vector) = (4.5, 1.6, 1.2, 0)
    }
    CGINCLUDE
    #include "PlayerGhost.cginc"
    ENDCG
    SubShader
    {
        Tags { "Queue" = "Transparent-2" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            ZWrite Off Cull Back Blend SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            float4 frag(Vary i) : SV_Target
            {
                float4 c = Shade(i);
                if (c.a < 0.01) discard;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
