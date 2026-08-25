// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;

namespace MA.Flora.Editor.InternalBridge
{
    internal static class EditorSnapSettingsBridge
    {
        public static bool IsVertexSnapActive() => EditorSnapSettings.vertexSnapActive;
    }
}
