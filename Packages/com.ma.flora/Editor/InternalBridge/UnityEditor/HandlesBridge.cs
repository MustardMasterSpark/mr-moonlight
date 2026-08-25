// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;
using UnityEngine;

namespace MA.Flora.Editor.InternalBridge
{
    internal static class HandlesBridge
    {
        internal static bool IsSceneCameraFiltered(this Camera camera)
        {
            return Handles.GetCameraFilterMode(camera) == Handles.CameraFilterMode.ShowFiltered;
        }

        internal static void LockHandlePosition()
        {
            Tools.LockHandlePosition();
        }

        internal static void LockHandlePosition(Vector3 position)
        {
            Tools.LockHandlePosition(position);
        }

        internal static void UnlockHandlePosition()
        {
            Tools.UnlockHandlePosition();
        }
    }
}
