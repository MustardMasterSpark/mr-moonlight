// Opaque colour with a fixed directional light for the sample scenes. The pass has no LightMode
// tag, so it renders in the Built-in pipeline, URP and HDRP.
Shader "DDT/Samples/Simple Lit"
{
    Properties
    {
        _Color ("Color", Color) = (0.8, 0.8, 0.8, 1)
        _LightDirection ("Light Direction", Vector) = (0.4, 0.8, 0.3, 0)
        _Ambient ("Ambient", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            ZWrite On
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            float4 _LightDirection;
            half _Ambient;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.position = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half Diffuse = saturate(dot(normalize(i.worldNormal), normalize(_LightDirection.xyz)));
                half Light = _Ambient + (1.0 - _Ambient) * Diffuse;
                return fixed4(_Color.rgb * Light, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
