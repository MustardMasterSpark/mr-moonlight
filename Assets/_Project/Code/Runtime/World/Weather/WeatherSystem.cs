using Haze.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace MrMoonlight.World.Weather
{
    /// <summary>
    /// The weather system for scenes with the <b>HAZE</b> fog (AST-078): <see cref="WeatherProfile"/>s from a
    /// <see cref="WeatherProfileLibrary"/>, fog through <see cref="HazeFogAdapter"/>. Everything else (sky, sun,
    /// lights, blending, the control board, saving) is <see cref="WeatherSystemBase"/>. The Volumetric Fog &amp; Mist 2
    /// scenes use <see cref="Vf2WeatherSystem"/> and its own library instead; nothing here knows that asset.
    ///
    /// Owner: MRM-86.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/World/Weather System")]
    public sealed class WeatherSystem : WeatherSystemBase
    {
        [Header("CONTROL BOARD (Play Mode): edit here, the scene changes live. Save copies it to the current profile.")]
        [Tooltip("The values being applied to the scene right now. Not the owner of anything: each section is written "
                 + "straight to the real sun, ambient, lights and fog. Reloaded from the profile when a blend locks.")]
        [SerializeField] private WeatherProfile live = new WeatherProfile();

        [Header("HAZE")]
        [Tooltip("The weather profiles.")]
        [SerializeField] private WeatherProfileLibrary library;

        [Tooltip("The global Volume carrying the HAZE Global Fog override.")]
        [SerializeField] private Volume fogVolume;

        [Tooltip("The HAZE density box covering the playable area (optional).")]
        [SerializeField] private HazeDensityVolume areaFog;

        protected override WeatherProfileBase Live => live;

        protected override WeatherProfileLibraryBase Lib => library;

        protected override WeatherProfileBase NewProfile() => new WeatherProfile();

        protected override IFogAdapter CreateFogAdapter()
        {
            HazeGlobalFogVolumeComponent global = null;
            HazeOverridesVolumeComponent noise = null;
            if (fogVolume != null)
            {
                // .profile makes a runtime copy, so Play Mode never edits the profile asset.
                VolumeProfile profile = fogVolume.profile;
                profile.TryGet(out global);
                if (!profile.TryGet(out noise))
                {
                    noise = profile.Add<HazeOverridesVolumeComponent>();
                    noise.active = false;
                }
            }

            return new HazeFogAdapter(global, noise, areaFog);
        }
    }
}
