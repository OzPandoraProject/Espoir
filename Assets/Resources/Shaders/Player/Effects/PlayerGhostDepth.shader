Shader "Hidden/Espoir/Player/Effects/GhostDepth"
{
    Properties
    {
        [HideInInspector] _GhostGroundY ("Ground Y", Float) = 0
        [NoScaleOffset] _Noise ("Noise (RGBA 데이터)", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-3" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            ColorMask 0 ZWrite On Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "PlayerGhost.cginc"
            float4 frag(Vary i) : SV_Target
            {
                if (Shade(i).a < 0.3) discard;
                return 0;
            }
            ENDCG
        }
    }
    Fallback Off
}
