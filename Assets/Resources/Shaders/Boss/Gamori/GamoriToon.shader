Shader "Espoir/Boss/GamoriToon"
{
    Properties
    {
        _MainTex ("Albedo (map)", 2D) = "white" {}
        _MeanTex ("Mean Map (meanMap, linear)", 2D) = "white" {}
        _HasMean ("Has Mean (uHasMean)", Float) = 0
        _GlossTex ("Gloss Mask (glossMap, linear)", 2D) = "black" {}
        _HasGloss ("Has Gloss (uHasGloss)", Float) = 0
        _ShadeTex ("Shade Map (shadeMap)", 2D) = "white" {}
        _HasShade ("Has Shade (uHasShade)", Float) = 0
        _BaseColor ("Base Color (uBaseColor)", Color) = (1, 1, 1, 1)
        [Enum(Skin,0,Face,1,Hair,2,Cloth,3)] _Part ("Part (uPart)", Float) = 3
        _NoSkin ("No Skin Pick (uNoSkin)", Float) = 0
        _Steel ("Steel (uSteel)", Float) = 0
        _NoGold ("No Gold (uNoGold)", Float) = 0
        _GoldAny ("Gold Any (uGoldAny)", Float) = 0

        [Header(Ramps)]
        [NoScaleOffset] _RampSkin ("Ramp Skin", 2D) = "white" {}
        [NoScaleOffset] _RampCloth ("Ramp Cloth", 2D) = "white" {}
        [NoScaleOffset] _RampHair ("Ramp Hair", 2D) = "white" {}

        [Header(Light)]
        _LightColor ("Light Color (uLightColor)", Color) = (1, 1, 1, 1)
        _Ambient ("Ambient (uAmbient)", Color) = (0.541, 0.565, 0.722, 1)
        _AmbientStrength ("Ambient Strength", Float) = 0.06
        _CastShadow ("Cast Shadow (uCastShadow)", Float) = 1
        _AlbedoGamma ("Albedo Gamma", Float) = 1
        _AlbedoSat ("Albedo Sat", Float) = 1
        _AlbedoFlat ("Albedo Flat", Float) = 0
        _TexShade ("Tex Shade", Float) = 0
        _GlossCut ("Gloss Cut (amount, ceil)", Vector) = (0, 0.08, 0, 0)
        _SoftNormal ("Soft Normal", Float) = 0
        _SoftNormalSkin ("Soft Normal Skin", Float) = -1
        _AO ("AO", Float) = 0
        _ClothHueKeep ("Cloth Hue Keep", Float) = 0
        _HairHueKeep ("Hair Hue Keep", Float) = 0
        _ShadeAmt ("Shade Amt", Float) = 0
        _ShadeHue ("Shade Hue", Float) = 0

        [Header(Face)]
        _HasFace ("Has Face", Float) = 1
        _FaceSDF ("Face SDF", Float) = 0
        _FaceSoft ("Face Soft", Float) = 0.05
        _FaceCurve ("Face Curve", Float) = 0.5
        _FaceNormalSmooth ("Face Normal Smooth", Float) = 0
        _FaceSoftNrm ("Face Soft Nrm", Float) = 0
        _FaceCastShadow ("Face Cast Shadow", Float) = 0
        _FaceBias ("Face Bias", Float) = 0
        _NeckShadow ("Neck Shadow", Float) = 0
        _NeckFull ("Neck Full", Float) = 0
        _NeckTint ("Neck Tint", Float) = 0
        _NeckShadeAmt ("Neck Shade Amt", Float) = 1
        _FaceWrap ("Face Wrap", Float) = 0
        _FaceEdge ("Face Edge", Float) = 0
        _FaceCenterLit ("Face Center Lit", Float) = 0
        _FaceBend ("Face Bend", Float) = 1
        _FaceBow ("Face Bow", Float) = 0
        _FaceFlat ("Face Flat", Float) = 0
        _FaceShade ("Face Shade", Float) = 1
        _FaceSdf2 ("Face Sdf2 (amount, halfW, centerY, radiusY)", Vector) = (0, 0.74, -0.75, 0.9)
        _FaceSdf2Curve ("Face Sdf2 Curve", Vector) = (0, -0.2, 0.3, 0)
        _FaceSdf2Soft ("Face Sdf2 Soft", Float) = 0
        _FaceSdf2Back ("Face Sdf2 Back", Float) = 1
        _FaceSdf2Shift ("Face Sdf2 Shift", Float) = 0
        [NoScaleOffset] _FaceSdfTex ("Face Sdf Map (linear)", 2D) = "black" {}
        _FaceSdfMapOn ("Face Sdf Map On", Float) = 0
        _FaceSdfRect ("Face Sdf Rect", Vector) = (1.25, -2.2, 1.3, 0)
        _FaceTexFlat ("Face Tex Flat (amount, range, min)", Vector) = (0, 0.12, 0.55, 0)
        _FaceTexFlatCol ("Face Tex Flat Col (sRGB numbers)", Vector) = (0.94, 0.78, 0.75, 0)
        _FaceTexFlatRef ("Face Tex Flat Ref (sRGB numbers)", Vector) = (0.94, 0.78, 0.75, 0)

        [Header(Skin)]
        _SkinPick ("Skin Pick", Float) = 0
        _SkinPickWide ("Skin Pick Wide", Float) = 0
        _SkinPickSet ("Skin Pick Set", Vector) = (-1, 0.5, 0.18, 1)
        _SkinRef ("Skin Ref (sRGB numbers, measured)", Vector) = (0.99, 0.82, 0.78, 0)
        _SkinTone ("Skin Tone (warm, edge, blush, blushY)", Vector) = (0, 0, 0, -1)
        _SkinToneCol ("Skin Tone Color", Color) = (0.95, 0.72, 0.74, 1)

        [Header(Eye)]
        _EyePos ("Eye Pos", Vector) = (0.36, -0.89, 0.63, 0)
        _EyeFx ("Eye Fx", Vector) = (0, 0, 0, 0)
        _EyeHlSize ("Eye Hl Size", Float) = 0.26
        _EyeUvU ("Eye Uv U", Float) = 0

        [Header(Metal)]
        _Metal ("Metal", Float) = 0
        _MetalBand ("Metal Band", Float) = 0.55
        _MetalSpec ("Metal Spec", Float) = 1
        _MetalTint ("Metal Tint", Color) = (1, 1, 1, 1)
        _MetalSteelTint ("Metal Steel Tint", Color) = (0.9, 0.93, 1, 1)
        _MetalSatMin ("Metal Sat Min", Float) = 0.35

        [Header(Hair)]
        _HairSpec ("Hair Spec", Float) = 0
        _HairSpecPow ("Hair Spec Pow", Float) = 40
        _HairSpecThresh ("Hair Spec Thresh", Float) = 0.5
        _HairStrand ("Hair Strand (lo, hi)", Vector) = (0.85, 1.35, 0, 0)
        _HairSpecColor ("Hair Spec Color", Color) = (1, 1, 1, 1)
        _HairRing ("Hair Ring", Float) = 0
        _HairRingPos ("Hair Ring Pos", Float) = 0.55
        _HairRingWidth ("Hair Ring Width", Float) = 0.08
        _HairRingCurve ("Hair Ring Curve", Float) = 0.3
        _HairLuma ("Hair Luma (measured)", Float) = 0.1
        _HairSpecMatte ("Hair Spec Matte", Float) = 0
        _HairSpecBody ("Hair Spec Body", Float) = 1
        _HairRingSoft ("Hair Ring Soft", Float) = 1
        _HairForm ("Hair Form", Vector) = (0, 0.35, 0, 0.9)
        _HairRingBack ("Hair Ring Back", Float) = 1
        _HairRingStrand ("Hair Ring Strand", Float) = 0

        [Header(Rim Halftone)]
        _Rim ("Rim", Float) = 0
        _RimWidth ("Rim Width", Float) = 0.6
        _RimSoft ("Rim Soft", Float) = 0.05
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimFace ("Rim Face", Float) = 1
        _RimHair ("Rim Hair", Float) = 1
        _Halftone ("Halftone", Float) = 0
        _HalftoneScale ("Halftone Scale", Float) = 5
        [Header(Finish)]
        _Finish ("Finish (exposure, saturation, glow guard)", Vector) = (1, 1, 0, 0)

        [Header(Outline)]
        _OutlineWidth ("Width px (uOutlineWidth)", Float) = 1
        _OutlineZ ("Depth Push (uOutlineZ)", Float) = 0.0004
        _OutlineColor ("Color (uOutlineColor)", Color) = (0.078, 0.047, 0.094, 1)
        _OutlineTex ("Use Texture (uOutlineTex)", Float) = 1
        _OutlineDark ("Texture Darkness (uOutlineDark)", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        CGINCLUDE
        float _RetreatFade;

        void ApplyRetreatFade(float2 pixel)
        {
            float noise = frac(52.9829189 * frac(dot(floor(pixel), float2(0.06711056, 0.00583715))));
            clip(noise - _RetreatFade);
        }
        ENDCG

        Pass
        {
            Name "TOON"
            Tags { "LightMode" = "ForwardBase" }
            Cull Off

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            #define PI 3.141592653589793

            sampler2D _MainTex; float4 _MainTex_ST;
            sampler2D _MeanTex; float _HasMean;
            sampler2D _GlossTex; float _HasGloss;
            sampler2D _ShadeTex; float _HasShade;
            float4 _BaseColor; float _Part; float _NoSkin; float _Steel; float _NoGold; float _GoldAny;
            sampler2D _RampSkin; sampler2D _RampCloth; sampler2D _RampHair;
            float4 _LightColor; float4 _Ambient; float _AmbientStrength; float _CastShadow; float _AlbedoGamma;
            float _AlbedoSat;
            float _AlbedoFlat; float _TexShade; float4 _GlossCut; float _SoftNormal; float _SoftNormalSkin; float _AO;
            float _ClothHueKeep; float _HairHueKeep; float _ShadeAmt; float _ShadeHue;
            float _HasFace; float _FaceSDF; float _FaceSoft; float _FaceCurve; float _FaceNormalSmooth;
            float _FaceSoftNrm;
            float _FaceCastShadow; float _FaceBias; float _NeckShadow; float _NeckFull; float _NeckTint;
            float _NeckShadeAmt;
            float _FaceWrap; float _FaceEdge; float _FaceCenterLit; float _FaceBend; float _FaceBow;
            float _FaceFlat; float _FaceShade;
            float4 _FaceSdf2; float4 _FaceSdf2Curve; float _FaceSdf2Soft; float _FaceSdf2Back; float _FaceSdf2Shift;
            sampler2D _FaceSdfTex; float _FaceSdfMapOn; float4 _FaceSdfRect;
            float4 _FaceTexFlat; float4 _FaceTexFlatCol; float4 _FaceTexFlatRef;
            float _SkinPick; float _SkinPickWide; float4 _SkinPickSet; float4 _SkinRef; float4 _SkinTone;
            float4 _SkinToneCol;
            float4 _EyePos; float4 _EyeFx; float _EyeHlSize; float _EyeUvU;
            float _Metal; float _MetalBand; float _MetalSpec; float4 _MetalTint; float4 _MetalSteelTint;
            float _MetalSatMin;
            float _HairSpec; float _HairSpecPow; float _HairSpecThresh; float4 _HairStrand; float4 _HairSpecColor;
            float _HairRing; float _HairRingPos; float _HairRingWidth; float _HairRingCurve; float _HairLuma;
            float _HairSpecMatte; float _HairSpecBody; float _HairRingSoft; float4 _HairForm; float _HairRingBack;
            float _HairRingStrand;
            float _Rim; float _RimWidth; float _RimSoft; float4 _RimColor; float _RimFace; float _RimHair;
            float _Halftone; float _HalftoneScale;

            float4 _GamoriLightDir;
            float4 _Finish;

            // _GamoriBurn.xyz = 진행률, 골반 높이, 길이의 역수
            float4 _GamoriBurn;
            float GamoriBurnHash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            float GamoriBurnNoise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(GamoriBurnHash(i), GamoriBurnHash(i + float3(1, 0, 0)), f.x),
                                 lerp(GamoriBurnHash(i + float3(0, 1, 0)), GamoriBurnHash(i + float3(1, 1, 0)), f.x),
                                     f.y),
                            lerp(lerp(GamoriBurnHash(i + float3(0, 0, 1)), GamoriBurnHash(i + float3(1, 0, 1)), f.x),
                                 lerp(GamoriBurnHash(i + float3(0, 1, 1)), GamoriBurnHash(i + float3(1, 1, 1)), f.x),
                                     f.y), f.z);
            }
            float GamoriBurn(float3 worldPos, out float edge)
            {
                edge = 0.0;
                if (_GamoriBurn.x <= 0.0) return 1.0;
                float3 q = worldPos * _GamoriBurn.z;
                float n = GamoriBurnNoise(q * 9.0) * 0.7 + GamoriBurnNoise(q * 21.0 + 7.3) * 0.3;
                float th = _GamoriBurn.x * 1.25 - 0.15 - (worldPos.y - _GamoriBurn.y) * _GamoriBurn.z * 0.22;
                float vis = smoothstep(th, th + 0.04, n);
                edge = vis * (1.0 - smoothstep(th + 0.04, th + 0.1, n));
                return vis;
            }
            float4 _ToonHeadCenter; float4 _ToonHeadUp; float4 _ToonHeadForward; float _ToonHeadRadius;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float4 tangent : TANGENT;     // xyz = 평활화한 노멀
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 viewNormal : TEXCOORD3;
                float3 softNormal : TEXCOORD4;
                float ao : TEXCOORD6;
                UNITY_SHADOW_COORDS(5)
            };

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                o.ao = v.color.r;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                o.softNormal = UnityObjectToWorldNormal(v.tangent.xyz);
                UNITY_TRANSFER_SHADOW(o, v.uv);
                return o;
            }

            float luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }
            float sq(float x) { return x * x; }

            float3 headRight() { return normalize(cross(_ToonHeadForward.xyz, _ToonHeadUp.xyz)); }

            float3 faceDirH(float3 p)
            {
                float3 h = p - _ToonHeadUp.xyz * dot(p, _ToonHeadUp.xyz);
                float r = length(h);
                return r > 1e-5 ? h / r : _ToonHeadForward.xyz;
            }

            float goldMask(float3 lin)
            {
                float3 c = pow(max(lin, 0.0), 1.0 / 2.2);
                float mx = max(max(c.r, c.g), c.b);
                float mn = min(min(c.r, c.g), c.b);
                float sat = (mx - mn) / max(mx, 1e-4);
                float hue = (c.g - c.b) / max(mx - mn, 1e-4);
                float redMax = step(c.g, c.r + 0.04);
                return smoothstep(_MetalSatMin, _MetalSatMin + 0.2, sat) * smoothstep(0.30, 0.50, mx) *
                    smoothstep(0.18, 0.38, hue) * redMax;
            }

            float steelMask(float3 lin)
            {
                float3 c = pow(max(lin, 0.0), 1.0 / 2.2);
                float mx = max(max(c.r, c.g), c.b);
                float mn = min(min(c.r, c.g), c.b);
                float sat = (mx - mn) / max(mx, 1e-4);
                return (1.0 - smoothstep(0.12, 0.22, sat)) * smoothstep(0.38, 0.55, mx);
            }

            float skinMask(float3 lin)
            {
                float3 c = pow(max(lin, 0.0), 1.0 / 2.2);
                float mx = max(max(c.r, c.g), c.b);
                float mn = min(min(c.r, c.g), c.b);
                float sat = (mx - mn) / max(mx, 1e-4);
                float redMax = step(c.g, c.r) * step(c.b, c.r);
                if (_SkinPickSet.x >= 0.0)
                {
                    float nr = 1.0 - smoothstep(_SkinPickSet.z, _SkinPickSet.z + 0.1, length(c - _SkinRef.rgb));
                    float hr = (c.g - c.b) / max(mx - mn, 1e-4);
                    return (1.0 - smoothstep(_SkinPickSet.w, _SkinPickSet.w + 0.06, hr)) *
                        smoothstep(_SkinPickSet.x - 0.04, _SkinPickSet.x, sat)
                        * (1.0 - smoothstep(_SkinPickSet.y, _SkinPickSet.y + 0.08, sat)) *
                            smoothstep(0.5, 0.6, mx) * redMax * nr;
                }
                float nearRef = 1.0 - smoothstep(lerp(0.1, 0.2, _SkinPickWide), lerp(0.18, 0.3, _SkinPickWide),
                    length(c - _SkinRef.rgb));
                float warm = lerp(1.0, smoothstep(-0.01, 0.01, c.g - c.b), _SkinPickWide);
                return warm * smoothstep(lerp(0.15, 0.07, _SkinPickWide), lerp(0.19, 0.11, _SkinPickWide), sat) *
                    (1.0 - smoothstep(0.42, 0.5, sat)) * smoothstep(0.6, 0.7, mx) * redMax * nearRef;
            }

            float3 rampSkin(float x) { return tex2D(_RampSkin, float2(clamp(x, 0.004, 0.996), 0.5)).rgb; }
            float3 rampHair(float x) { return tex2D(_RampHair, float2(clamp(x, 0.004, 0.996), 0.5)).rgb; }
            float3 rampCloth(float x) { return tex2D(_RampCloth, float2(clamp(x, 0.004, 0.996), 0.5)).rgb; }
            float3 rampPart(int part, float x)
            {
                return part <= 1 ? rampSkin(x) : part == 2 ? rampHair(x) :
                rampCloth(x);
            }

            float4 frag(v2f i, float facing : VFACE) : SV_Target
            {
                ApplyRetreatFade(i.pos.xy);
                float burnEdge;
                clip(GamoriBurn(i.worldPos, burnEdge) - 0.5);
                float3 uHeadCenter = _ToonHeadCenter.xyz;
                float3 uHeadUp = _ToonHeadUp.xyz;
                float3 uHeadForward = _ToonHeadForward.xyz;
                float3 uHeadRight = headRight();
                float uHeadRadius = max(_ToonHeadRadius, 1e-4);

                float4 tex = tex2D(_MainTex, i.uv);
                float3 rawAlbedo = tex.rgb * _BaseColor.rgb;
                float3 albedo = pow(max(rawAlbedo, 0.0), _AlbedoGamma);
                float la = luma(albedo);
                albedo = max(lerp(la.xxx, albedo, _AlbedoSat), 0.0);

                int part = (int)(_Part + 0.5);
                float neckW = 0.0, neckShadeW = 0.0;
                if ((part == 3 || part == 1) && _NeckTint > 0.0)
                {
                    float3 hq = i.worldPos - uHeadCenter;
                    float vq = dot(hq, uHeadUp) / uHeadRadius;
                    float rq = length(hq - uHeadUp * dot(hq, uHeadUp)) / uHeadRadius;
                    float mxq = max(max(rawAlbedo.r, rawAlbedo.g), rawAlbedo.b);
                    neckW = (1.0 - smoothstep(0.5, 0.68, rq)) * smoothstep(-0.5, -0.65, vq) *
                        (1.0 - smoothstep(1.9, 2.2, -vq))
                          * smoothstep(0.25, 0.4, mxq) * _NeckTint;
                    neckShadeW = neckW;
                }
                if (part == 3 && _SkinPick > 0.0 && _NoSkin < 0.5 && neckW < 0.05 && skinMask(rawAlbedo) *
                    _SkinPick > 0.5) part = 0;
                float bake = 0.0;
                if (_HasMean > 0.5 && (_AlbedoFlat > 0.0 || _TexShade > 0.0))
                {
                    float mAvg = tex2D(_MeanTex, i.uv).r; mAvg *= mAvg;
                    float lr = log2(clamp(mAvg / max(luma(rawAlbedo), 1e-4), 0.2, 5.0));
                    float keep = 1.0 - smoothstep(1.1, 1.9, abs(lr));
                    float lrB = log2(clamp(mAvg / max(luma(tex2Dbias(_MainTex, float4(i.uv, 0.0, 5.0)).rgb *
                        _BaseColor.rgb), 1e-4), 0.2, 5.0));
                    bake = -lrB * (1.0 - smoothstep(1.1, 1.9, abs(lrB)));
                    if (part > 1) { float fl = lr * keep * _AlbedoFlat; albedo *= exp2(fl < 0.0 ? fl * 0.4 : fl); }
                }
                if (_HasGloss > 0.5 && _GlossCut.x > 0.0)
                {
                    float gmk = tex2D(_GlossTex, i.uv).r * _GlossCut.x;
                    float gl = luma(albedo), gc = max(_GlossCut.y, 1e-3);
                    albedo *= lerp(1.0, gc * (1.0 - exp(-gl / gc)) / max(gl, 1e-5), gmk);
                }
                if (part == 1 && _FaceTexFlat.x > 0.0)
                {
                    float3 fc = pow(max(albedo, 0.0), 1.0 / 2.2);
                    float fmx = max(max(fc.r, fc.g), fc.b), fmn = min(min(fc.r, fc.g), fc.b);
                    float fsat = (fmx - fmn) / max(fmx, 1e-4);
                    float3 refC = _FaceTexFlatRef.rgb;
                    float fd = length(fc / max(fmx, 1e-4) - refC / max(max(refC.r, refC.g), refC.b));
                    float fm = (1.0 - smoothstep(_FaceTexFlat.y, _FaceTexFlat.y + 0.06, fd)) *
                        smoothstep(_FaceTexFlat.z - 0.08, _FaceTexFlat.z, fmx)
                             * smoothstep(0.08, 0.13, fsat) * (1.0 - smoothstep(0.42, 0.5, fsat));
                    albedo = lerp(albedo, pow(_FaceTexFlatCol.rgb, 2.2), fm * _FaceTexFlat.x);
                }
                float3 N = normalize(i.worldNormal);
                float3 VN = normalize(i.viewNormal);
                float3 SN = normalize(i.softNormal);
                bool front = facing > 0;
                if (!front) { N = -N; VN = -VN; SN = -SN; }
                if (part != 1) N = normalize(lerp(N, SN, part == 0
                    && _SoftNormalSkin >= 0.0 ? _SoftNormalSkin : _SoftNormal));
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 L = normalize(_GamoriLightDir.w > 0.5 ? _GamoriLightDir.xyz : _WorldSpaceLightPos0.xyz);
                float3 hp = i.worldPos - uHeadCenter;

                if (part == 1 && _FaceSoftNrm > 0.0) N = normalize(lerp(N, SN, _FaceSoftNrm));
                if (part == 1) N = normalize(lerp(N, normalize(hp + 1e-5), _FaceNormalSmooth));

                float NdotL = dot(N, L);
                UNITY_LIGHT_ATTENUATION(sh, i, i.worldPos);
                float lf = NdotL * 0.5 + 0.5;
                if (part != 1) lf = clamp(lf + clamp(bake, -1.2, 1.2) * 0.11 * _TexShade, 0.0, 1.0);

                float faceLit = 1.0;
                float softLit = 0.0, softW = 0.0;
                float faceW = 0.0;
                if (part <= 1)
                {
                    float3 hpH = hp - uHeadUp * dot(hp, uHeadUp);
                    float rH = length(hpH);
                    float3 dirH = rH > 1e-5 ? hpH / rH : uHeadForward;
                    float v = dot(hp, uHeadUp) / uHeadRadius;
                    float region = part == 1 ? 1.0
                        : (1.0 - smoothstep(1.3, 1.9, rH / uHeadRadius)) * (1.0 - smoothstep(3.2, 4.2, -v)) * _HasFace;
                    float faceShadowMul = lerp(1.0, sh, _CastShadow * _FaceCastShadow);

                    if (part == 1) lf = clamp(NdotL * 0.5 + 0.5 + _FaceWrap, 0.0, 1.0) * faceShadowMul;
                    if (part == 1 && _FaceBend > 1.0)
                    {
                        float3 Lh = L - uHeadUp * dot(L, uHeadUp);
                        float lh = length(Lh);
                        if (lh > 1e-4)
                        {
                            Lh /= lh;
                            float th = acos(clamp(dot(Lh, uHeadForward), -1.0, 1.0));
                            float sd = dot(Lh, uHeadRight) >= 0.0 ? 1.0 : -1.0;
                            float th2 = PI * pow(th / PI, _FaceBend);
                            float3 Lb = uHeadForward * cos(th2) + uHeadRight * (sd * sin(th2));
                            float sideAmt = 1.0 - abs(dot(Lh, uHeadForward));
                            float bow = _FaceBow * sideAmt * (exp(-sq((v + 0.35) / 0.45)) - 0.35);
                            float3 Nf = normalize(N - uHeadUp * dot(N, uHeadUp) * _FaceFlat + 1e-5);
                            lf = clamp(dot(Nf, normalize(Lb)) * 0.5 + 0.5 + _FaceWrap + bow, 0.0, 1.0) * faceShadowMul;
                        }
                    }
                    if ((part == 1 || (part == 0 && (_FaceSdf2Soft > 0.0 || _FaceSdfMapOn > 0.5) && region > 0.01))
                        && _FaceSdf2.x > 0.0)
                    {
                        float3 L2 = L - uHeadUp * dot(L, uHeadUp);
                        L2 = length(L2) > 1e-4 ? normalize(L2) : uHeadForward;
                        float sideSign = dot(L2, uHeadRight) >= 0.0 ? -1.0 : 1.0;
                        float ang = pow(acos(clamp(dot(L2, uHeadForward), -1.0, 1.0)) / PI, max(_FaceBend, 1.0));
                        float ey = clamp((v - _FaceSdf2.z) / _FaceSdf2.w, -0.98, 0.98);
                        float bnd = (1.15 + _FaceSdf2Soft) * cos(PI * ang);
                        if (bnd < 0.0) bnd *= _FaceSdf2Back;
                        float xs = dot(hp, uHeadRight) / uHeadRadius * sideSign - _FaceSdf2Shift *
                            max(1.0 - bnd * bnd / 1.3225, 0.0);
                        float uu = clamp(xs / (_FaceSdf2.y * (part == 1 ? sqrt(1.0 - ey * ey) : 1.0)), -1.0, 1.0);
                        if (_FaceSdf2Curve.x > 0.0)
                        {
                            float sf = 1.0 - bnd * bnd / 1.3225;
                            float cw = max(_FaceSdf2Curve.z, 0.05);
                            uu -= _FaceSdf2Curve.x * sf * (exp(-sq((v - _FaceSdf2Curve.y) / cw)) - 0.5);
                        }
                        float a2 = fwidth(uu) * 1.2 + _FaceSoft * 0.3 + _FaceSdf2Soft;
                        float lit2 = 1.0 - smoothstep(bnd - a2, bnd + a2, uu);
                        if (_FaceSdfMapOn > 0.5)
                        {
                            float2 muv = float2(dot(hp, uHeadRight) / uHeadRadius * sideSign / _FaceSdfRect.x *
                                0.5 + 0.5, (v - _FaceSdfRect.y) / (_FaceSdfRect.z - _FaceSdfRect.y));
                            float tm = tex2D(_FaceSdfTex, muv).r;
                            float am = max(fwidth(tm) * 0.75, 0.004) + _FaceSoft * 0.1;
                            lit2 = smoothstep(ang - am, ang + am, tm);
                        }
                        float fd2 = dot(dirH, uHeadForward);
                        float wf2 = lerp(smoothstep(0.15, 0.45, fd2), smoothstep(-0.5, -0.2, fd2),
                            _FaceSdf2Curve.w) * _FaceSdf2.x;
                        if (part == 0) wf2 *= region;
                        lf = lerp(lf, lerp(0.3, 0.88, lit2) * faceShadowMul, wf2);
                        if (_FaceSdf2Soft > 0.0) { softLit = lit2; softW = wf2; }
                    }
                    if (part == 1 && _EyePos.w > 0.0 && _EyeFx.x > 0.0)
                    {
                        float3 qe = float3(dot(hp, uHeadRight), dot(hp, uHeadUp), dot(hp, uHeadForward)) / uHeadRadius;
                        float2 d2 = (qe.xy - float2(qe.x >= 0.0 ? _EyePos.x : -_EyePos.x, _EyePos.y)) / _EyePos.w;
                        float em = (1.0 - smoothstep(2.0, 2.4, length(d2 * float2(1.0, 1.6)))) *
                            step(_EyePos.z - 0.35, qe.z) * (_EyeUvU > 0.0 ? step(_EyeUvU, i.uv.x) : 1.0);
                        lf = lerp(lf, max(lf, 0.9), em * _EyeFx.x);
                    }
                    if (part == 1 && _FaceCenterLit > 0.0)
                    {
                        float3 Lf = L - uHeadUp * dot(L, uHeadUp);
                        Lf = length(Lf) > 1e-4 ? normalize(Lf) : uHeadForward;
                        float lightFront = smoothstep(-0.25, 0.25, dot(Lf, uHeadForward));
                        float keepLit = smoothstep(_FaceCenterLit - 0.05, _FaceCenterLit + 0.01,
                            dot(dirH, uHeadForward)) * lightFront;
                        lf = lerp(lf, max(lf, 0.8), keepLit);
                    }
                    if (_FaceSDF > 0.0)
                    {
                        float3 Lh = L - uHeadUp * dot(L, uHeadUp);
                        float lhl = length(Lh);
                        Lh = lhl > 1e-4 ? Lh / lhl : uHeadForward;
                        float facingL = dot(Lh, uHeadForward);
                        float frontF = max(dot(dirH, uHeadForward), 0.0);
                        float side = 1.0 - abs(facingL);
                        float cheek = exp(-sq((v + 0.45) / 0.32));
                        float shape = 0.15 * cheek * _FaceCurve * side * frontF;
                        float bias = _FaceBias * side * frontF;
                        float d = dot(dirH, Lh) + shape + bias;
                        float aa = fwidth(d) * 1.2;
                        faceLit = smoothstep(-_FaceSoft - aa, _FaceSoft + aa, d);
                        faceW = region * _FaceSDF;
                        lf = lerp(lf, lerp(0.32, 0.88, faceLit) * faceShadowMul, faceW);
                    }
                    float jawUnder = part == 1
                        ? smoothstep(-0.4, -0.7, dot(normalize(i.worldNormal), uHeadUp)) * smoothstep(-0.85, -1.05, v)
                        : lerp(smoothstep(-0.95, -1.25, v), smoothstep(-0.45, -0.75, v), _NeckFull) * region;
                    lf = lerp(lf, min(lf, 0.34), jawUnder * _NeckShadow);
                    if (part == 1 || jawUnder * _NeckShadow > 0.01) lf = max(lf, 0.3 * faceShadowMul);
                }

                float castSh = part == 1 ? 1.0 : sh;
                float3 ramp = rampPart(part, lf);
                float3 rampCast = rampPart(part, lf * castSh);
                if (part == 1 && (_FaceEdge > 0.0 || _FaceShade < 1.0))
                {
                    float3 fr = ramp;
                    if (_FaceEdge > 0.0)
                    {
                        fr = (rampSkin(lf - _FaceEdge) + rampSkin(lf - _FaceEdge * 0.5) + ramp +
                            rampSkin(lf + _FaceEdge * 0.5) + rampSkin(lf + _FaceEdge)) / 5.0;
                    }
                    ramp = lerp(rampSkin(0.996), fr, _FaceShade);
                    rampCast = ramp;
                }
                if (softW > 0.0)
                {
                    float3 sr = lerp(rampSkin(0.3), rampSkin(0.996), softLit);
                    ramp = lerp(ramp, sr, softW); rampCast = ramp;
                }
                ramp = lerp(ramp, rampCast, _CastShadow);
                if (neckW > 0.0)
                {
                    float lfN = min(lf, 0.3);
                    float3 neckRamp = lerp(rampSkin(lf), lerp(rampSkin(0.9), rampSkin(0.3), _NeckShadeAmt),
                        neckShadeW / max(neckW, 1e-4));
                    ramp = lerp(ramp, neckRamp, neckW);
                    lf = lerp(lf, lfN, neckShadeW);
                }
                lf *= lerp(1.0, castSh, _CastShadow);

                float vAO = i.ao;
                float occl = (1.0 - smoothstep(0.45, 0.85, vAO)) * _AO * (part == 1 ? 0.35 : 1.0);
                float3 rampShadow = rampPart(part, 0.3);
                rampShadow = lerp(rampShadow, rampSkin(0.3), neckW);
                occl *= 1.0 - 0.65 * neckW;
                ramp = lerp(ramp, min(ramp, rampShadow), occl);
                lf = lerp(lf, min(lf, 0.32), occl);

                float3 H = normalize(L + V);
                float NdotH = max(dot(N, H), 0.0);
                float litMask = smoothstep(0.38, 0.6, lf);
                float gGold = (part == 3 || _GoldAny > 0.5) && _NoGold < 0.5 ? goldMask(rawAlbedo) : 0.0;
                float gSteel = part == 3 && _Steel > 0.0 ? steelMask(rawAlbedo) * _Steel : 0.0;
                float gm = max(gGold, gSteel);
                float3 metalTint = lerp(_MetalTint.rgb, _MetalSteelTint.rgb, gSteel / max(gGold + gSteel, 1e-4));
                float ring = 0.0;

                float3 shaded = albedo * ramp;
                if (part == 3 && _ClothHueKeep > 0.0)
                {
                    float rl = luma(ramp);
                    float satBoost = lerp(1.0, 1.25, clamp((1.0 - rl) * 2.0, 0.0, 1.0));
                    float3 keepC = max(lerp(luma(albedo).xxx, albedo, satBoost), 0.0) * rl;
                    shaded = lerp(shaded, keepC, _ClothHueKeep * (1.0 - neckW));
                }
                if (part == 2 && _HairHueKeep > 0.0)
                {
                    float rl = luma(ramp);
                    float satBoost = lerp(1.0, 1.12, clamp((1.0 - rl) * 2.0, 0.0, 1.0));
                    float3 keepC = max(lerp(luma(albedo).xxx, albedo, satBoost), 0.0) * rl;
                    shaded = lerp(shaded, keepC, _HairHueKeep);
                }
                if (_HasShade > 0.5 && _ShadeAmt > 0.0)
                {
                    float3 rLit = rampPart(part, 0.996);
                    float3 rSh = rampPart(part, 0.3);
                    float litW = clamp((luma(ramp) - luma(rSh)) / max(luma(rLit) - luma(rSh), 1e-3), 0.0, 1.0);
                    float3 shadeAlb = pow(max(tex2D(_ShadeTex, i.uv).rgb * _BaseColor.rgb, 0.0), _AlbedoGamma);
                    float3 hue = rSh / max(max(max(rSh.r, rSh.g), rSh.b), 1e-3);
                    float3 shadeCol = shadeAlb * lerp(float3(1.0, 1.0, 1.0), hue, _ShadeHue);
                    shaded = lerp(shaded, lerp(shadeCol, albedo * rLit, litW), _ShadeAmt);
                }
                float3 col = shaded * _LightColor.rgb + albedo * _Ambient.rgb * _AmbientStrength;

                if (_Metal > 0.0 && gm > 0.0)
                {
                    float mc = VN.y * 0.5 + 0.5 + VN.x * 0.12;
                    float band = smoothstep(_MetalBand - 0.035, _MetalBand + 0.035, mc);
                    float spec = smoothstep(0.90, 0.93, NdotH);
                    float3 metalCol = albedo * metalTint * lerp(0.5, 1.3, band) * lerp(0.55, 1.0, litMask)
                                    + albedo * spec * _MetalSpec * 2.5;
                    col = lerp(col, metalCol, gm * _Metal);
                }

                if (part == 2 && _HairSpec > 0.0)
                {
                    float3 T = normalize(uHeadUp - N * dot(N, uHeadUp) + 1e-5);
                    float th = dot(T, H);
                    float kk = pow(sqrt(max(1.0 - th * th, 0.0)), _HairSpecPow);
                    float strand = smoothstep(_HairStrand.x, _HairStrand.y, la / max(_HairLuma, 1e-4));
                    ring = smoothstep(_HairSpecThresh, _HairSpecThresh + 0.04, kk) * strand * litMask * _HairSpecBody;
                    if (_HairRing > 0.0)
                    {
                        float3 Ns = normalize(hp + 1e-5);
                        float3 vs = normalize(mul((float3x3)UNITY_MATRIX_V, Ns));
                        float hr = length(hp) / uHeadRadius;
                        float vH = dot(hp, uHeadUp) / uHeadRadius;
                        float headZone = (1.0 - smoothstep(2.4, 3.0, hr)) * smoothstep(-0.6, 0.0, vH);
                        float ax = abs(vs.x);
                        float w = _HairRingWidth * (1.0 - 0.65 * ax);
                        float c = _HairRingPos - _HairRingCurve * ax * ax;
                        float vy = lerp(vs.y, VN.y, _HairRingStrand);
                        float e = _HairRingSoft >= 1.0 ? 0.55 * w : max(0.55 * _HairRingSoft * w, fwidth(vy) * 1.5);
                        float lowE = c - w, upE = c + w;
                        float band = smoothstep(lowE, lowE + e, vy) * (1.0 - smoothstep(upE - e, upE, vy)) *
                            (1.0 - smoothstep(0.6, 0.9, ax));
                        float brk = smoothstep(0.7, 1.0, la / max(_HairLuma, 1e-4)) *
                            smoothstep(0.45, 0.75, dot(N, Ns));
                        float backF = lerp(_HairRingBack, 1.0, smoothstep(-0.35, 0.25, dot(Ns, uHeadForward)));
                        float ringH = band * brk * headZone * backF * smoothstep(0.05, 0.3, vs.z) *
                            lerp(0.55, 1.0, litMask);
                        ring = lerp(ring, max(ring * (1.0 - headZone), ringH), _HairRing);
                    }
                    float k = ring * _HairSpec;
                    float3 hcol = _HairSpecColor.rgb * _LightColor.rgb;
                    float grain = clamp(la / max(_HairLuma, 1e-4), 0.55, 1.4);
                    float3 hairHue = albedo / max(max(max(albedo.r, albedo.g), albedo.b), 1e-3);
                    float3 target = lerp(hcol, hcol * lerp(float3(1.0, 1.0, 1.0), hairHue, 0.3) * grain,
                        _HairSpecMatte);
                    float kk2 = clamp(k, 0.0, 1.0) * lerp(1.0, 0.85, _HairSpecMatte);
                    col = lerp(col, target, kk2) + hcol * max(k - 1.0, 0.0) * 0.5 * (1.0 - _HairSpecMatte);
                }

                if (part <= 1 && (_SkinTone.x > 0.0 || _SkinTone.y > 0.0 || _SkinTone.z > 0.0))
                {
                    col *= lerp(float3(1.0, 1.0, 1.0), float3(1.0, 0.93, 0.87), _SkinTone.x);
                    if (part == 1)
                    {
                        float fx = dot(hp, uHeadRight) / uHeadRadius / max(_FaceSdf2.y, 0.1);
                        float fy = (dot(hp, uHeadUp) / uHeadRadius - _FaceSdf2.z) / max(_FaceSdf2.w, 0.1);
                        float er = length(float2(fx, fy * 0.9));
                        float edgeT = smoothstep(0.45, 1.05, er) *
                            smoothstep(-0.2, 0.4, dot(faceDirH(hp), uHeadForward));
                        col *= lerp(float3(1.0, 1.0, 1.0), _SkinToneCol.rgb, edgeT * _SkinTone.y);
                        float2 bc = float2(abs(fx) - 0.62, (dot(hp, uHeadUp) / uHeadRadius - _SkinTone.w) / 0.16);
                        float2 bq = bc / float2(0.3, 1.0);
                        float blush = exp(-dot(bq, bq));
                        col *= lerp(float3(1.0, 1.0, 1.0), _SkinToneCol.rgb, blush * _SkinTone.z);
                    }
                }

                if (part == 2 && (_HairForm.x > 0.0 || _HairForm.z > 0.0))
                {
                    float3 Nr = normalize(i.worldNormal) * (front ? 1.0 : -1.0);
                    float ndv = max(dot(Nr, V), 0.0);
                    float aF = fwidth(ndv) * 1.2 + 0.01;
                    float side = 1.0 - smoothstep(_HairForm.y - aF, _HairForm.y + aF, ndv);
                    col = lerp(col, col * float3(0.6, 0.48, 0.56), side * _HairForm.x);
                    float mid = smoothstep(_HairForm.w - aF, _HairForm.w + aF, ndv);
                    col = lerp(col, col * 1.32, mid * _HairForm.z * lerp(0.6, 1.0, litMask));
                }

                float fres = 1.0 - max(dot(N, V), 0.0);
                float rim = smoothstep(_RimWidth, _RimWidth + _RimSoft, fres) * (0.3 + 0.7 * litMask);
                col += rim * _RimColor.rgb * _Rim * (part == 1 ? _RimFace : part == 2 ? _RimHair : 1.0) *
                    lerp(float3(1.0, 1.0, 1.0), albedo * 3.0, 0.4);

                if (part == 1 && _EyePos.w > 0.0)
                {
                    float3 qe = float3(dot(hp, uHeadRight), dot(hp, uHeadUp), dot(hp, uHeadForward)) / uHeadRadius;
                    float sx = qe.x >= 0.0 ? 1.0 : -1.0;
                    float2 d2 = (qe.xy - float2(sx * _EyePos.x, _EyePos.y)) / _EyePos.w;
                    float gate = step(_EyePos.z - 0.35, qe.z) * (_EyeUvU > 0.0 ? step(_EyeUvU, i.uv.x) : 1.0);
                    float rr = length(d2);
                    float aE = fwidth(rr) * 1.2 + 1e-4;
                    float irisDisk = (1.0 - smoothstep(1.05 - aE, 1.05 + aE, rr)) * gate;
                    if (irisDisk > 0.0)
                    {
                        float amx = max(max(albedo.r, albedo.g), albedo.b);
                        float irisM = smoothstep(0.35, 0.6,
                            (amx - min(min(albedo.r, albedo.g), albedo.b)) / max(amx, 1e-4));
                        float3 boosted = max(lerp(luma(col).xxx, col, 1.35), 0.0) * 1.18;
                        col = lerp(col, boosted, irisDisk * irisM * _EyeFx.y);
                        float lidE = d2.y + 0.18 * d2.x * d2.x;
                        float lid = smoothstep(0.02 - aE * 2.0, 0.02 + aE * 2.0, lidE);
                        col = lerp(col, col * float3(0.48, 0.4, 0.46), irisDisk * irisM * lid * _EyeFx.z);
                        float s1 = 1.0 - smoothstep(_EyeHlSize - aE, _EyeHlSize + aE,
                            length(d2 - float2(0.38 * sx, 0.36)));
                        float s2 = (1.0 - smoothstep(_EyeHlSize * 0.5 - aE, _EyeHlSize * 0.5 + aE,
                            length(d2 - float2(-0.3 * sx, -0.42)))) * 0.8;
                        col = lerp(col, float3(1.0, 0.97, 0.97) * _LightColor.rgb, irisDisk * max(s1, s2) * _EyeFx.w);
                    }
                }

                if (_Halftone > 0.0)
                {
                    float2 p = i.pos.xy / _HalftoneScale;
                    p = float2(0.7071 * p.x + 0.7071 * p.y, -0.7071 * p.x + 0.7071 * p.y);
                    float d = length(frac(p) - 0.5);
                    float shade = 1.0 - smoothstep(0.3, 0.62, lf);
                    float r = shade * 0.6;
                    float dots = 1.0 - smoothstep(r - 0.06, r + 0.06, d);
                    col = lerp(col, col * 0.3, dots * _Halftone);
                }

                col *= _Finish.x;
                col = max(lerp(dot(col, float3(0.2126, 0.7152, 0.0722)).xxx, col, _Finish.y), 0.0);
                if (_Finish.z > 0.0)
                {
                    float bright = dot(col, float3(0.2126, 0.7152, 0.0722));
                    float knee = _Finish.z * 0.82, room = _Finish.z - knee;
                    if (bright > knee) col *= (_Finish.z - room * exp(-(bright - knee) / room)) / bright;
                }
                col = lerp(col, float3(1.4, 0.2, 0.3), burnEdge * 0.8);
                return float4(col, 1.0);
            }
            ENDCG
        }

        Pass
        {
            Name "SHADOWCASTER"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            float4 _GamoriBurn;
            float GamoriBurnHash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            float GamoriBurnNoise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(GamoriBurnHash(i), GamoriBurnHash(i + float3(1, 0, 0)), f.x),
                                 lerp(GamoriBurnHash(i + float3(0, 1, 0)), GamoriBurnHash(i + float3(1, 1, 0)), f.x),
                                     f.y),
                            lerp(lerp(GamoriBurnHash(i + float3(0, 0, 1)), GamoriBurnHash(i + float3(1, 0, 1)), f.x),
                                 lerp(GamoriBurnHash(i + float3(0, 1, 1)), GamoriBurnHash(i + float3(1, 1, 1)), f.x),
                                     f.y), f.z);
            }
            float GamoriBurn(float3 worldPos, out float edge)
            {
                edge = 0.0;
                if (_GamoriBurn.x <= 0.0) return 1.0;
                float3 q = worldPos * _GamoriBurn.z;
                float n = GamoriBurnNoise(q * 9.0) * 0.7 + GamoriBurnNoise(q * 21.0 + 7.3) * 0.3;
                float th = _GamoriBurn.x * 1.25 - 0.15 - (worldPos.y - _GamoriBurn.y) * _GamoriBurn.z * 0.22;
                float vis = smoothstep(th, th + 0.04, n);
                edge = vis * (1.0 - smoothstep(th + 0.04, th + 0.1, n));
                return vis;
            }
            struct v2f
            {
                V2F_SHADOW_CASTER;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o = (v2f)0;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                ApplyRetreatFade(i.pos.xy);
                float burnEdge;
                clip(GamoriBurn(i.worldPos, burnEdge) - 0.5);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode" = "Always" }
            Cull Front

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            float4 _BaseColor;
            float _OutlineWidth;
            float _OutlineZ;
            float4 _OutlineColor;
            float _OutlineTex;
            float _OutlineDark;

            float4 _GamoriBurn;
            float GamoriBurnHash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            float GamoriBurnNoise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(GamoriBurnHash(i), GamoriBurnHash(i + float3(1, 0, 0)), f.x),
                                 lerp(GamoriBurnHash(i + float3(0, 1, 0)), GamoriBurnHash(i + float3(1, 1, 0)), f.x),
                                     f.y),
                            lerp(lerp(GamoriBurnHash(i + float3(0, 0, 1)), GamoriBurnHash(i + float3(1, 0, 1)), f.x),
                                 lerp(GamoriBurnHash(i + float3(0, 1, 1)), GamoriBurnHash(i + float3(1, 1, 1)), f.x),
                                     f.y), f.z);
            }
            float GamoriBurn(float3 worldPos, out float edge)
            {
                edge = 0.0;
                if (_GamoriBurn.x <= 0.0) return 1.0;
                float3 q = worldPos * _GamoriBurn.z;
                float n = GamoriBurnNoise(q * 9.0) * 0.7 + GamoriBurnNoise(q * 21.0 + 7.3) * 0.3;
                float th = _GamoriBurn.x * 1.25 - 0.15 - (worldPos.y - _GamoriBurn.y) * _GamoriBurn.z * 0.22;
                float vis = smoothstep(th, th + 0.04, n);
                edge = vis * (1.0 - smoothstep(th + 0.04, th + 0.1, n));
                return vis;
            }

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
                float3 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 clip = UnityObjectToClipPos(v.vertex);
                float3 vn = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                float2 dir = mul((float2x2)UNITY_MATRIX_P, vn.xy);
                float dl = length(dir);
                dir = dl > 1e-5 ? dir / dl : float2(0.0, 0.0);
                float distScale = clamp(2.6 / clip.w, 0.75, 1.0);
                clip.xy += dir * _OutlineWidth * distScale * 2.0 / _ScreenParams.xy * clip.w;
                #if defined(UNITY_REVERSED_Z)
                    clip.z -= _OutlineZ * 0.5 * clip.w;
                #else
                    clip.z += _OutlineZ * clip.w;
                #endif
                o.pos = clip;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                ApplyRetreatFade(i.pos.xy);
                float burnEdge;
                clip(GamoriBurn(i.worldPos, burnEdge) - 0.5);
                float3 a = tex2D(_MainTex, i.uv).rgb * _BaseColor.rgb;
                float3 texCol = pow(max(a, 0.0), 1.4) * _OutlineDark;
                return float4(lerp(_OutlineColor.rgb, texCol, _OutlineTex), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
