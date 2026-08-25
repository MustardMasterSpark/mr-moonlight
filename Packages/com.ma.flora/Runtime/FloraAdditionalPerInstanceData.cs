// Copyright © Magnetic Arcade. All Rights Reserved.

using System;

namespace MA.Flora
{
    /// <summary>
    /// Specifies which additional per-instance data is allocated for instances of a prefab with <see cref="FloraAdditionalRendererSettings"/>.
    /// </summary>
    [Flags]
    public enum FloraAdditionalPerInstanceData
    {
        /// <summary>
        /// No additional per-instance data is allocated.
        /// </summary>
        None = 0,
        /// <summary>
        /// Each instance will allocate a unique, stable random <c>float</c> value (0-1) based on the instance's handle.
        /// This value is accessible in shaders via the <c>flora_RandomID</c> DOTS property, or the `InstanceRandomID` node in Shader Graph.
        /// </summary>
        RandomID = 1 << 0,
        /// <summary>
        /// Each instance will have an accessible color <c>float4</c> value assigned to it that is unique per-instance.
        /// This value is accessible in shaders via the <c>flora_VariationColor</c> DOTS property, or the `InstanceVariationColor` node in Shader Graph.
        /// </summary>
        VariationColor = 1 << 1,
    }
}
