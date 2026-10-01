using UnityEngine;
using VolumetricFogAndMist2;

namespace MrMoonlight.World.Weather
{
    /// <summary>
    /// The only code that knows Volumetric Fog &amp; Mist 2 (AST-282): reads and writes the <see cref="Vf2FogSettings"/>
    /// of a <see cref="Vf2WeatherProfile"/> from and to one <see cref="VolumetricFog"/> volume (the fog experiment).
    /// Works on a runtime copy of the volume's profile, so Play Mode never edits the profile asset.
    /// "Fog off" fades density to zero. The sun colour and brightness in the fog follow the scene sun
    /// (profile Day Night Cycle on, <see cref="VolumetricFogManager.sun"/> assigned), so only the fog's own
    /// look is stored here.
    ///
    /// Owner: MRM-85 (fog comparison), MRM-86 (weather profiles).
    /// </summary>
    public sealed class Vf2FogAdapter : IFogAdapter
    {
        private readonly VolumetricFog _fog;
        private readonly VolumetricFogProfile _profile;

        public Vf2FogAdapter(VolumetricFog fog)
        {
            _fog = fog;
            if (_fog == null || _fog.profile == null) return;
            _profile = Object.Instantiate(_fog.profile);
            _profile.name = _fog.profile.name + " (runtime)";
            _fog.profile = _profile;
        }

        public bool HasFog => _profile != null;

        /// <summary>
        /// Whether the fog is drawn. The fog is a mesh the render feature draws, so switching the VolumetricFog
        /// COMPONENT off is not enough (the mesh keeps drawing with its last material values). Hide the mesh too.
        /// </summary>
        private bool Visible
        {
            get => _fog.meshRenderer != null ? _fog.meshRenderer.enabled : _fog.enabled;
            set
            {
                if (_fog.meshRenderer != null) _fog.meshRenderer.enabled = value;
            }
        }

        public void Capture(WeatherProfileBase profile)
        {
            if (_profile == null) return;
            Vf2FogSettings v = ((Vf2WeatherProfile)profile).Fog;
            v.Enabled = Visible;
            v.Density = _profile.density;
            v.NoiseScale = _profile.noiseScale;
            v.NoiseStrength = _profile.noiseStrength;
            v.Albedo = _profile.albedo;
            v.Brightness = _profile.brightness;
            v.DeepObscurance = _profile.deepObscurance;
            v.AmbientLightMultiplier = _profile.ambientLightMultiplier;
            v.LightDiffusionIntensity = _profile.lightDiffusionIntensity;
            v.LightDiffusionPower = _profile.lightDiffusionPower;
            v.Turbulence = _profile.turbulence;
            v.WindDirection = _profile.windDirection;
            v.MaxDistance = _profile.maxDistance;
            v.NativeLightsMultiplier = _fog.nativeLightsMultiplier;
            v.LightDiffusionBackScatter = _profile.lightDiffusionBackScatter;
            v.DiffusionFloor = _profile.diffusionFloor;
            v.LightDiffusionNearDepthAtten = _profile.lightDiffusionNearDepthAtten;
            v.ReceiveShadows = _profile.receiveShadows;
            v.ShadowIntensity = _profile.shadowIntensity;
            v.ShadowMaxDistance = _profile.shadowMaxDistance;
            v.NoiseFinalMultiplier = _profile.noiseFinalMultiplier;
            v.UseDetailNoise = _profile.useDetailNoise;
            v.DetailScale = _profile.detailScale;
            v.DetailStrength = _profile.detailStrength;
            v.DetailOffset = _profile.detailOffset;
            v.ScaleNoiseWithHeight = _profile.scaleNoiseWithHeight;
            v.Border = _profile.border;
            v.Height = _profile.customHeight ? _profile.height : 0f;
            v.VerticalOffset = _profile.verticalOffset;
            v.Distance = _profile.distance;
            v.DistanceFallOff = _profile.distanceFallOff;
            v.MaxDistanceFallOff = _profile.maxDistanceFallOff;
            v.RaymarchQuality = _profile.raymarchQuality;
            v.RaymarchNearStepping = _profile.raymarchNearStepping;
            v.Jittering = _profile.jittering;
            v.Dithering = _profile.dithering;
            v.DetailNoiseWindDirection = _profile.detailNoiseWindDirection;
            v.CustomDetailWind = _profile.useCustomDetailNoiseWindDirection;
            v.DistantFog = _profile.distantFog;
            v.DistantFogStartDistance = _profile.distantFogStartDistance;
            v.DistantFogDensity = _profile.distantFogDistanceDensity;
            v.DistantFogColor = _profile.distantFogColor;
        }

