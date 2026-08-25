// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MA.Flora
{
    /// <summary>
    /// Provides additional rendering options for rendering instances of a prefab in the scene.
    /// Attach this component to a prefab to override default rendering options on a per-object basis.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Flora/Flora Additional Renderer Settings")]
    [Icon("Packages/com.ma.flora/Editor/EditorResources/Icons/AdditionalRendererSettings Icon.png")]
    [HelpURL("https://flora.magneticarcade.com/scripts/additional-renderer-settings")]
    public sealed class FloraAdditionalRendererSettings : MonoBehaviour, ISerializationCallbackReceiver
    {
        /// <summary>
        /// Specifies what additional per-instance data is allocated for instances of this prefab.
        /// </summary>
        [Tooltip("Specifies which additional per-instance data is allocated for these instances.")]
        public FloraAdditionalPerInstanceData AdditionalPerInstanceData;

        /// <summary>
        /// When the <see cref="FloraAdditionalPerInstanceData.VariationColor"/> is enabled, this color is used as the initial value for instances of this prefab.
        /// Individual instances can override this color using a <see cref="FloraSystem.SetInstanceVariationColor"/> call.
        /// If the <see cref="FloraAdditionalPerInstanceData.VariationColor"/> flag is not enabled, this value is ignored.
        /// </summary>
        [ColorUsage(false, true), Tooltip("Initial variation color for instances of this prefab. Ignored if VariationColor is not enabled.")]
        [FormerlySerializedAs("DefaultVariantColor")]
        public Color InitialVariationColor = Color.white;

        /// <summary>
        /// Sets a per-prefab <b>render</b> distance cap for these instances.
        /// This value is composed with scene/volume distance caps (the smallest non-zero cap wins).
        /// Use <c>0</c> to leave this cap disabled.
        /// </summary>
        [Min(0), Tooltip("Nonzero = additional max render distance cap for this prefab. Zero = no prefab cap.")]
        public float MaxRenderDistance;

        /// <summary>
        /// Sets a per-prefab <b>shadow</b> distance cap for these instances.
        /// This value is composed with scene/volume shadow caps (the smallest non-zero cap wins).
        /// Use <c>0</c> to leave this cap disabled.
        /// </summary>
        [Min(0), Tooltip("Nonzero = additional max shadow distance cap for this prefab. Zero = no prefab cap.")]
        public float MaxShadowDistance;

        /// <summary>
        /// Lowest LOD index that may be selected when drawing shadows for these instances.
        /// (<c>0</c> = most detailed).
        /// </summary>
        [Range(0, 7), Tooltip("Controls the minimum LOD used for instance shadows (0 = highest detail).")]
        public int MinShadowLOD;

        /// <summary>
        /// When enabled, instances respect any <see cref="FloraDensitySettings.GlobalDensity"/> controls.
        /// </summary>
        [Tooltip("Whether these instances are culled or reduced based on global and volume-based density.")]
        public bool AffectedByGlobalDensity = true;

        /// <summary>
        /// When enabled, instances respect any <see cref="FloraDensitySettings.RangeDensity"/> controls.
        /// </summary>
        [Tooltip("If true, these instances are influenced by range-based density settings in the scene or volumes.")]
        public bool AffectedByRangeDensity = true;

        /// <summary>
        /// When enabled, instances can be culled by the <see cref="FloraRenderSettings.MinScreenSize"/>.
        /// </summary>
        [Tooltip("If true, these instances are influenced by range-based density settings in the scene or volumes.")]
        public bool AffectedByMinimumScreenSize = true;

        #region Obsolete

        [Obsolete("Use AdditionalPerInstanceData instead."), HideInInspector]
        public bool RequiresPerInstanceRandomID = false;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
#pragma warning disable 618
            if (RequiresPerInstanceRandomID)
            {
                AdditionalPerInstanceData |= FloraAdditionalPerInstanceData.RandomID;
                RequiresPerInstanceRandomID = false;
            }
#pragma warning restore 618
        }

        #endregion
    }
}
