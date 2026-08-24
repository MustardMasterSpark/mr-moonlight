using System;
using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// A snapshot of the Sun's tunable state. Elevation/azimuth, not world position — a
    /// directional light's contribution depends entirely on its rotation, never on the
    /// GameObject's transform position, so "where the sun is" is expressed as an angle pair
    /// here rather than a Vector3. Shared between <see cref="SunController"/> (which applies
    /// it) and <see cref="TimeManager"/>'s presets (which store named instances of it).
    /// Owner: MRM-47
    /// </summary>
    [Serializable]
    public struct SunState
    {
        /// <summary>Sun height above the horizon, in degrees. 90 = directly overhead, 0 = on the horizon, negative = below it (night).</summary>
        [Tooltip("Sun height above the horizon, in degrees. 90 = overhead, 0 = horizon, negative = below it (night).")]
        public float Elevation;

        /// <summary>Compass heading the light is coming from, in degrees.</summary>
        [Tooltip("Compass heading the light comes from, in degrees.")]
        public float Azimuth;

        public Color Color;
        public float Intensity;

        /// <summary>If true, ColorTemperature drives the light's color instead of Color — mirrors the Light component's own Use Color Temperature toggle.</summary>
        [Tooltip("If true, Color Temperature drives the light's color instead of Color.")]
        public bool UseColorTemperature;
        public float ColorTemperature;

        /// <summary>Matches the Island scene's Directional Light as found on 2026-08-24, before any of MRM-47/MRM-69's systems touched it — the safe "nothing changed yet" default.</summary>
        public static SunState Default => new SunState
        {
            Elevation = 50f,
            Azimuth = 330f,
            Color = new Color(1f, 0.9568627f, 0.8392157f),
            Intensity = 1f,
            UseColorTemperature = false,
            ColorTemperature = 6570f
        };
    }
}
