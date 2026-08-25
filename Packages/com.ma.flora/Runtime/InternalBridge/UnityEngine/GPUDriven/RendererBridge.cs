// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEngine;

namespace MA.InternalBridge.GPUDriven
{
    internal static class RendererBridge
    {
        public static void SetAllowGPUDrivenRendering(this Renderer renderer, bool value)
        {
            renderer.allowGPUDrivenRendering = value;
        }

        public static bool HasSmallMeshCulling(this Renderer renderer)
        {
            return renderer.smallMeshCulling;
        }

        public static void SetSmallMeshCulling(this Renderer renderer, bool value)
        {
            renderer.smallMeshCulling = value;
        }
    }
}
