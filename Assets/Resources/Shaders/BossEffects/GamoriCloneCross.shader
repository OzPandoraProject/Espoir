Shader "Espoir/Boss/GamoriCloneCross"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _GatePlane ("Gate Plane (normal, distance)", Vector) = (0, 0, 1, 0)
        _Hide ("Hide", Float) = 0
    }
    SubShader
    {
        // 문 내부를 그린 뒤 꽃잎보다 먼저 그린다.
        Tags { "Queue" = "AlphaTest+60" "RenderType" = "Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST, _GatePlane;
            float _Hide;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float side : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(world);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.side = dot(world, _GatePlane.xyz) - _GatePlane.w;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                clip(min(i.side, 0.5 - _Hide));
                return float4(tex2D(_MainTex, i.uv).rgb, 1.0);
            }
            ENDCG
        }
    }
}
