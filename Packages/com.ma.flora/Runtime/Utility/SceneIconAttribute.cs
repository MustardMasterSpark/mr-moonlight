// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Diagnostics;

namespace MA.Flora
{
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Class)]
    internal class SceneIconAttribute : Attribute
    {
        /// <summary>
        /// The path to the custom icon in the Unity project.
        /// </summary>
        public readonly string Path;

        /// <summary>
        /// Creates a new attribute that specifies a custom icon for a scene object.
        /// </summary>
        /// <param name="path">The path to the icon in the Unity project.</param>
        public SceneIconAttribute(string path = null)
        {
            Path = path;
        }
    }
}
