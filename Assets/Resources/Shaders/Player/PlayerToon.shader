Shader "Espoir/PlayerToon"
{
    Properties
    {
        [Header(Base)]
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Base Color", Color) = (1,1,1,1)
        [Enum(Skin,0,Face,1,Hair,2,Cloth,3)] _Part ("Part", Float) = 3
        _Ramp ("Ramp", 2D) = "white" {}
        _AlbedoGamma ("Albedo Gamma", Range(0.5, 1.6)) = 1
        _AlbedoSat ("Albedo Saturation", Range(0, 2)) = 1

        [Header(Lighting)]
        _LightTint ("Light Color", Color) = (1, 0.972, 0.941, 1)
        [Toggle] _UseSceneLightColor ("Multiply Scene Light Color", Float) = 0
        _Ambient ("Ambient Color", Color) = (0.541, 0.565, 0.722, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 0.6)) = 0.06
        _CastShadow ("Receive Cast Shadow", Range(0, 1)) = 0
        _SoftNormal ("Smoothed Normal Amount", Range(0, 1)) = 0.85
        _AO ("Baked AO", Range(0, 1)) = 0.85
        _ClothHueKeep ("Cloth Keep Hue In Shadow", Range(0, 1)) = 0.75

        [Header(Face)]
        _FaceSDF ("Face SDF", Range(0, 1)) = 0
        _FaceSoft ("Face SDF Softness", Range(0.002, 0.3)) = 0.06
        _FaceCurve ("Face Cheek Bulge", Range(-1, 2)) = 0.5
        _FaceNormalSmooth ("Face Spherical Normal", Range(0, 1)) = 0.82
        _FaceCastShadow ("Face Receive Cast Shadow", Range(0, 1)) = 0.15
        _FaceBias ("Face Side Light Bias", Range(-0.3, 0.5)) = 0.1
        _NeckShadow ("Under Jaw / Neck Shadow", Range(0, 1)) = 1

        [Header(Hair Highlight)]
        _HairSpec ("Strength", Range(0, 2)) = 0
        _HairSpecPow ("Sharpness", Range(4, 200)) = 60
        _HairSpecThresh ("Cut", Range(0.05, 0.95)) = 0.55
        _HairSpecColor ("Color", Color) = (0.549, 0.486, 0.463, 1)
        _HairLuma ("Hair Mean Luma (linear)", Float) = 0.02

        [Header(Metal)]
        _Metal ("Amount", Range(0, 1)) = 0
        _MetalBand ("Band", Range(0.2, 0.9)) = 0.55
        _MetalSpec ("Highlight", Range(0, 3)) = 1
        _MetalTint ("Tint", Color) = (1, 0.902, 0.69, 1)
        _MetalSteel ("Silver Steel Also Metal (weapons)", Range(0, 1)) = 0
        _MetalSteelTint ("Steel Tint", Color) = (0.902, 0.929, 1, 1)

        [Header(Rim)]
        _Rim ("Strength", Range(0, 2)) = 0
        _RimWidth ("Width", Range(0.2, 0.98)) = 0.62
        _RimSoft ("Softness", Range(0.002, 0.4)) = 0.04
        _RimColor ("Color", Color) = (1,1,1,1)

        [Header(Outline)]
        _OutlineWidth ("Width (px)", Range(0, 5)) = 1
        _OutlineColor ("Color", Color) = (0.078, 0.047, 0.094, 1)
        _OutlineTex ("Use Texture Color", Range(0, 1)) = 1
        _OutlineDark ("Texture Color Darkness", Range(0, 1)) = 0.32
        _OutlineZ ("Depth Offset", Float) = 0.0004
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex; float4 _MainTex_ST;
    sampler2D _Ramp;
    float4 _Color;
    float _Part, _AlbedoGamma, _AlbedoSat;
    float4 _LightTint, _Ambient;
    float _UseSceneLightColor, _AmbientStrength, _CastShadow, _SoftNormal, _AO, _ClothHueKeep;
    float _FaceSDF, _FaceSoft, _FaceCurve, _FaceNormalSmooth, _FaceCastShadow, _FaceBias, _NeckShadow;
    float _HairSpec, _HairSpecPow, _HairSpecThresh, _HairLuma; float4 _HairSpecColor;
    float _Metal, _MetalBand, _MetalSpec, _MetalSteel; float4 _MetalTint, _MetalSteelTint;
    float _Rim, _RimWidth, _RimSoft; float4 _RimColor;
    float _OutlineWidth, _OutlineTex, _OutlineDark, _OutlineZ; float4 _OutlineColor;

    // CToonHeadTracker 가 매 프레임 설정
    float4 _ToonHeadCenter, _ToonHeadUp, _ToonHeadForward;
    float _ToonHeadRadius;

    float Luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }
    ENDCG

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma target 3.0
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;   // xyz = 다듬은 노멀
                float4 color : COLOR;       // r = AO
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 softNormal : TEXCOORD3;
                float3 viewNormal : TEXCOORD4;
                float ao : TEXCOORD5;
                SHADOW_COORDS(6)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.softNormal = UnityObjectToWorldNormal(v.tangent.xyz);
                o.viewNormal = mul((float3x3)UNITY_MATRIX_V, o.worldNormal);
                o.ao = v.color.r;
                TRANSFER_SHADOW(o);
                return o;
            }

            // 텍스처에서 금색 장식 영역 추출 (sRGB 기준 HSV 판정)
            float GoldMask(float3 lin)
            {
                float3 c = pow(max(lin, 0), 1.0 / 2.2);
                float mx = max(max(c.r, c.g), c.b);
                float mn = min(min(c.r, c.g), c.b);
                float sat = (mx - mn) / max(mx, 1e-4);
                float hue = (c.g - c.b) / max(mx - mn, 1e-4);
                float redMax = step(c.g, c.r + 0.04);
                return smoothstep(0.35, 0.55, sat) * smoothstep(0.30, 0.50, mx) * smoothstep(0.18, 0.38, hue) * redMax;
            }

            // 채도가 낮고 밝은 부분을 칼날 영역으로 분류한다.
            float SteelMask(float3 lin)
            {
                float3 c = pow(max(lin, 0), 1.0 / 2.2);
                float mx = max(max(c.r, c.g), c.b);
                float mn = min(min(c.r, c.g), c.b);
                float sat = (mx - mn) / max(mx, 1e-4);
                return (1.0 - smoothstep(0.12, 0.22, sat)) * smoothstep(0.38, 0.55, mx);
            }

            float4 frag(v2f i, float facing : VFACE) : SV_Target
            {
                float3 rawAlbedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                float3 albedo = pow(max(rawAlbedo, 0), _AlbedoGamma);
                float la = Luma(albedo);
                albedo = max(lerp(la.xxx, albedo, _AlbedoSat), 0);

                int part = (int)(_Part + 0.5);
                float3 N = normalize(i.worldNormal);
                float3 VN = normalize(i.viewNormal);
                float3 SN = normalize(i.softNormal);
                if (facing < 0) { N = -N; VN = -VN; SN = -SN; }
                if (part != 1) N = normalize(lerp(N, SN, _SoftNormal));
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 up = _ToonHeadUp.xyz, fwd = _ToonHeadForward.xyz;
                float3 hp = i.worldPos - _ToonHeadCenter.xyz;
                float radius = max(_ToonHeadRadius, 1e-3);

                if (part == 1) N = normalize(lerp(N, normalize(hp + 1e-5), _FaceNormalSmooth));

                float NdotL = dot(N, L);
                float sh = SHADOW_ATTENUATION(i);
                float lf = NdotL * 0.5 + 0.5;

                if (part <= 1)
                {
                    float3 hpH = hp - up * dot(hp, up);
                    float rH = length(hpH);
                    float3 dirH = rH > 1e-5 ? hpH / rH : fwd;
                    float v = dot(hp, up) / radius;
                    float region = part == 1 ? 1.0
                        : (1.0 - smoothstep(1.3, 1.9, rH / radius)) * (1.0 - smoothstep(3.2, 4.2, -v));
                    float faceShadowMul = lerp(1.0, sh, _CastShadow * _FaceCastShadow);
                    if (part == 1) lf = (NdotL * 0.5 + 0.5) * faceShadowMul;

                    if (_FaceSDF > 0)
                    {
                        float3 Lh = L - up * dot(L, up);
                        float lhl = length(Lh);
                        Lh = lhl > 1e-4 ? Lh / lhl : fwd;
                        float facingL = dot(Lh, fwd);
                        float front = max(dot(dirH, fwd), 0.0);
                        float side = 1.0 - abs(facingL);
                        float cheek = exp(-pow((v + 0.45) / 0.32, 2.0));
                        float shape = 0.15 * cheek * _FaceCurve * side * front;
                        float bias = _FaceBias * side * front;
                        float d = dot(dirH, Lh) + shape + bias;
                        float aa = fwidth(d) * 1.2;
                        float faceLit = smoothstep(-_FaceSoft - aa, _FaceSoft + aa, d);
                        lf = lerp(lf, lerp(0.32, 0.88, faceLit) * faceShadowMul, region * _FaceSDF);
                    }

                    // 턱 아래와 목은 항상 그늘
                    float jawUnder = part == 1
                        ? smoothstep(-0.4, -0.7, dot(normalize(i.worldNormal), up)) * smoothstep(-0.85, -1.05, v)
                        : smoothstep(-0.95, -1.25, v) * region;
                    lf = lerp(lf, min(lf, 0.34), jawUnder * _NeckShadow);
                    if (part == 1 || jawUnder * _NeckShadow > 0.01) lf = max(lf, 0.3 * faceShadowMul);
                }

                // 드리우는 그림자는 램프 색 단계에서 섞음
                float castSh = part == 1 ? 1.0 : sh;
                float3 ramp = tex2D(_Ramp, float2(clamp(lf, 0.004, 0.996), 0.5)).rgb;
                float3 rampCast = tex2D(_Ramp, float2(clamp(lf * castSh, 0.004, 0.996), 0.5)).rgb;
                ramp = lerp(ramp, rampCast, _CastShadow);
                lf *= lerp(1.0, castSh, _CastShadow);

                // 미리 구운 차폐: 가려진 곳을 그림자색으로
                float occl = (1.0 - smoothstep(0.45, 0.85, i.ao)) * _AO * (part == 1 ? 0.35 : 1.0);
                float3 rampShadow = tex2D(_Ramp, float2(0.3, 0.5)).rgb;
                ramp = lerp(ramp, min(ramp, rampShadow), occl);
                lf = lerp(lf, min(lf, 0.32), occl);

                float3 H = normalize(L + V);
                float NdotH = max(dot(N, H), 0);
                float litMask = smoothstep(0.38, 0.6, lf);
                float gGold = part == 3 ? GoldMask(rawAlbedo) : 0;
                float gSteel = part == 3 && _MetalSteel > 0 ? SteelMask(rawAlbedo) * _MetalSteel : 0;
                float gm = max(gGold, gSteel);
                float3 metalTint = lerp(_MetalTint.rgb, _MetalSteelTint.rgb, gSteel / max(gGold + gSteel, 1e-4));

                float3 lightCol = _LightTint.rgb * lerp(1.0.xxx, _LightColor0.rgb, _UseSceneLightColor);
                float3 shaded = albedo * ramp;
                if (part == 3 && _ClothHueKeep > 0)
                {
                    float rl = Luma(ramp);
                    float satBoost = lerp(1.0, 1.25, saturate((1.0 - rl) * 2.0));
                    float3 keep = max(lerp(Luma(albedo).xxx, albedo, satBoost), 0) * rl;
                    shaded = lerp(shaded, keep, _ClothHueKeep);
                }
                float3 col = shaded * lightCol + albedo * _Ambient.rgb * _AmbientStrength;

                if (_Metal > 0 && gm > 0)
                {
                    float mc = VN.y * 0.5 + 0.5 + VN.x * 0.12;
                    float band = smoothstep(_MetalBand - 0.035, _MetalBand + 0.035, mc);
                    float spec = smoothstep(0.90, 0.93, NdotH);
                    float3 metalCol = albedo * metalTint * lerp(0.5, 1.3, band) * lerp(0.55, 1.0, litMask)
                                    + albedo * spec * _MetalSpec * 2.5;
                    col = lerp(col, metalCol, gm * _Metal);
                }

                if (part == 2 && _HairSpec > 0)
                {
                    float3 T = normalize(up - N * dot(N, up) + 1e-5);
                    float th = dot(T, H);
                    float kk = pow(sqrt(max(1.0 - th * th, 0.0)), _HairSpecPow);
                    float strand = smoothstep(0.85, 1.35, la / max(_HairLuma, 1e-4));
                    float ring = smoothstep(_HairSpecThresh, _HairSpecThresh + 0.04, kk) * strand * litMask;
                    float k = ring * _HairSpec;
                    col = lerp(col, _HairSpecColor.rgb * lightCol, saturate(k)) +
                        _HairSpecColor.rgb * max(k - 1.0, 0.0) * 0.5;
                }

                float fres = 1.0 - max(dot(N, V), 0);
                float rim = smoothstep(_RimWidth, _RimWidth + _RimSoft, fres) * (0.3 + 0.7 * litMask);
                col += rim * _RimColor.rgb * _Rim * lerp(1.0.xxx, albedo * 3.0, 0.4);

                return float4(col, 1);
            }
            ENDCG
        }

        // 외곽선: 다듬은 노멀(tangent) 방향으로 화면 픽셀 단위 두께만큼 밀어낸 뒷면
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float4 clip = UnityObjectToClipPos(v.vertex);
                float3 n = length(v.tangent.xyz) > 0.01 ? v.tangent.xyz : v.normal;
                float3 vn = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, n));
                float2 dir = mul((float2x2)UNITY_MATRIX_P, vn.xy);
                float dl = length(dir);
                dir = dl > 1e-5 ? dir / dl : 0;
                float distScale = clamp(2.6 / clip.w, 0.75, 1.0);
                clip.xy += dir * _OutlineWidth * distScale * 2.0 / _ScreenParams.xy * clip.w;
                #if defined(UNITY_REVERSED_Z)
                    clip.z -= _OutlineZ * clip.w;
                #else
                    clip.z += _OutlineZ * clip.w;
                #endif
                o.pos = _OutlineWidth > 0.01 ? clip : float4(0, 0, -2, 1);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 a = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                float3 texCol = pow(max(a, 0), 1.4) * _OutlineDark;
                return float4(lerp(_OutlineColor.rgb, texCol, _OutlineTex), 1);
            }
            ENDCG
        }

        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    Fallback Off
}
