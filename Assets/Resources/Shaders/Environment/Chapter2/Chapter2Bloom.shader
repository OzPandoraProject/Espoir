Shader "Hidden/Espoir/Chapter2Bloom"
{
    HLSLINCLUDE
    #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

    TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
    TEXTURE2D_SAMPLER2D(_Chapter2BloomMip0, sampler_Chapter2BloomMip0);
    TEXTURE2D_SAMPLER2D(_Chapter2BloomMip1, sampler_Chapter2BloomMip1);
    TEXTURE2D_SAMPLER2D(_Chapter2BloomMip2, sampler_Chapter2BloomMip2);
    TEXTURE2D_SAMPLER2D(_Chapter2BloomMip3, sampler_Chapter2BloomMip3);
    TEXTURE2D_SAMPLER2D(_Chapter2BloomMip4, sampler_Chapter2BloomMip4);
    TEXTURE2D_SAMPLER2D(_Chapter2BloomTex, sampler_Chapter2BloomTex);
    float4 _Threshold;   // x 시작 밝기, y 부드러운 폭
    float4 _Blur;        // xy 한 칸 방향(텍셀), z 커널 반경
    float4 _Factors;
    float _Weights[11];
    TEXTURE2D_SAMPLER2D(_Chapter2BloomCharacterMask, sampler_Chapter2BloomCharacterMask);
    float _CharacterAmount;
    float _FactorLast;
    float _AddScale;     // 가산 합성 배율

    float4 FragBright(VaryingsDefault i) : SV_Target
    {
        float3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord).rgb;
        float v = dot(c, float3(0.2126, 0.7152, 0.0722));
        float a = smoothstep(_Threshold.x, _Threshold.x + _Threshold.y, v);
        return float4(c * a, 1.0);
    }

    float4 FragBlur(VaryingsDefault i) : SV_Target
    {
        int radius = (int)_Blur.z;
        float3 sum = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord).rgb;
        float weight = 1.0;
        [loop]
        for (int k = 1; k < radius; k++)
        {
            float w = _Weights[k];
            float2 o = _Blur.xy * k;
            sum += (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord + o).rgb
                  + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord - o).rgb) * w;
            weight += 2.0 * w;
        }
        return float4(sum / weight, 1.0);
    }

    float4 FragComposite(VaryingsDefault i) : SV_Target
    {
        float2 uv = i.texcoord;
        float3 c = SAMPLE_TEXTURE2D(_Chapter2BloomMip0, sampler_Chapter2BloomMip0, uv).rgb * _Factors.x
                 + SAMPLE_TEXTURE2D(_Chapter2BloomMip1, sampler_Chapter2BloomMip1, uv).rgb * _Factors.y
                 + SAMPLE_TEXTURE2D(_Chapter2BloomMip2, sampler_Chapter2BloomMip2, uv).rgb * _Factors.z
                 + SAMPLE_TEXTURE2D(_Chapter2BloomMip3, sampler_Chapter2BloomMip3, uv).rgb * _Factors.w
                 + SAMPLE_TEXTURE2D(_Chapter2BloomMip4, sampler_Chapter2BloomMip4, uv).rgb * _FactorLast;
        return float4(c, 1.0);
    }

    // 원본 화면에 더하기
    float4 FragAdd(VaryingsDefault i) : SV_Target
    {
        float4 src = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
        float3 bloom = SAMPLE_TEXTURE2D(_Chapter2BloomTex, sampler_Chapter2BloomTex, i.texcoord).rgb;
        return float4(src.rgb + bloom * _AddScale, src.a);
    }
    float4 FragAddMasked(VaryingsDefault i) : SV_Target
    {
        float4 src = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
        float3 bloom = SAMPLE_TEXTURE2D(_Chapter2BloomTex, sampler_Chapter2BloomTex, i.texcoord).rgb;
        float mask = SAMPLE_TEXTURE2D(_Chapter2BloomCharacterMask, sampler_Chapter2BloomCharacterMask, i.texcoord).r;
        return float4(src.rgb + bloom * _AddScale * lerp(1.0, _CharacterAmount, saturate(mask)), src.a);
    }
    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex VertDefault
            #pragma fragment FragBright
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex VertDefault
            #pragma fragment FragBlur
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex VertDefault
            #pragma fragment FragComposite
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex VertDefault
            #pragma fragment FragAdd
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex VertDefault
            #pragma fragment FragAddMasked
            ENDHLSL
        }
    }
}