        public void ApplyBlend(WeatherProfileBase a, WeatherProfileBase b, float t)
        {
            if (_profile == null) return;
            Vf2FogSettings va = ((Vf2WeatherProfile)a).Fog, vb = ((Vf2WeatherProfile)b).Fog;
            float onA = va.Enabled ? 1f : 0f;
            float onB = vb.Enabled ? 1f : 0f;

            _profile.density = Mathf.Lerp(va.Density * onA, vb.Density * onB, t);
            _profile.noiseScale = Mathf.Lerp(va.NoiseScale, vb.NoiseScale, t);
            _profile.noiseStrength = Mathf.Lerp(va.NoiseStrength, vb.NoiseStrength, t);
            _profile.albedo = Color.Lerp(va.Albedo, vb.Albedo, t);
            _profile.brightness = Mathf.Lerp(va.Brightness, vb.Brightness, t);
            _profile.deepObscurance = Mathf.Lerp(va.DeepObscurance, vb.DeepObscurance, t);
            _profile.ambientLightMultiplier = Mathf.Lerp(va.AmbientLightMultiplier, vb.AmbientLightMultiplier, t);
            _profile.lightDiffusionIntensity = Mathf.Lerp(va.LightDiffusionIntensity, vb.LightDiffusionIntensity, t);
            _profile.lightDiffusionPower = Mathf.Lerp(va.LightDiffusionPower, vb.LightDiffusionPower, t);
            _profile.turbulence = Mathf.Lerp(va.Turbulence, vb.Turbulence, t);
            _profile.windDirection = Vector3.Lerp(va.WindDirection, vb.WindDirection, t);
            _profile.maxDistance = Mathf.Lerp(va.MaxDistance, vb.MaxDistance, t);
            _fog.nativeLightsMultiplier = Mathf.Lerp(va.NativeLightsMultiplier, vb.NativeLightsMultiplier, t);

            _profile.lightDiffusionBackScatter = Mathf.Lerp(va.LightDiffusionBackScatter, vb.LightDiffusionBackScatter, t);
            _profile.diffusionFloor = Mathf.Lerp(va.DiffusionFloor, vb.DiffusionFloor, t);
            _profile.lightDiffusionNearDepthAtten = Mathf.Lerp(va.LightDiffusionNearDepthAtten, vb.LightDiffusionNearDepthAtten, t);
            _profile.shadowIntensity = Mathf.Lerp(va.ShadowIntensity, vb.ShadowIntensity, t);
            _profile.shadowMaxDistance = Mathf.Lerp(va.ShadowMaxDistance, vb.ShadowMaxDistance, t);
            _profile.noiseFinalMultiplier = Mathf.Lerp(va.NoiseFinalMultiplier, vb.NoiseFinalMultiplier, t);
            _profile.detailScale = Mathf.Lerp(va.DetailScale, vb.DetailScale, t);
            _profile.detailStrength = Mathf.Lerp(va.DetailStrength, vb.DetailStrength, t);
            _profile.detailOffset = Mathf.Lerp(va.DetailOffset, vb.DetailOffset, t);
            _profile.scaleNoiseWithHeight = Mathf.Lerp(va.ScaleNoiseWithHeight, vb.ScaleNoiseWithHeight, t);
            _profile.border = Mathf.Lerp(va.Border, vb.Border, t);
            _profile.verticalOffset = Mathf.Lerp(va.VerticalOffset, vb.VerticalOffset, t);
            _profile.distance = Mathf.Lerp(va.Distance, vb.Distance, t);
            _profile.distanceFallOff = Mathf.Lerp(va.DistanceFallOff, vb.DistanceFallOff, t);
            _profile.maxDistanceFallOff = Mathf.Lerp(va.MaxDistanceFallOff, vb.MaxDistanceFallOff, t);
            _profile.raymarchNearStepping = Mathf.Lerp(va.RaymarchNearStepping, vb.RaymarchNearStepping, t);
            _profile.jittering = Mathf.Lerp(va.Jittering, vb.Jittering, t);
            _profile.dithering = Mathf.Lerp(va.Dithering, vb.Dithering, t);
            _profile.detailNoiseWindDirection = Vector3.Lerp(va.DetailNoiseWindDirection, vb.DetailNoiseWindDirection, t);

            // Switches and whole numbers change when the blend completes.
            bool end = t >= 1f;
            _profile.receiveShadows = end ? vb.ReceiveShadows : va.ReceiveShadows;
            _profile.useDetailNoise = end ? vb.UseDetailNoise : va.UseDetailNoise;
            _profile.useCustomDetailNoiseWindDirection = end ? vb.CustomDetailWind : va.CustomDetailWind;
            _profile.raymarchQuality = end ? vb.RaymarchQuality : va.RaymarchQuality;
            float height = Mathf.Lerp(va.Height, vb.Height, t);
            _profile.customHeight = height > 0f;
            _profile.height = height;

            // Distant fog (horizon): the on/off switch changes when the blend completes; the rest blends.
            _profile.distantFog = t >= 1f ? vb.DistantFog : va.DistantFog;
            _profile.distantFogStartDistance = Mathf.Lerp(va.DistantFogStartDistance, vb.DistantFogStartDistance, t);
            _profile.distantFogDistanceDensity = Mathf.Lerp(va.DistantFogDensity * onA, vb.DistantFogDensity * onB, t);
            _profile.distantFogColor = Color.Lerp(va.DistantFogColor, vb.DistantFogColor, t);

            // The component stays enabled so the material keeps updating; only the mesh is hidden when off.
            _fog.UpdateMaterialProperties();
            Visible = va.Enabled || vb.Enabled;
        }
    }
}
