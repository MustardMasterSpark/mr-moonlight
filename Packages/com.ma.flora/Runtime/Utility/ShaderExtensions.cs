// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEngine;

namespace MA.Flora
{
    internal static class ShaderExtensions
    {
        public static bool HasOverridableGlobalKeyword(this Shader shader, string keyword)
        {
            return shader.keywordSpace.FindKeyword(keyword) is { isValid: true, isOverridable: true };
        }

        public static bool HasDOTSKeyword(this Shader shader)
        {
            return HasOverridableGlobalKeyword(shader, "DOTS_INSTANCING_ON");
        }

        public static bool HasLODFadeKeyword(this Shader shader)
        {
            return HasOverridableGlobalKeyword(shader, "LOD_FADE_CROSSFADE");
        }
    }
}
