using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MrMoonlight.World.Weather
{
    /// <summary>
    /// One weather condition: what the sky, the light and the fog look like at one moment of the game (a story
    /// part or a Legion act). Lives in a <see cref="WeatherProfileLibrary"/>; applied and blended by
    /// <see cref="WeatherSystem"/>.
    ///
    /// <para>Today a profile holds the sky, the sun, the environment (ambient) light, the world light sources
    /// that are not the player's own, and the fog. Rain, wind, particles, audio and the player's own two lights
    /// (personal light, hands' light) are meant to become further sections here later.</para>
    ///
    /// <para><b>The player's own lights are deliberately NOT in a profile</b> (flashlight, PlayerAmbientLight,
    /// the hands' ViewModelLight, weapon muzzle flashes): Carlos, 2026-09-29.</para>
    ///
    /// Owner: MRM-86.
    /// </summary>
    [Serializable]
    public sealed class WeatherProfile
    {
        [Tooltip("The moment of the game this weather belongs to (story part or Legion act). Shown as the list entry's title.")]
        public string Name = "New weather";

        [Tooltip("Skybox/Cubemap material for this weather.")]
        public Material Skybox;

        [Tooltip("The scene's global directional light (the SUN).")]
        public SunLightSettings Sun = new SunLightSettings();

        [Tooltip("Ambient light and reflections (Lighting window > Environment).")]
        public EnvironmentLightSettings Environment = new EnvironmentLightSettings();

        // Special light sources: individual lights in the world, NOT the sun or the ambient. Each category has an
        // Override switch (off = the lights keep their prefab values). The player's own lights (personal light,
        // hands' light) will join this group later (Carlos, 2026-09-29).
        [Header("* Special light sources (not the sun) — Override off = prefab values")]
        [Tooltip("* Special light source. Spotter lamps and dropped lamps (lights tagged WeatherLightSource: Lamp).")]
        public WorldLightSettings Lamps = new WorldLightSettings();

        [Tooltip("* Special light source. Flares (lights tagged WeatherLightSource: Flare).")]
        public WorldLightSettings Flares = new WorldLightSettings();

        [Tooltip("* Special light source. The moon prop's glow (lights tagged WeatherLightSource: MoonGlow).")]
        public WorldLightSettings MoonGlow = new WorldLightSettings();

        [Header("Fog")]
        [Tooltip("Fog. HAZE-shaped for now: this section may change if the fog asset changes "
                 + "(Volumetric Fog & Mist 2 is being considered for sandstorms).")]
        public FogSettings Fog = new FogSettings();

        /// <summary>The world-light section for a category.</summary>
        public WorldLightSettings GetWorldLights(WeatherLightCategory category)
        {
            switch (category)
            {
                case WeatherLightCategory.Flare: return Flares;
                case WeatherLightCategory.MoonGlow: return MoonGlow;
                default: return Lamps;
            }
        }

        /// <summary>A deep copy (material references are shared, not copied).</summary>
        public WeatherProfile Clone() => JsonUtility.FromJson<WeatherProfile>(JsonUtility.ToJson(this));
    }

    /// <summary>Everything that describes the global directional light. Field defaults are Unity's own new-light defaults. Owner: MRM-86</summary>
    [Serializable]
    public sealed class SunLightSettings
    {
        [Tooltip("Height of the sun above the horizon, degrees. 90 = overhead, 0 = horizon, negative = below it.")]
        [Range(-90f, 90f)] public float Elevation = 50f;

        [Tooltip("Compass heading the light comes from, degrees.")]
        [Range(0f, 360f)] public float Azimuth = 330f;

        [Tooltip("Filter colour of the light (multiplied by the temperature colour when that is used).")]
        public Color Color = new Color(1f, 0.9568627f, 0.8392157f); // Unity's default directional light

        [Tooltip("Tint the light by a colour temperature (Kelvin) as well as the colour above.")]
        public bool UseColorTemperature;

        [Tooltip("Colour temperature, Kelvin. Low = warm/orange, 6500 = neutral, high = cold/blue.")]
        [Range(1000f, 20000f)] public float ColorTemperature = 6570f;

        [Tooltip("Brightness of the light.")]
        [Min(0f)] public float Intensity = 1f;

        [Tooltip("Indirect Multiplier: how much this light contributes to baked/indirect lighting.")]
        [Min(0f)] public float IndirectMultiplier = 1f;

        [Tooltip("Shadow type.")]
        public LightShadows Shadows = LightShadows.Soft;

        [Tooltip("How dark the shadows are, 0-1.")]
        [Range(0f, 1f)] public float ShadowStrength = 1f;

        /// <summary>Reads the light's current values.</summary>
        public void Capture(Light light)
        {
            Vector3 euler = light.transform.eulerAngles;
            Elevation = Mathf.DeltaAngle(0f, euler.x);
            Azimuth = Mathf.Repeat(euler.y, 360f);
            Color = light.color;
            UseColorTemperature = light.useColorTemperature;
            ColorTemperature = light.colorTemperature;
            Intensity = light.intensity;
            IndirectMultiplier = light.bounceIntensity;
            Shadows = light.shadows;
            ShadowStrength = light.shadowStrength;
        }

        /// <summary>Writes these values to the light exactly.</summary>
        public void Apply(Light light)
        {
            light.transform.rotation = Rotation;
            light.useColorTemperature = UseColorTemperature;
            light.colorTemperature = ColorTemperature;
            light.color = Color;
            light.intensity = Intensity;
            light.bounceIntensity = IndirectMultiplier;
            light.shadows = Shadows;
            light.shadowStrength = ShadowStrength;
        }

        /// <summary>
        /// Writes a blend of two settings to the light. The colour is blended as the final colour (colour x
        /// temperature), so blending between a temperature-driven and a plain-colour sun does not jump.
        /// Shadow type switches when the blend is complete.
        /// </summary>
        public static void ApplyBlend(Light light, SunLightSettings a, SunLightSettings b, float t)
        {
            if (t >= 1f) { b.Apply(light); return; }
            if (t <= 0f) { a.Apply(light); return; }

            light.transform.rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t);
            light.useColorTemperature = false;
            light.color = Color.Lerp(a.FinalColor, b.FinalColor, t);
            light.intensity = Mathf.Lerp(a.Intensity, b.Intensity, t);
            light.bounceIntensity = Mathf.Lerp(a.IndirectMultiplier, b.IndirectMultiplier, t);
            light.shadows = a.Shadows;
            light.shadowStrength = Mathf.Lerp(a.ShadowStrength, b.ShadowStrength, t);
        }

        private Quaternion Rotation => Quaternion.Euler(Elevation, Azimuth, 0f);

        private Color FinalColor =>
            UseColorTemperature ? Color * Mathf.CorrelatedColorTemperatureToRGB(ColorTemperature) : Color;
    }

    /// <summary>Ambient light and reflections (RenderSettings). Field defaults are a new Unity scene's. Owner: MRM-86</summary>
    [Serializable]
    public sealed class EnvironmentLightSettings
    {
        [Tooltip("Where ambient light comes from: the skybox, three colours (Gradient), or one colour.")]
        public AmbientMode Source = AmbientMode.Skybox;

        [Tooltip("Ambient brightness when Source is Skybox (Lighting window: Intensity Multiplier).")]
        [Range(0f, 8f)] public float SkyboxIntensity = 1f;

        [Tooltip("Ambient colour (Flat), or the sky colour (Gradient).")]
        [ColorUsage(false, true)] public Color SkyColor = new Color(0.212f, 0.227f, 0.259f);

        [Tooltip("Horizon colour (Gradient only).")]
        [ColorUsage(false, true)] public Color EquatorColor = new Color(0.114f, 0.125f, 0.133f);

        [Tooltip("Ground colour (Gradient only).")]
        [ColorUsage(false, true)] public Color GroundColor = new Color(0.047f, 0.043f, 0.035f);

        [Tooltip("How strongly the sky is reflected by shiny surfaces, 0-1.")]
        [Range(0f, 1f)] public float ReflectionIntensity = 1f;

        public void Capture()
        {
            Source = RenderSettings.ambientMode == AmbientMode.Custom ? AmbientMode.Skybox : RenderSettings.ambientMode;
            SkyboxIntensity = RenderSettings.ambientIntensity;
            SkyColor = RenderSettings.ambientSkyColor;
            EquatorColor = RenderSettings.ambientEquatorColor;
            GroundColor = RenderSettings.ambientGroundColor;
            ReflectionIntensity = RenderSettings.reflectionIntensity;
        }

        public void Apply() => ApplyBlend(this, this, 1f);

        /// <summary>Source switches when the blend is complete; everything else blends.</summary>
        public static void ApplyBlend(EnvironmentLightSettings a, EnvironmentLightSettings b, float t)
        {
            RenderSettings.ambientMode = t >= 1f ? b.Source : a.Source;
            RenderSettings.ambientIntensity = Mathf.Lerp(a.SkyboxIntensity, b.SkyboxIntensity, t);
            RenderSettings.ambientSkyColor = Color.Lerp(a.SkyColor, b.SkyColor, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(a.EquatorColor, b.EquatorColor, t);
            RenderSettings.ambientGroundColor = Color.Lerp(a.GroundColor, b.GroundColor, t);
            RenderSettings.reflectionIntensity = Mathf.Lerp(a.ReflectionIntensity, b.ReflectionIntensity, t);
        }
    }

    /// <summary>
    /// Settings for one category of world light (lamps, flares, the moon's glow). Off = every light of the
    /// category keeps its own prefab values. Owner: MRM-86
    /// </summary>
    [Serializable]
    public sealed class WorldLightSettings
    {
        [Tooltip("Off: these lights keep their prefab values. On: every light of this kind uses the values below.")]
        public bool Override;

        public Color Color = Color.white;

        [Min(0f)] public float Intensity = 1f;

        [Tooltip("Reach of the light, metres.")]
        [Min(0f)] public float Range = 10f;

        public LightShadows Shadows = LightShadows.None;

        [Range(0f, 1f)] public float ShadowStrength = 1f;

        public void Capture(Light light)
        {
            Color = light.color;
            Intensity = light.intensity;
            Range = light.range;
            Shadows = light.shadows;
            ShadowStrength = light.shadowStrength;
        }

        public void Apply(Light light)
        {
            light.color = Color;
            light.intensity = Intensity;
            light.range = Range;
            light.shadows = Shadows;
            light.shadowStrength = ShadowStrength;
        }

        /// <summary>Blends two settings onto a light; shadow type switches when the blend is complete.</summary>
        public static void ApplyBlend(Light light, WorldLightSettings a, WorldLightSettings b, float t)
        {
            light.color = Color.Lerp(a.Color, b.Color, t);
            light.intensity = Mathf.Lerp(a.Intensity, b.Intensity, t);
            light.range = Mathf.Lerp(a.Range, b.Range, t);
            light.shadows = t >= 1f ? b.Shadows : a.Shadows;
            light.shadowStrength = Mathf.Lerp(a.ShadowStrength, b.ShadowStrength, t);
        }
    }

    /// <summary>
    /// Fog. <b>HAZE-shaped</b> (AST-078): its global volume override, its optional noise/scattering override,
    /// and the scene's one "area" density box. If the project moves to Volumetric Fog &amp; Mist 2 (under
    /// consideration, Carlos 2026-09-29, mainly for sandstorms), this section and <see cref="HazeFogAdapter"/>
    /// are what change; the rest of the profile does not. Owner: MRM-86
    /// </summary>
    [Serializable]
    public sealed class FogSettings
    {
        [Tooltip("Fog on or off. Off fades the fog out (density to zero) rather than cutting it.")]
        public bool Enabled = true;

        [Tooltip("HAZE global fog (the Volume override): fog everywhere.")]
        public HazeGlobalFogSettings Global = new HazeGlobalFogSettings();

        [Tooltip("HAZE density box covering the playable area (HAZE Explorable Area Fog), added on top of the global fog.")]
        public HazeAreaFogSettings Area = new HazeAreaFogSettings();

        [Tooltip("HAZE noise and multiple scattering (the optional HAZE Overrides volume component).")]
        public HazeNoiseSettings Noise = new HazeNoiseSettings();
    }

    /// <summary>HAZE global fog volume override, every parameter. Field defaults are HAZE's own. Owner: MRM-86</summary>
    [Serializable]
    public sealed class HazeGlobalFogSettings
    {
        [Header("Density")]
        [Tooltip("Master fog amount. HAZE's default is 0 (no global fog).")]
        [Min(0f)] public float DensityMultiplier;
        [Tooltip("Density below which fog is cut away (noise threshold).")]
        public float DensityThreshold = 0.2f;

        [Header("Color")]
        [Tooltip("Colour of the fog itself.")]
        [ColorUsage(false, true)] public Color AmbientColor = Color.white;
        [Tooltip("Colour multiplied by the sun's light in the fog. Raise the HDR intensity for stronger sun rays.")]
        [ColorUsage(false, true)] public Color MainLightContribution = Color.white;

        [Header("Height fog")]
        [Min(0f)] public float HeightFogFactor;
        [Tooltip("World height, metres, the fog reaches up to.")]
        public float MaxFogHeight;
        public float HeightFogSmoothness;
        public bool CameraRelativeHeightFog;

        [Header("Lighting")]
        [Tooltip("How much lamps, flares, the flashlight etc. light the fog (Forward+ only).")]
        [Min(0f)] public float AdditionalLightContribution;
        [Min(0f)] public float ProbeVolumeContribution;
        [Tooltip("How much the sun scatters into the fog, 0-1.")]
        [Range(0f, 1f)] public float MainLightScattering = 1f;
        [Tooltip("Extra density where the sun is not shadowed: stronger sun rays.")]
        [Min(0f)] public float MainLightDensityBoost;
        [Min(0f)] public float SecondaryLightDensityBoost;
    }

    /// <summary>HAZE density volume (the area box), every parameter except its shape, gradient texture and transform. Field defaults are HAZE's own. Owner: MRM-86</summary>
    [Serializable]
    public sealed class HazeAreaFogSettings
    {
        [Tooltip("Overall weight of the box, 0-1. Easiest way to fade it.")]
        [Range(0f, 1f)] public float Weight = 1f;
        [Min(0f)] public float Density = 1f;
        public float NoiseThreshold = 0.5f;

        [ColorUsage(false, true)] public Color AmbientColor = Color.white;
        [ColorUsage(false, true)] public Color MainLightContribution = Color.white;
        [Range(0f, 0.9999f)] public float GradientLightScattering = 0.5f;

        [Min(0f)] public float HeightFogFactor;
        public float MaxFogHeight;
        public float HeightFogSmoothness = 0.1f;

        [Min(0f)] public float AdditionalLightContribution = 1f;
        [Min(0f)] public float ProbeVolumeContribution;
        [Range(0f, 1f)] public float MainLightScattering = 1f;
        [Min(0f)] public float MainLightDensityBoost;
        [Min(0f)] public float SecondaryLightDensityBoost;
    }

    /// <summary>HAZE noise and multiple scattering override. Owner: MRM-86</summary>
    [Serializable]
    public sealed class HazeNoiseSettings
    {
        [Tooltip("Off: HAZE uses the noise/scattering set on the renderer feature (PC_Renderer). On: the values below.")]
        public bool Override;

        [Min(0.001f)] public float NoiseTiling = 0.001f;
        [Tooltip("How fast the fog noise drifts, per axis. The wind in the fog.")]
        public Vector3 NoisePanningSpeed = Vector3.zero;
        public Vector4 NoiseWeights = Vector4.one;

        [Range(0f, 1f)] public float MultipleScatteringIntensity = 1f;
        [Min(0.01f)] public float MultipleScatteringRadius = 7f;
        [Range(0f, 1f)] public float MultipleScatteringScatter = 1f;
        [Range(0f, 1f)] public float MultipleScatteringThreshold;
        [Range(3, 10)] public int MaxMultipleScatteringIterations = 5;
    }
}
