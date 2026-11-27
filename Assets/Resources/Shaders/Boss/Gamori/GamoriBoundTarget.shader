Shader "Espoir/Boss/GamoriBoundTarget"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Ramp ("Ramp", 2D) = "white" {}
        _Hot ("Hot", Float) = 0
        _Dis ("Dissolve", Float) = 0
        _Lean ("Lean (m)", Float) = 0
        _Height ("Height (m)", Float) = 1.667
        _Base ("Feet Position", Vector) = (0, 0, 0, 0)
        _PullDir ("Push Direction", Vector) = (0, 0, 1, 0)
        _Grow ("Outline Width (m)", Float) = 0.0035
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex, _Ramp;
    float4 _MainTex_ST, _Base, _PullDir;
    float _Hot, _Dis, _Lean, _Height, _Grow;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 normal : TEXCOORD1;
        float3 world : TEXCOORD2;
        float3 local : TEXCOORD3;
    };

    float Hash(float3 p)
    {
        p = frac(p * 0.3183099 + 0.1);
        p *= 17.0;
        return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
    }

    float Noise(float3 p)
    {
        float3 i = floor(p), f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
            lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                        lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
    }

    v2f Vert(appdata_base v, float grow)
    {
        v2f o;
        float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
        float3 normal = UnityObjectToWorldNormal(v.normal);
        world += normal * grow;
        o.local = world - _Base.xyz;
        float h = saturate(o.local.y / _Height);
        world += _PullDir.xyz * (_Lean * h * h);
        o.world = world;
        o.normal = normal;
        o.pos = UnityWorldToClipPos(world);
        o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
        return o;
    }

    float Burn(v2f i)
    {
        float h = saturate(i.local.y / _Height);
        float dz = Noise(i.local * 9.0 + 4.2) * 0.5 + (1.0 - h) * 0.5;
        clip(dz - _Dis);
        return _Dis > 0.0 ? 1.0 - smoothstep(0.0, 0.08, dz - _Dis) : 0.0;
    }

    float3 Finish(float3 col, float3 normal, float3 world, float burn)
    {
        float3 V = normalize(_WorldSpaceCameraPos.xyz - world);
        float fr = pow(saturate(1.0 - abs(dot(normal, V))), 2.0);
        col += float3(1.3, 0.03, 0.06) * _Hot * (0.25 + 0.75 * fr);
        return lerp(col, float3(3.4, 0.9, 0.7), burn);
    }
    ENDCG

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            v2f vert(appdata_base v) { return Vert(v, 0.0); }

            float4 frag(v2f i, float facing : VFACE) : SV_Target
            {
                float burn = Burn(i);
                float3 n = normalize(i.normal) * (facing > 0.0 ? 1.0 : -1.0);
                float3 base = tex2D(_MainTex, i.uv).rgb;
                float nl = dot(n, normalize(_WorldSpaceLightPos0.xyz)) * 0.5 + 0.5;
                float3 ramp = tex2D(_Ramp, float2(clamp(nl, 0.02, 0.98), 0.5)).rgb;
                float3 col = base * (ramp + float3(0.05, 0.05, 0.07));
                return float4(Finish(col, n, i.world, burn), 1.0);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "Always" }
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            v2f vert(appdata_base v) { return Vert(v, _Grow); }

            float4 frag(v2f i) : SV_Target
            {
                float burn = Burn(i);
                float3 base = tex2D(_MainTex, i.uv).rgb;
                float3 col = lerp(float3(0.078, 0.047, 0.094), base * 0.32, 0.5);
                return float4(Finish(col, -normalize(i.normal), i.world, burn), 1.0);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            struct v2s
            {
                V2F_SHADOW_CASTER;
                float3 local : TEXCOORD1;
            };

            v2s vert(appdata_base v)
            {
                v2s o;
                o.local = mul(unity_ObjectToWorld, v.vertex).xyz - _Base.xyz;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag(v2s i) : SV_Target
            {
                float h = saturate(i.local.y / _Height);
                clip(Noise(i.local * 9.0 + 4.2) * 0.5 + (1.0 - h) * 0.5 - _Dis);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}
