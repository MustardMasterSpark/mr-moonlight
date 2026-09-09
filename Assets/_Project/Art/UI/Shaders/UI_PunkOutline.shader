// Hand-scribbled "punk" outline for uGUI, hugging the alpha silhouette of a button's own sprite
// (torn-paper cutouts). Same visual language as the Highlight Plus 2 stylized outline tuned in
// the Playground project (Docs/highlight-plus-2-punk-outline.md) - Highlight Plus itself only
// supports MeshRenderer/SpriteRenderer/SkinnedMeshRenderer, not uGUI CanvasRenderer, so this
// reimplements the look directly as a UI shader instead of porting the component.
//
// This shader is meant for a dedicated "HighlightOverlay" child quad that's LARGER than the
// button's own RectTransform (see ButtonPunkOutlineHighlight.cs) - not the button's own Image.
// Reason: these torn-paper source textures have almost no transparent margin around the paper
// shape (confirmed by inspecting one directly - the shape touches the texture's own left edge),
// so there is no room inside the texture's own UV range for an outward-growing outline to exist.
// _PaddingUV remaps the overlay's larger 0-1 UV range back down to where the real sprite content
// sits in its middle, leaving genuine "outside the image" UV space around it for the outline to
// march into - and outside-the-image samples are explicitly treated as transparent (SampleAlpha
// below) rather than trusting the texture's hardware wrap mode, which would otherwise clamp to
// (and repeat) whatever opaque edge pixel is nearest.
Shader "MrMoonlight/UI/PunkOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _DistortionTex ("Distortion Noise", 2D) = "gray" {}

        _OutlineWidth ("Outline Width (px)", Float) = 10
        _EdgeAlphaCutoff ("Edge Alpha Cutoff", Range(0,1)) = 0.5

        _DistortionAmount ("Distortion Amount (px)", Float) = 6
        _PatternScale ("Pattern Scale", Float) = 3
        _StopMotionScale ("Stop Motion Rate", Float) = 7

        _GradientKnee ("Gradient Knee (0-1)", Range(0,1)) = 0.6
        _ColorInner ("Outline Color - Inner", Color) = (0,0,0,1)
        _ColorOuter ("Outline Color - Outer", Color) = (1,1,1,1)

        _HighlightAmount ("Highlight Amount", Range(0,1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            // Loop bounds must be compile-time constants, not uniforms - a data-dependent loop
            // count fails to compile on several shader targets (confirmed here: it silently fell
            // back to Unity's magenta error shader with nothing in the Console). Tune these by
            // editing the shader, not the material Inspector.
            #define RING_STEPS 14
            #define ANGLE_STEPS 28

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            sampler2D _DistortionTex;

            // CanvasRenderer binds the sprite texture directly at draw time (bypassing
            // Material.SetTexture), so Unity's usual automatic "_MainTex_TexelSize" companion
            // property is never populated for a uGUI Image (confirmed at runtime: it read back as
            // the (1,1,1,1) default instead of the sprite's real size). Set manually from script.
            float4 _SpriteTexelSize;

            // Fraction of this quad's own 0-1 UV range that is padding beyond the real sprite on
            // each side (x = horizontal fraction per side, y = vertical fraction per side). Set
            // from script to match how much bigger the overlay's RectTransform is than the
            // button's own rect. (0,0) means this quad IS the real sprite bounds (no remap).
            float2 _PaddingUV;

            float _OutlineWidth;
            float _EdgeAlphaCutoff;

            float _DistortionAmount;
            float _PatternScale;
            float _StopMotionScale;

            float _GradientKnee;
            fixed4 _ColorInner;
            fixed4 _ColorOuter;

            float _HighlightAmount;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Deterministic "stop motion" pattern sample: re-samples to a new noise frame at
            // _StopMotionScale steps per second rather than scrolling smoothly, matching the
            // Highlight Plus stylized-outline "boiling/blinking" redraw feel.
            float PatternSample(float2 uv)
            {
                float2 patternUV = uv * _PatternScale + floor(_Time.y * _StopMotionScale) / _StopMotionScale;
                return tex2Dlod(_DistortionTex, float4(patternUV, 0, 0)).r;
            }

            // Treats anything outside the real sprite's 0-1 UV as transparent, rather than
            // trusting the texture's hardware wrap mode (Clamp would otherwise repeat whichever
            // opaque edge pixel is nearest, which is wrong once padding pushes sampling outside
            // the actual image).
            fixed SampleAlpha(float2 uv)
            {
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return 0;
                return tex2Dlod(_MainTex, float4(uv, 0, 0)).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                if (_HighlightAmount <= 0.001) return fixed4(0, 0, 0, 0);

                // Remap this quad's own UV (0-1 across the padded, enlarged overlay) back down to
                // where the real sprite content sits (the middle), so ring-marching outside that
                // central region is genuinely "outside the image" rather than sampling into it.
                float2 uv = (IN.texcoord - _PaddingUV) / max(1.0 - 2.0 * _PaddingUV, 0.0001);
                float2 texel = _SpriteTexelSize.xy;

                fixed baseAlpha = SampleAlpha(uv);
                if (baseAlpha >= _EdgeAlphaCutoff)
                {
                    // Inside the sprite's own silhouette - the real button Image already draws
                    // this pixel, so the overlay stays transparent here (no double-draw).
                    return fixed4(0, 0, 0, 0);
                }

                // March outward in rings, each ring's sample angles jittered by the distortion
                // noise so the silhouette reads as hand-scribbled rather than a clean offset.
                // Ring/angle counts are compile-time (#define above) - see the comment by
                // "#pragma target 3.0" for why a uniform-bounded loop isn't safe here.
                float hitT = 1.0;
                bool found = false;
                for (int r = 1; r <= RING_STEPS; r++)
                {
                    float radiusPx = _OutlineWidth * (r / (float)RING_STEPS);
                    for (int a = 0; a < ANGLE_STEPS; a++)
                    {
                        float angle = 6.2831853 * (a / (float)ANGLE_STEPS);
                        float2 dir = float2(cos(angle), sin(angle));
                        float noise = PatternSample(uv + dir * radiusPx * texel) - 0.5;
                        float jitteredRadius = radiusPx + noise * _DistortionAmount;
                        float2 sampleUV = uv + dir * jitteredRadius * texel;
                        if (SampleAlpha(sampleUV) >= _EdgeAlphaCutoff)
                        {
                            found = true;
                        }
                    }
                    if (found)
                    {
                        hitT = (r - 1) / (float)max(RING_STEPS - 1, 1);
                        break;
                    }
                }

                if (!found) return fixed4(0, 0, 0, 0);

                float g = hitT < _GradientKnee ? 0.0 : saturate((hitT - _GradientKnee) / max(1.0 - _GradientKnee, 0.0001));
                fixed4 outlineColor = lerp(_ColorInner, _ColorOuter, g);
                outlineColor.a *= _HighlightAmount;
                return outlineColor;
            }
            ENDCG
        }
    }
}
