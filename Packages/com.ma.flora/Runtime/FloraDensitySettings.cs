// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    /// <summary>
    /// Modes for how range density settings affect objects.
    /// </summary>
    public enum FloraDensityMode
    {
        /// <summary>
        /// The density setting does not affect any objects.
        /// </summary>
        Disabled,
        /// <summary>
        /// The density setting affects only renderers, not LODGroups.
        /// </summary>
        RenderersOnly,
        /// <summary>
        /// The density setting affects both renderers and LODGroups.
        /// </summary>
        RenderersAndLODGroups
    }

    /// <summary>
    /// A <see cref="VolumeParameter{T}"/> that stores a pair of float values (X, Y) representing
    /// a screen-space coverage range, used to control culling or density adjustments.
    /// </summary>
    /// <remarks>
    /// The values typically define how large an instance must appear on screen before
    /// certain culling or density logic is applied. For example, if <c>X</c> is 0.1, that
    /// might represent 10% screen coverage as the “start” threshold, while <c>Y</c> could
    /// be 0.0 for a “fully distant” threshold.
    /// </remarks>
    [Serializable]
    public class FloraScreenRangeParameter : FloatRangeParameter
    {
        public override Vector2 value
        {
            get => m_Value;
            set
            {
                float rangeMax = Mathf.Clamp(value.x, max, min);
                float rangeMin = Mathf.Clamp(value.y, max, min);
                if (rangeMax < rangeMin)
                    (rangeMax, rangeMin) = (rangeMin, rangeMax);

                m_Value = new Vector2(rangeMax, rangeMin);
            }
        }

        /// <summary>
        /// Creates a new <see cref="FloraScreenRangeParameter"/>.
        /// </summary>
        /// <param name="value">Initial X, Y values (screen coverage range).</param>
        /// <param name="min">Minimum allowable value.</param>
        /// <param name="max">Maximum allowable value.</param>
        /// <param name="overrideState">Whether this parameter is currently overriding lower-priority volumes.</param>
        public FloraScreenRangeParameter(Vector2 value, float min, float max, bool overrideState = false)
            : base(value, min, max, overrideState)
        {
        }
    }

    /// <summary>
    /// A <see cref="VolumeComponent"/> that controls how density-based culling is applies to instances.
    /// </summary>
    /// <remarks>
    /// This component provides two broad density control options:
    /// <list type="bullet">
    /// <item><description><b>Global Density</b> – Reduces the overall count of instances.</description></item>
    /// <item><description><b>Range-Based Density</b> – Dynamically adjusts instance density based on their screen coverage.</description></item>
    /// </list>
    /// </remarks>
    [VolumeComponentMenu("Flora/Density Settings")]
    [HelpURL("https://flora.magneticarcade.com/scripts/density-settings")]
    public class FloraDensitySettings : VolumeComponent
    {
        /// <summary>
        /// Determines how global density settings affect objects.
        /// </summary>
        [Tooltip("Determines how global density settings affect objects.")]
        public EnumParameter<FloraDensityMode> GlobalDensityMode = new(FloraDensityMode.Disabled);

        /// <summary>
        /// A layer mask restricting which instance layers are subject to global density culling.
        /// </summary>
        /// <remarks>
        /// Only instances in these layers will have their overall count reduced based on <see cref="GlobalDensity"/>.
        /// </remarks>
        [Tooltip("Select which layers are affected by global density culling.")]
        public LayerMaskParameter GlobalDensityMask = new(-1);

        /// <summary>
        /// The fraction of instances that remain visible under global density culling.
        /// </summary>
        [Tooltip("Specifies the fraction of instances to keep when applying global density culling.")]
        public ClampedFloatParameter GlobalDensity = new(0.75f, 0.0f, 1.0f);

        /// <summary>
        /// The maximum size (in meters) for objects to be affected by global density.
        /// </summary>
        /// <remarks>
        /// If an instance’s diagonal size exceeds this threshold, it is not culled to prevent large objects from disappearing.
        /// </remarks>
        [Tooltip("Maximum bounding size for an object to be affected by global density.")]
        public MinFloatParameter GlobalDensitySizeThreshold = new(2.0f, 0.0f);

        /// <summary>
        /// Determines how range-based density settings affect objects.
        /// </summary>
        [Tooltip("Determines how range-based density settings affect objects.")]
        public EnumParameter<FloraDensityMode> RangeDensityMode = new(FloraDensityMode.Disabled);

        /// <summary>
        /// A layer mask restricting which instance layers are subject to range-based density culling.
        /// </summary>
        /// <remarks>
        /// Only instances in these layers will have their density reduced if they occupy a small percentage of the screen
        /// (as defined by <see cref="RangeDensityScreenPercentage"/>).
        /// </remarks>
        [Tooltip("Select which layers are affected by range-based density culling.")]
        public LayerMaskParameter RangeDensityMask = new(-1);

        /// <summary>
        /// The fraction of instances to keep when the instance is at or below the smallest
        /// screen coverage in <see cref="RangeDensityScreenPercentage"/>.
        /// </summary>
        /// <remarks>
        /// A value of 0.1f means only 10% of those small-on-screen instances remain.
        /// </remarks>
        [Tooltip("Specifies the fraction of instances to keep at the far (smallest coverage) end of range-based density.")]
        public ClampedFloatParameter RangeDensity = new(0.1f, 0.0f, 1.0f);

        /// <summary>
        /// The exponent that governs how quickly density transitions to the <see cref="RangeDensity"/> value.
        /// </summary>
        /// <remarks>
        /// A higher value causes a steeper falloff curve—instances quickly drop in density as they
        /// become smaller on screen. A lower value yields a gentler transition.
        /// </remarks>
        [Tooltip("Controls how sharply the density transitions occur between full and RangeDensity.")]
        public ClampedFloatParameter RangeDensityFalloff = new(0.5f, 0.0f, 1.0f);

        /// <summary>
        /// Defines the range of the screen (by percentage) over which range-based density culling is applied.
        /// </summary>
        /// <remarks>
        /// The lower bound of the range is defined by <see cref="FloraRenderSettings.MinScreenSize"/>.
        /// </remarks>
        [Tooltip("Specifies the screen coverage thresholds for range-based density culling. The lower bound is defined by the Minimum Screen Size.")]
        public FloraScreenRangeParameter RangeDensityScreenPercentage = new(new Vector2(0.05f, 0.0f), 0.15f, 0.0f);

        #region Obsolete

        [Obsolete("Use GlobalDensityMode instead. This property will be removed in a future release.", false)]
        public BoolParameter GlobalDensityEnabled = new(false);

        [Obsolete("Use RangeDensityMode instead. This property will be removed in a future release.", false)]
        public BoolParameter RangeDensityEnabled = new(false);

        [Obsolete("Use RangeDensityMode instead. This property will be removed in a future release.", false)]
        public BoolParameter RangeDensityAffectsLODGroups = new(false);

        [Obsolete("Use RangeDensityFalloff instead. This property will be removed in a future release.", false)]
        public ClampedFloatParameter RangeDensityFalloffPower = new(0.5f, 0.0f, 1.0f);

        private void Awake()
        {
#pragma warning disable 618
            if (GlobalDensityEnabled.overrideState && GlobalDensityEnabled.value)
            {
                GlobalDensityMode.value = FloraDensityMode.RenderersOnly;
                GlobalDensityMode.overrideState = true;
                GlobalDensityEnabled.overrideState = false;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }

            if (RangeDensityEnabled.overrideState && RangeDensityEnabled.value)
            {
                RangeDensityMode.value = RangeDensityAffectsLODGroups.value ? FloraDensityMode.RenderersAndLODGroups : FloraDensityMode.RenderersOnly;
                RangeDensityMode.overrideState = true;
                RangeDensityEnabled.overrideState = false;
                RangeDensityAffectsLODGroups.overrideState = false;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
#pragma warning restore 618
        }

        #endregion
    }
}
