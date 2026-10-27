Shader "Hidden/Espoir/PlayerPostMask"
{
    CGINCLUDE
    #include "UnityCG.cginc"

    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    float _PlayerMaskOutlineWidth;
    float _PlayerMaskOutlineZ;

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float4 tangent : TANGENT;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float4 screenPos : TEXCOORD0;
    };

    v2f VertBody(appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.screenPos = ComputeScreenPos(o.pos);
        return o;
    }

    v2f VertOutline(appdata v)
    {
        v2f o;
        float4 clipPosition = UnityObjectToClipPos(v.vertex);
        float3 outlineNormal = length(v.tangent.xyz) > 0.01 ? v.tangent.xyz : v.normal;
        float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, outlineNormal));
        float2 direction = mul((float2x2)UNITY_MATRIX_P, viewNormal.xy);
        float directionLength = length(direction);
        direction = directionLength > 1e-5 ? direction / directionLength : 0;
        float distanceScale = clamp(2.6 / clipPosition.w, 0.75, 1.0);
        clipPosition.xy += direction * _PlayerMaskOutlineWidth * distanceScale *
            2.0 / _ScreenParams.xy * clipPosition.w;
        #if defined(UNITY_REVERSED_Z)
            clipPosition.z -= _PlayerMaskOutlineZ * clipPosition.w;
        #else
            clipPosition.z += _PlayerMaskOutlineZ * clipPosition.w;
        #endif
        o.pos = clipPosition;
        o.screenPos = ComputeScreenPos(o.pos);
        return o;
    }

    float EyeDepth(float depth)
    {
        if (unity_OrthoParams.w > 0.5)
        {
            #if defined(UNITY_REVERSED_Z)
                depth = 1.0 - depth;
            #endif
            return lerp(_ProjectionParams.y, _ProjectionParams.z, depth);
        }

        return LinearEyeDepth(depth);
    }

    float4 FragMask(v2f i) : SV_Target
    {
        float sceneDepth = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
        float eyeDepth = EyeDepth(i.pos.z);
     
        const float DEPTH_EPSILON = 0.0001;
        clip(EyeDepth(sceneDepth) - eyeDepth + max(1.0, eyeDepth) * DEPTH_EPSILON);
        return float4(1, 1, 1, 1);
    }
    ENDCG

    SubShader
    {
        ZWrite Off
        ZTest Always
        Blend Off
        ColorMask R

        Pass
        {
            Name "BODY"
            Cull Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex VertBody
            #pragma fragment FragMask
            ENDCG
        }

        Pass
        {
            Name "OUTLINE"
            Cull Front

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex VertOutline
            #pragma fragment FragMask
            ENDCG
        }
    }

    Fallback Off
}
