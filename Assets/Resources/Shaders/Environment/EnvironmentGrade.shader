Shader "Hidden/Espoir/EnvironmentGrade"
{
    HLSLINCLUDE
    #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
    #include "EnvironmentHeatHaze.hlsl"

    TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
    TEXTURE2D_SAMPLER2D(_CharacterMask, sampler_CharacterMask);
    float4 _Grade;
    float3 _ShadowTone;
    float3 _HighlightTone;
    float3 _VignetteColor;
    float4 _Detail;

    float4 Frag(VaryingsDefault i) : SV_Target
    {
        float3 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord).rgb;
        float2 maskUV = HeatHazeSample(i.texcoord).xy;
        float mask = SAMPLE_TEXTURE2D(_CharacterMask, sampler_CharacterMask, maskUV).r;
        [branch]
        if (mask >= 1.0) return float4(source, 1.0);

        float3 col = source * _Grade.x;
        col /= 1.0 + max(col - 0.85, 0.0) * 0.9;
        float3 ungraded = col;
        float luminance = dot(col, float3(0.299, 0.587, 0.114));
        col = lerp(luminance.xxx, col, _Grade.y);
        col = (col - 0.5) * _Grade.z + 0.5;
        col += (_ShadowTone - 0.5) * (1.0 - smoothstep(0.0, 0.55, luminance)) * _Grade.w;
        col += (_HighlightTone - 0.5) * smoothstep(0.45, 1.0, luminance) * _Grade.w;
        float protection = _Detail.w > 0.0 ? smoothstep(0.0, _Detail.w, luminance) : 1.0;
        col = lerp(ungraded, col, protection);
        float vignette = smoothstep(0.35, 0.85, length((i.texcoord - 0.5) * float2(1.15, 1.0)));
        col = lerp(col, col * _VignetteColor * 1.8, vignette * _Detail.x);
        float grain = frac(sin(dot(i.texcoord * 1024.0 + frac(_Detail.z), float2(12.9898, 78.233))) * 43758.5453);
        col += (grain - 0.5) * _Detail.y;
        return float4(lerp(max(col, 0.0), source, mask), 1.0);
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
