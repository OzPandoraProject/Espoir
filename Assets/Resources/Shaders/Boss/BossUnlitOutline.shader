Shader "Espoir/BossUnlitOutline"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Outline)]
        _OutlineWidth ("Width (px)", Range(0, 5)) = 0.8
        _OutlineColor ("Color", Color) = (0.078, 0.047, 0.094, 1)
        _OutlineTex ("Use Texture Color", Range(0, 1)) = 1
        _OutlineDark ("Texture Color Darkness", Range(0, 1)) = 0.35
        _OutlineZ ("Depth Offset", Float) = 0.0004

        [Header(Ground Contact)]
        _GroundY ("Ground World Y", Float) = 0
        _GroundBand ("Ground Band Height (m)", Float) = 0.5
        _GroundLift ("Lift Near Ground (m)", Float) = 0.03
        _GroundPull ("Pull Toward Camera Near Ground (m)", Float) = 0.15
        _ContactLine ("Contact Line (px)", Range(0, 4)) = 1
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    float4 _Color;
    float4 _OutlineColor;
    float _OutlineWidth;
    float _OutlineTex;
    float _OutlineDark;
    float _OutlineZ;
    float _GroundY;
    float _GroundBand;
    float _GroundLift;
    float _GroundPull;
    float _ContactLine;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float height : TEXCOORD1;
        float4 screenPos : TEXCOORD2;
    };

    float3 GetOutlineColor(float3 albedo)
    {
        float3 textureOutline = pow(max(albedo, 0), 1.4) * _OutlineDark;
        return lerp(_OutlineColor.rgb, textureOutline, _OutlineTex);
    }

    v2f VertBody(appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        o.height = mul(unity_ObjectToWorld, v.vertex).y - _GroundY;
        o.screenPos = ComputeScreenPos(o.pos);
        return o;
    }

    v2f VertOutline(appdata v)
    {
        v2f o;
        float3 worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
        float height = worldPosition.y - _GroundY;
        float groundWeight = 1 - saturate(height / max(_GroundBand, 0.001));
        worldPosition.y += _GroundLift * groundWeight;

        // 바닥 근처 외곽선은 카메라 쪽으로 미터 단위만큼 당겨서 바닥에 묻히지 않게 함
        float3 viewPosition = mul(UNITY_MATRIX_V, float4(worldPosition, 1)).xyz;
        viewPosition.z += _GroundPull * groundWeight;
        float4 clipPosition = mul(UNITY_MATRIX_P, float4(viewPosition, 1));
        float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
        float2 direction = mul((float2x2)UNITY_MATRIX_P, viewNormal.xy);
        float directionLength = length(direction);
        direction = directionLength > 0.00001 ? direction / directionLength : 0;
        float distanceScale = clamp(2.6 / clipPosition.w, 0.75, 1);
        clipPosition.xy += direction * _OutlineWidth * distanceScale *
            2 / _ScreenParams.xy * clipPosition.w;

        float depthOffset = _OutlineZ * (1 - groundWeight);
        #if defined(UNITY_REVERSED_Z)
            clipPosition.z -= depthOffset * clipPosition.w;
        #else
            clipPosition.z += depthOffset * clipPosition.w;
        #endif

        o.pos = clipPosition;
        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        o.height = height;
        o.screenPos = ComputeScreenPos(o.pos);
        return o;
    }

    float4 FragBody(v2f i) : SV_Target
    {
        float3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;

        if (_ContactLine > 0)
        {
            float pixelHeight = max(fwidth(i.height), 0.00001);
            float contact = 1 - smoothstep(pixelHeight * _ContactLine,
                pixelHeight * (_ContactLine + 1), i.height);
            albedo = lerp(albedo, GetOutlineColor(albedo), contact);
        }

        return float4(albedo, 1);
    }

    float4 FragOutline(v2f i) : SV_Target
    {
        clip(_OutlineWidth - 0.01);
        float3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
        return float4(GetOutlineColor(albedo), 1);
    }

    float EyeDepth(float depth)
    {
        if (unity_OrthoParams.w > 0.5)
        {
            #if defined(UNITY_REVERSED_Z)
                depth = 1 - depth;
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
        clip(EyeDepth(sceneDepth) - eyeDepth + max(1, eyeDepth) * DEPTH_EPSILON);
        return float4(1, 1, 1, 1);
    }

    float4 FragOutlineMask(v2f i) : SV_Target
    {
        clip(_OutlineWidth - 0.01);
        return FragMask(i);
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "UNLIT"
            Tags { "LightMode" = "ForwardBase" }
            Cull Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex VertBody
            #pragma fragment FragBody
            ENDCG
        }

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode" = "Always" }
            Cull Front

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex VertOutline
            #pragma fragment FragOutline
            ENDCG
        }

        // 화면 AO가 적용된 뒤 CEnvironmentGradeMask 가 이 패스로 본체를 원래 색으로 다시 그림 (보스는 AO 제외)
        Pass
        {
            Name "AO_EXCLUDE"
            Tags { "LightMode" = "AOExclude" }
            Cull Off
            ZWrite Off
            ZTest Equal

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex VertBody
            #pragma fragment FragBody
            ENDCG
        }

        Pass
        {
            Name "ENVIRONMENT_MASK"
            Tags { "LightMode" = "EnvironmentMask" }
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask R

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex VertBody
            #pragma fragment FragMask
            ENDCG
        }

        Pass
        {
            Name "ENVIRONMENT_OUTLINE_MASK"
            Tags { "LightMode" = "EnvironmentMask" }
            Cull Front
            ZWrite Off
            ZTest Always
            ColorMask R

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex VertOutline
            #pragma fragment FragOutlineMask
            ENDCG
        }

        Pass
        {
            Name "SHADOWCASTER"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex VertShadow
            #pragma fragment FragShadow
            #pragma multi_compile_shadowcaster

            struct v2fShadow
            {
                V2F_SHADOW_CASTER;
            };

            v2fShadow VertShadow(appdata_base v)
            {
                v2fShadow o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 FragShadow(v2fShadow i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback Off
}
