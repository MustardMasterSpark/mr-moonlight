namespace MrMoonlight.World.Weather
{
    /// <summary>
    /// What a <see cref="WeatherSystemBase"/> needs from a fog asset: read the scene's fog into a profile, write a
    /// blend of two profiles to the scene. One implementation per fog asset (<see cref="HazeFogAdapter"/>,
    /// <see cref="Vf2FogAdapter"/>); each knows only its own profile type.
    /// </summary>
    public interface IFogAdapter
    {
        /// <summary>Reads what the fog objects show right now into the profile's fog section.</summary>
        void Capture(WeatherProfileBase profile);

        /// <summary>Writes a blend of two profiles' fog (t = 0 is a, 1 is b).</summary>
        void ApplyBlend(WeatherProfileBase a, WeatherProfileBase b, float t);
    }
}
