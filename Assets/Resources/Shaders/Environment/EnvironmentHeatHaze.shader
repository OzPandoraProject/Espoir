Shader "Hidden/Espoir/EnvironmentHeatHaze"
{
    HLSLINCLUDE
    #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
    #include "EnvironmentHeatHaze.hlsl"

    TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);

    float4 Frag(VaryingsDefault i) : SV_Target
    {
        float3 haze = HeatHazeSample(i.texcoord);
        float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, haze.xy);
        color.rgb *= haze.z;
        return color;
    }
    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex VertDefault
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
