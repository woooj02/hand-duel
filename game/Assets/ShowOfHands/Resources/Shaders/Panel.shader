// Cards, rings and soft glows: one transparent shader with a rounded-rectangle shape, an optional picture,
// and an optional ring. Drawn with a MaterialPropertyBlock per object, so everything shares one material.
Shader "ShowOfHands/Panel"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "white" {}
        _Color ("Fill", Color) = (0.14, 0.1, 0.21, 1)
        _Edge ("Edge", Color) = (0.36, 0.29, 0.52, 1)
        // x: half width, y: half height (metres), z: corner radius, w: edge width
        _Shape ("Shape", Vector) = (0.1, 0.06, 0.02, 0.003)
        // x: mode (0 card, 1 picture, 2 ring, 3 glow), y: ring fill 0..1, z: ring thickness (0..1 of radius), w: picture strength
        _Mode ("Mode", Vector) = (0, 0, 0.18, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Edge)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Shape)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Mode)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float roundedBox(float2 p, float2 half, float r)
            {
                float2 q = abs(p) - half + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float4 fill = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float4 edge = UNITY_ACCESS_INSTANCED_PROP(Props, _Edge);
                float4 shape = UNITY_ACCESS_INSTANCED_PROP(Props, _Shape);
                float4 mode = UNITY_ACCESS_INSTANCED_PROP(Props, _Mode);

                float2 p = (i.uv - 0.5) * shape.xy * 2.0;
                float aa = max(fwidth(p.x), fwidth(p.y)) * 1.5 + 1e-5;

                if (mode.x > 2.5)
                {
                    // Soft round glow.
                    float d = length(i.uv - 0.5) * 2.0;
                    float a = saturate(1.0 - d);
                    return fixed4(fill.rgb, fill.a * a * a);
                }
                if (mode.x > 1.5)
                {
                    // Ring that empties clockwise from the top.
                    float2 c = (i.uv - 0.5) * 2.0;
                    float r = length(c);
                    float w = fwidth(r) * 1.5 + 1e-5;
                    float band = smoothstep(1.0 - mode.z - w, 1.0 - mode.z, r) * (1.0 - smoothstep(1.0 - w, 1.0, r));
                    float angle = atan2(c.x, c.y) / 6.2831853 + 0.5;
                    float on = step(1.0 - angle, mode.y);
                    float3 rgb = lerp(edge.rgb, fill.rgb, on);
                    float alpha = band * lerp(edge.a, fill.a, on);
                    return fixed4(rgb, alpha);
                }
                if (mode.x > 0.5)
                {
                    // A picture, tinted by fill.
                    fixed4 t = tex2D(_MainTex, i.uv);
                    return fixed4(t.rgb * fill.rgb, t.a * fill.a);
                }
                float d = roundedBox(p, shape.xy, shape.z);
                float inside = 1.0 - smoothstep(-aa, aa, d);
                float border = smoothstep(-shape.w - aa, -shape.w + aa, d);
                float3 rgb = lerp(fill.rgb, edge.rgb, border * edge.a);
                float alpha = inside * lerp(fill.a, max(fill.a, edge.a), border);
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
