// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;

namespace MA.Flora.Editor.InternalBridge
{
    internal static class SelectionBridge
    {
        public static void SetSelectionWithActiveObject(UnityEngine.Object[] newSelection, UnityEngine.Object activeObject)
        {
            Selection.SetSelectionWithActiveObject(newSelection, activeObject);
        }
    }
}
