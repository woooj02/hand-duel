// The stage: a large inside-out sphere with a plum glow low in front and darkness everywhere else.
Shader "ShowOfHands/Backdrop"
{
    Properties
    {
        _Color ("Background", Color) = (0.005, 0.004, 0.008, 1)
        _Glow ("Stage glow", Color) = (0.023, 0.013, 0.05, 1)
        _Forward ("Stage direction", Vector) = (0, 0, 1, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Background" }
        Pass
        {
            Cull Front
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color, _Glow;
            float4 _Forward;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 f = normalize(_Forward.xyz);
                // Brightest a little below straight ahead, falling away to the sides and behind.
                float ahead = saturate(dot(d, normalize(f + float3(0, -0.35, 0))));
                float glow = pow(ahead, 3.0);
                float floorGlow = saturate(-d.y) * 0.35 * saturate(dot(d, f) * 0.5 + 0.5);
                float3 rgb = _Color.rgb + _Glow.rgb * (glow * 1.4 + floorGlow);
                // A little noise hides banding in the dark gradient.
                float n = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453);
                rgb += (n - 0.5) / 16384.0;
                return fixed4(rgb, 1);
            }
            ENDCG
        }
    }
}
