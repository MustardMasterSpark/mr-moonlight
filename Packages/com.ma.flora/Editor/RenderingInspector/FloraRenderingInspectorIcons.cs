// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEngine;

namespace MA.Flora.Editor
{
    internal static class FloraRenderingInspectorIcons
    {
        public static string GetStyleClass(FloraRenderingInspectorNode node)
        {
            if (node == null)
                return "overview";

            if (node.Kind == FloraRenderingInspectorNodeKind.Source)
                return "source";

            if (node.Kind == FloraRenderingInspectorNodeKind.Root)
                return string.IsNullOrEmpty(node.IconClass) ? "group" : node.IconClass;

            return string.IsNullOrEmpty(node.IconClass) ? "source" : node.IconClass;
        }

        public static bool TryGetThumbnail(FloraRenderingInspectorNode node, EditorIconSize size, out Texture2D thumbnail)
        {
            thumbnail = null;
            return node?.UseTargetThumbnail == true && node.Target && EditorIcons.TryGetThumbnail(node.Target, size, out thumbnail);
        }
    }
}
