// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace MA.Flora.Editor.InternalBridge
{
    internal static class EditorGUIUtilityBridge
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Texture2D LoadIconRequired(string path)
        {
            return EditorGUIUtility.LoadIconRequired(path);
        }
    }
}
