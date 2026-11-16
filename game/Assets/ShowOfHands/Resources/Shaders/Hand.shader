// Glowing hands: soft wrap lighting, a bright rim, and screen-door fading so overlapping balls and tubes
// never need sorting. Per-instance colour and effects; works with single-pass instanced stereo.
Shader "ShowOfHands/Hand"
{
    Properties
    {
        _Color ("Colour (a = shown)", Color) = (1, 0.6, 0.3, 1)
        _Fx ("x glow, y quilt", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 objectPos : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Fx)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.objectPos = v.vertex.xyz;
                return o;
            }

            static const float bayer[16] =
            {
                0.0, 8.0, 2.0, 10.0,
                12.0, 4.0, 14.0, 6.0,
                3.0, 11.0, 1.0, 9.0,
                15.0, 7.0, 13.0, 5.0
            };

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float4 fx = UNITY_ACCESS_INSTANCED_PROP(Props, _Fx);

                uint px = (uint)i.pos.x & 3u;
                uint py = (uint)i.pos.y & 3u;
                float threshold = (bayer[py * 4u + px] + 0.5) / 16.0;
                clip(color.a - threshold);

                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 l = normalize(float3(0.25, 0.85, -0.45));
                float wrap = saturate(dot(n, l) * 0.5 + 0.5);
                float rim = pow(1.0 - saturate(dot(n, v)), 2.2);

                float3 base = color.rgb;
                // Quilt lines for the mitt: a diagonal grid in object space.
                float quilt = 0.0;
                if (fx.y > 0.5)
                {
                    float2 g = float2(i.objectPos.x + i.objectPos.z, i.objectPos.x - i.objectPos.z) * 9.0;
                    float2 f = abs(frac(g) - 0.5);
                    quilt = smoothstep(0.44, 0.5, max(f.x, f.y));
                }
                float3 lit = base * (0.42 + 0.68 * wrap);
                lit += base * rim * 0.9 + rim * 0.12;
                lit += base * fx.x;
                lit = lerp(lit, lit * 0.45, quilt);
                return fixed4(lit, 1.0);
            }
            ENDCG
        }
    }
}
