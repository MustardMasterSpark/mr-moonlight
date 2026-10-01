using System.Collections.Generic;
using UnityEngine;

namespace MrMoonlight.World.Weather
{
    /// <summary>
    /// The same list of weathers for the Volumetric Fog &amp; Mist 2 scenes (fog experiment, AST-282): each entry's fog
    /// is VF2 values only. Read and saved by <see cref="Vf2WeatherSystem"/>; the HAZE library is never touched.
    /// Owner: MRM-85/86.
    /// </summary>
    [CreateAssetMenu(menuName = "MrMoonlight/Weather Profile Library (Volumetric Fog 2)", fileName = "WeatherProfiles_VF2")]
    public sealed class Vf2WeatherProfileLibrary : WeatherProfileLibraryBase
    {
        [Tooltip("One entry per weather. Open an entry to edit its sky, light and fog; in Play Mode the scene "
                 + "follows your edits live while you stand in that weather.")]
        public List<Vf2WeatherProfile> Profiles = new List<Vf2WeatherProfile>();

        public override int Count => Profiles.Count;

        public override WeatherProfileBase this[int index] => Profiles[index];
    }
}
