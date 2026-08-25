// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    /// <summary>
    /// Modes for how the minimum screen size setting affects objects.
    /// </summary>
    public enum FloraMinimumScreenSizeMode
    {
        /// <summary>
        /// The minimum screen size setting does not affect any objects.
        /// </summary>
        Disabled,
        /// <summary>
        /// The minimum screen size setting affects only renderers, not LODGroups.
        /// </summary>
        RenderersOnly,
        /// <summary>
        /// The minimum screen size setting affects both renderers and LODGroups.
        /// </summary>
        RenderersAndLODGroups
    }

    /// <summary>
    /// A volume parameter that holds a float value representing a fraction of the screen size, clamped between a minimum and maximum.
    /// </summary>
    [Serializable]
    public class FloraScreenSizeParameter : ClampedFloatParameter
    {
        /// <summary>
        /// Creates a new <see cref="FloraScreenSizeParameter"/> with the given value, minimum, maximum, and override state.
        /// </summary>
        /// <param name="value">The initial value of the parameter.</param>
        /// <param name="min">The minimum allowable value (clamped between 0 and 1).</param>
        /// <param name="max">The maximum allowable value (clamped between 0 and 1).</param>
        /// <param name="overrideState">The initial override state of the parameter.</param>
        public FloraScreenSizeParameter(float value, float min, float max, bool overrideState = false)
            : base(value, Mathf.Clamp01(min), Mathf.Clamp01(max), overrideState)
        {
        }
    }

    /// <summary>
    /// A <see cref="VolumeComponent"/> that controls instance culling and rendering bounds within Flora.
    /// </summary>
    /// <remarks>
    /// Attach this component to a volume to override scene-wide culling parameters.
    /// </remarks>
    [VolumeComponentMenu("Flora/Render Settings")]
    [HelpURL("https://flora.magneticarcade.com/scripts/render-settings")]
    public class FloraRenderSettings : VolumeComponent
    {
        /// <summary>
        /// The maximum distance at which instances are rendered.
        /// </summary>
        /// <remarks>
        /// Any instance beyond this distance will be culled.
        /// A value of 0 means there is no distance-based limit (unless overridden by other volume or object-level settings).
        /// </remarks>
        [Tooltip("The maximum distance for instance rendering. 0 means unlimited, unless overridden.")]
        public MinFloatParameter MaxRenderDistance = new(0.0f, 0.0f);

        /// <summary>
        /// Furthest distance (metres) shadows may be drawn (0 = pipeline shadow distance).
        /// </summary>
        /// <remarks>
        /// Similar to <see cref="MaxRenderDistance"/>, but specifically for shadows.
        /// </remarks>
        [Tooltip("The maximum distance for instance shadows. 0 means unlimited, unless overridden.")]
        public MinFloatParameter MaxShadowDistance = new(0.0f, 0.0f);

        /// <summary>
        /// Determines how the minimum screen size setting affects objects.
        /// </summary>
        [Tooltip("Determines how the minimum screen size setting affects objects.")]
        public EnumParameter<FloraMinimumScreenSizeMode> MinScreenSizeMode = new(FloraMinimumScreenSizeMode.RenderersOnly);

        /// <summary>
        /// The smallest fraction of the screen that an instance can occupy before it’s culled.
        /// </summary>
        /// <remarks>
        /// If an instance’s projected size on screen is less than <see cref="MinScreenSize"/>, it will be culled entirely.
        /// This saves rendering cost for tiny or distant objects.
        /// </remarks>
        [Tooltip("The smallest fraction of the screen that an instance can occupy before being culled.")]
        public FloraScreenSizeParameter MinScreenSize = new(0.005f, 0.0f, 0.1f);

        /// <summary>
        /// Lowest LOD index allowed to cast shadows (0 = full detail).
        /// </summary>
        /// <remarks>
        /// Higher values force less detailed shadow LODs. If higher than 0, only LODs at or above this index will be used for shadows.
        /// </remarks>
        [Tooltip("Determines the lowest available LOD index for rendering shadows. Higher values force less detailed shadows LODs.")]
        public ClampedIntParameter MinShadowLOD = new(0, 0, 7);

        /// <summary>
        /// A random height value added to each LOD transition height, creating a randomized LOD transition threshold for each instance.
        /// </summary>
        /// <remarks>
        /// Used to add a bit of randomness to the LOD transitions, preventing them from being too uniform as the camera moves.
        /// Helps reduce uniform popping and create a less noticeable transition.
        /// </remarks>
        [Tooltip("A random height value added to each LOD transition height, creating a randomized LOD transition threshold for each instance.")]
        public ClampedFloatParameter RandomizeLODTransition = new(0.0f, 0f, 1.0f);

        /// <summary>
        /// The duration of cross-fade transitions between LODs for <see cref="LODGroup"/>'s with the animated flag enabled.
        /// </summary>
        [Tooltip("The time in seconds for cross-fade transitions between LODs.")]
        public ClampedFloatParameter CrossFadeDuration = new(0.3f, 0.001f, 1.0f);

        #region Obsolete

        [Obsolete("Use MinScreenSizeMode instead. This property will be removed in a future release.", false)]
        public BoolParameter MinScreenSizeAffectsLODGroups = new(false);

        private void Awake()
        {
#pragma warning disable 618
            // Migrate old MinScreenSizeAffectsLODGroups to new MinScreenSizeMode
            if (MinScreenSizeAffectsLODGroups.overrideState && MinScreenSizeAffectsLODGroups.value)
            {
                MinScreenSizeMode.value = FloraMinimumScreenSizeMode.RenderersAndLODGroups;
                MinScreenSizeMode.overrideState = true;
                MinScreenSizeAffectsLODGroups.overrideState = false;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
#pragma warning restore 618
        }

        #endregion
    }
}
