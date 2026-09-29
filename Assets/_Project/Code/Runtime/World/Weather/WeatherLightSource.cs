using System.Collections.Generic;
using UnityEngine;

namespace MrMoonlight.World.Weather
{
    /// <summary>Kinds of world light a weather profile can restyle. Owner: MRM-86</summary>
    public enum WeatherLightCategory
    {
        /// <summary>Spotter lamps and dropped lamps.</summary>
        Lamp,
        /// <summary>Flares.</summary>
        Flare,
        /// <summary>The moon prop's glow.</summary>
        MoonGlow,
    }

    /// <summary>
    /// Marks a light that is not the sun and not the player's own (Spotter lamp, dropped lamp, flare, moon glow)
    /// so the active <see cref="WeatherSystem"/> can restyle it per weather. Put it on the Light's GameObject in
    /// the prefab. It remembers the prefab's own values, which are used whenever a profile does not override
    /// the category. Lights spawned at runtime pick up the current weather when they enable.
    ///
    /// <para>Does nothing in a scene without a WeatherSystem (today: every scene but the Lighting Test Scene).</para>
    ///
    /// Owner: MRM-86.
    /// </summary>
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("Mr. Moonlight/World/Weather Light Source")]
    public sealed class WeatherLightSource : MonoBehaviour
    {
        private static readonly List<WeatherLightSource> ActiveSources = new List<WeatherLightSource>();

        [Tooltip("Which profile section restyles this light.")]
        [SerializeField] private WeatherLightCategory category;

        private Light _light;
        private WorldLightSettings _prefabValues;

        /// <summary>Every enabled weather light in the loaded scenes.</summary>
        public static IReadOnlyList<WeatherLightSource> All => ActiveSources;

        public WeatherLightCategory Category => category;

        public Light Light => _light;

        /// <summary>The light's own values from the prefab, used when a profile does not override its category.</summary>
        public WorldLightSettings PrefabValues => _prefabValues;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _prefabValues = new WorldLightSettings();
            _prefabValues.Capture(_light);
        }

        private void OnEnable()
        {
            ActiveSources.Add(this);
            if (WeatherSystem.Active != null)
            {
                WeatherSystem.Active.ApplyTo(this);
            }
        }

        private void OnDisable() => ActiveSources.Remove(this);
    }
}
