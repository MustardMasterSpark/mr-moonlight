// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;
#if !UNITY_6000_1_OR_NEWER
using System.Collections.Generic;
using UnityEditor.ShortcutManagement;
#endif

namespace MA.Flora.Editor.InternalBridge
{
    internal static class SceneViewBridge
    {
        public static void EnableRectSelector(this SceneView sceneView)
        {
#if UNITY_6000_1_OR_NEWER
            sceneView.rectSelection.RegisterShortcutContext();
#else
            ShortcutIntegration.instance.contextManager.RegisterToolContext(new SceneViewPickingShortcutContext());
#endif
        }

        public static void DisableRectSelector(this SceneView sceneView)
        {
#if UNITY_6000_1_OR_NEWER
            sceneView.rectSelection.UnregisterShortcutContext();
#else
            var contextType = ShortcutIntegration.instance.contextManager.GetType();
            var toolContextsField = contextType.GetField("m_ToolContexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (toolContextsField == null)
                return;

            var toolContexts = (List<IShortcutContext>)toolContextsField.GetValue(ShortcutIntegration.instance.contextManager);
            var contextIndex = toolContexts.FindIndex(c => c is SceneViewPickingShortcutContext);
            if (contextIndex == -1)
                return;

            ShortcutIntegration.instance.contextManager.DeregisterToolContext(toolContexts[contextIndex]);
#endif
        }
    }
}
