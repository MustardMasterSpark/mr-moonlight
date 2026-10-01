using System.Collections.Generic;
using UnityEngine;

namespace MrMoonlight.World.Weather
{
    /// <summary>What a <see cref="WeatherSystemBase"/> needs from a library: how many weathers, and each one.</summary>
    public abstract class WeatherProfileLibraryBase : ScriptableObject
    {
        public abstract int Count { get; }

        public abstract WeatherProfileBase this[int index] { get; }
    }

    /// <summary>
    /// Every weather profile for the HAZE scenes, one per moment of the game, in play order: Story 1-7 then Legion
    /// W1 A1 to W7 A2. Data only. <see cref="WeatherSystem"/> applies and blends them; in the Lighting Test Scene the
    /// "Save Lighting Values" / "Save Fog Values" buttons write back into this asset (editor Play Mode only).
    /// Owner: MRM-86.
    /// </summary>
    [CreateAssetMenu(menuName = "MrMoonlight/Weather Profile Library", fileName = "WeatherProfiles")]
    public sealed class WeatherProfileLibrary : WeatherProfileLibraryBase
    {
        [Tooltip("One entry per weather. Open an entry to edit its sky, light and fog; in Play Mode the scene "
                 + "follows your edits live while you stand in that weather.")]
        public List<WeatherProfile> Profiles = new List<WeatherProfile>();

        public override int Count => Profiles.Count;

        public override WeatherProfileBase this[int index] => Profiles[index];
    }
}
