// Copyright © Magnetic Arcade. All Rights Reserved.

#if HAS_PACKAGE_UNITY_URP
using UnityEngine.Rendering.Universal;

namespace MA.InternalBridge
{
    internal static class UniversalBridge
    {
        internal static bool HasDepthPriming(this ScriptableRenderer renderer)
        {
            return renderer.useDepthPriming;
        }

        internal static RenderingMode GetActualRenderingPath(this UniversalRenderer renderer)
        {
            return renderer.renderingModeActual;
        }

        internal static int GetXrCompatibleScreenWidth(this UniversalCameraData cameraData)
        {
            return (int)(cameraData.pixelWidth * cameraData.renderScale);
        }

        internal static int GetXrCompatibleScreenHeight(this UniversalCameraData cameraData)
        {
            return (int)(cameraData.pixelHeight * cameraData.renderScale);
        }
    }
}
#endif
