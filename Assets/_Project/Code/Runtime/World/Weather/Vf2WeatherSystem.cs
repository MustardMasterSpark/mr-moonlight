using UnityEngine;
using VolumetricFogAndMist2;

namespace MrMoonlight.World.Weather
{
    /// <summary>
    /// The weather system for scenes with <b>Volumetric Fog &amp; Mist 2</b> (AST-282, the fog experiment):
    /// <see cref="Vf2WeatherProfile"/>s from a <see cref="Vf2WeatherProfileLibrary"/>, fog through
    /// <see cref="Vf2FogAdapter"/>. Everything else is <see cref="WeatherSystemBase"/>. Holds no HAZE data; the HAZE
    /// scenes use <see cref="WeatherSystem"/> and the original library.
    ///
    /// Owner: MRM-85/86.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/World/Weather System (Volumetric Fog 2)")]
    public sealed class Vf2WeatherSystem : WeatherSystemBase
    {
        [Header("CONTROL BOARD (Play Mode): edit here, the scene changes live. Save copies it to the current profile.")]
        [Tooltip("The values being applied to the scene right now. Not the owner of anything: each section is written "
                 + "straight to the real sun, ambient, lights and fog. Reloaded from the profile when a blend locks.")]
        [SerializeField] private Vf2WeatherProfile live = new Vf2WeatherProfile();

        [Header("Volumetric Fog & Mist 2")]
        [Tooltip("The weather profiles (the VF2 library, never the HAZE one).")]
        [SerializeField] private Vf2WeatherProfileLibrary library;

        [Tooltip("The Volumetric Fog & Mist 2 volume this weather drives.")]
        [SerializeField] private VolumetricFog fogVolume;

        protected override WeatherProfileBase Live => live;

        protected override WeatherProfileLibraryBase Lib => library;

        protected override WeatherProfileBase NewProfile() => new Vf2WeatherProfile();

        protected override IFogAdapter CreateFogAdapter() => new Vf2FogAdapter(fogVolume);
    }
}
