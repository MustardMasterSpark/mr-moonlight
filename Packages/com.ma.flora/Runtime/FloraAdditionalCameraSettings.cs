// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    /// <summary>
    /// Provides additional settings for controlling how instances are rendered by a camera.
    /// Attach this component alongside a <see cref="Camera"/> to override settings on a per-camera basis.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Flora/Flora Additional Camera Settings")]
    [Icon("Packages/com.ma.flora/Editor/EditorResources/Icons/AdditionalCameraSettings Icon.png")]
    [HelpURL("https://flora.magneticarcade.com/scripts/additional-camera-settings")]
    public sealed class FloraAdditionalCameraSettings : MonoBehaviour, IAdditionalData
    {
        /// <summary>
        /// If <c>true</c>, this camera can participate in GPU occlusion culling when rendering instances.
        /// </summary>
        [Tooltip("If enabled, GPU occlusion culling can be used on this camera.")]
        public bool AllowGPUOcclusionCulling = true;

        /// <summary>
        /// Completely disables instance rendering for this camera.
        /// </summary>
        [Tooltip("Disable all instancing for this camera.")]
        public bool DisableInstanceRendering;

        /// <summary>
        /// Multiplier applied to LOD distance tests for this camera (<c>1.0</c> = default behaviour).
        /// </summary>
        [Range(0.001f, 3.0f), Tooltip("Scale applied to LOD calculations for this camera.")]
        public float LODBiasScale = 1.0f;

        /// <summary>
        /// Tells the camera that the camera just teleported to a new position, which will reset the LOD transition timer.
        /// Can be used to ensure that the camera does not show LOD transitions immediately after teleporting.
        /// (e.g. when using a teleportation system in VR or a cinematic cut).
        /// </summary>
        /// <remarks>
        /// The value will be reset to <c>false</c> after the camera has processed the teleport.
        /// </remarks>
        [Tooltip("When set, the camera will reset the LOD transition timer when it teleports to a new position.")]
        public bool Teleported = false;
    }
}
