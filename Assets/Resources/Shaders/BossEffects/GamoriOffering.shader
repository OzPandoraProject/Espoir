Shader "Espoir/Boss/GamoriOffering"
{
    Properties
    {
        _Noise ("Noise", 2D) = "gray" {}
        _Height ("Height", Float) = 1.4
        _Seed ("Seed", Float) = 1.9
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Dissolve ("Dissolve", Range(0, 1.1)) = 0
        _Hot ("Hot", Float) = 0
        _Pull ("Pull", Float) = 0
        _Lean ("Lean", Float) = 0
        _PullDir ("Pull Direction", Vector) = (0, 0, 1, 0)
        _Base ("Base Position", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "EspoirBossFxCommon.cginc"

            sampler2D _Noise;
            float _Height, _Seed, _Alpha, _Dissolve, _Hot, _Pull, _Lean;
            float4 _PullDir, _Base;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 local : TEXCOORD2;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.local = world - _Base.xyz;
                float h = saturate(o.local.y / _Height);
                world += _PullDir.xyz * (_Lean * h * h);
                o.world = world;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1.0));
                return o;
            }

            float3 Ramp(float i)
            {
                float3 c = float3(0.02, 0.0, 0.005);
                c = lerp(c, float3(0.17, 0.002, 0.022), smoothstep(0.13, 0.23, i));
                c = lerp(c, float3(1.3, 0.03, 0.06), smoothstep(0.35, 0.45, i));
                c = lerp(c, float3(3.4, 0.9, 0.7), smoothstep(0.75, 0.85, i));
                return c;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                float rim = pow(saturate(1.0 - abs(dot(n, view))), 2.0);
                float h = saturate(i.local.y / _Height);
                float2 flowUv = float2(i.local.x * 0.9 + i.local.z * 0.6 + _Seed, i.local.y * 0.5 - _Time.y * 0.1);
                float flow = tex2D(_Noise, flowUv).a;
                float grain = tex2D(_Noise, i.local.xy * 0.7 + float2(_Seed, i.local.z * 0.5)).r * 0.5 + (1.0 - h)
                    * 0.5;
                clip(grain - _Dissolve);
                float burn = _Dissolve > 0.0 ? 1.0 - smoothstep(0.0, 0.08, grain - _Dissolve) : 0.0;
                float light = 0.11 + 0.1 * flow + 0.06 * (1.0 - h) + rim * 0.6 + _Hot * (0.3 + 0.45 * rim)
                    + burn * 0.62 + _Pull * 0.12 * rim;
                float alpha = clamp((0.52 + 0.42 * rim + 0.3 * burn + 0.2 * _Hot) * _Alpha, 0.0, 0.94);
                clip(alpha - 0.01);
                return float4(EspoirAces(Ramp(saturate(light))), alpha);
            }
            ENDCG
        }
    }
}
