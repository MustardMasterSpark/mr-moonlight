// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;

namespace MA.Flora.Editor.InternalBridge
{
    internal static class ToolsBridge
    {
        public static bool IsVertexDragging() => Tools.vertexDragging;
    }
}
