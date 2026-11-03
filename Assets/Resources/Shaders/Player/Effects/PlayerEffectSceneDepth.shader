Shader "Hidden/Espoir/Player/Effects/SceneDepth"
{
    SubShader
    {
        Pass
        {
            Cull Off ZTest Always ZWrite On ColorMask 0
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            UNITY_DECLARE_DEPTH_TEXTURE(_SceneDepth);
            float frag(v2f_img i) : SV_Depth
            {
                return SAMPLE_DEPTH_TEXTURE(_SceneDepth, i.uv);
            }
            ENDCG
        }
    }
}
