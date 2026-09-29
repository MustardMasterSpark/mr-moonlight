Shader "Skybox/SkyboxBlender" {

    Properties {
        _Tint ("Tint Color", Color) = (.5, .5, .5, 1)
        _Tint2 ("Tint Color 2", Color) = (.5, .5, .5, 1)

        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        
        _Rotation ("Rotation", Range(0, 360)) = 0
        _BlendCubemaps ("Blend Cubemaps", Range(0, 1)) = 0

        // Local addition: per-sky orientation (x = yaw, y = pitch, z = roll, degrees). _Orient1 turns _Tex,
        // _Orient2 turns _Tex2, so two skies with different origins can blend without one popping.
        // Written by SkyboxOrientationTool; (0,0,0) leaves the sky exactly as authored.
        _Orient1 ("Orientation 1 (yaw, pitch, roll)", Vector) = (0, 0, 0, 0)
        _Orient2 ("Orientation 2 (yaw, pitch, roll)", Vector) = (0, 0, 0, 0)

        [NoScaleOffset] _Tex ("Cubemap (HDR)", Cube) = "grey" {}
        [NoScaleOffset] _Tex2 ("Cubemap (HDR) 2", Cube) = "grey" {}
    }

    SubShader {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
    
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
    
            #include "UnityCG.cginc"
    
            samplerCUBE _Tex;
            samplerCUBE _Tex2;
            
            float _BlendCubemaps;
            half4 _Tex_HDR;
            
            half4 _Tint;
            half4 _Tint2;
            
            half _Exposure;
            float _Rotation;
            float4 _Orient1;
            float4 _Orient2;

            // Sky content ends up at R*d where R = Rz(roll) * Rx(pitch) * Ry(yaw) (yaw uses the same matrix as
            // RotateAroundYInDegrees below, so positive yaw turns the same way as _Rotation). Sampling therefore
            // uses the inverse: undo roll, then pitch, then yaw.
            float3 OrientDir (float3 d, float3 ypr)
            {
                float3 r = radians(ypr);
                float s, c;

                sincos(-r.z, s, c);
                d = float3(c * d.x - s * d.y, s * d.x + c * d.y, d.z);

                sincos(-r.y, s, c);
                d = float3(d.x, c * d.y - s * d.z, s * d.y + c * d.z);

                sincos(-r.x, s, c);
                d = float3(c * d.x - s * d.z, d.y, s * d.x + c * d.z);

                return d;
            }


            float4 RotateAroundYInDegrees (float4 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;

                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                
                return float4(mul(m, vertex.xz), vertex.yw).xzyw;
            }
        
            struct appdata_t {
                float4 vertex : POSITION;
                float3 normal : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
    
            struct v2f {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;

                UNITY_VERTEX_OUTPUT_STEREO
            };
    
            v2f vert (appdata_t v)
            {
                v2f o;
                
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(RotateAroundYInDegrees(v.vertex, _Rotation));
                o.texcoord = v.vertex;
                
                return o;
            }
    
            fixed4 frag (v2f i) : SV_Target
            {
                float4 env1 = texCUBE (_Tex, OrientDir(i.texcoord, _Orient1.xyz));
                float4 env2 = texCUBE (_Tex2, OrientDir(i.texcoord, _Orient2.xyz));
                float4 env = lerp( env1, env2, _BlendCubemaps );

                float4 tint = lerp( _Tint, _Tint2, _BlendCubemaps );
                half3 c = DecodeHDR (env, _Tex_HDR);

                c = c * tint.rgb * unity_ColorSpaceDouble;
                c *= _Exposure;
                
                return half4(c, 1);
            }

            ENDCG
        }
    }
    Fallback Off
}