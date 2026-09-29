using Haze.Runtime;
using UnityEngine;

namespace MrMoonlight.World.Weather
{
    /// <summary>
    /// The only code that knows HAZE (AST-078): reads and writes <see cref="FogSettings"/> from and to the HAZE
    /// global volume override, the HAZE noise/scattering override and one HAZE density box. Swapping the fog
    /// asset (e.g. to Volumetric Fog &amp; Mist 2) means replacing this class and <see cref="FogSettings"/>.
    ///
    /// <para>Volume parameters only take effect with their override switch on, so every write sets
    /// <c>overrideState</c>. "Fog off" is written as zero density and zero box weight so it can fade.</para>
    ///
    /// Owner: MRM-86.
    /// </summary>
    public sealed class HazeFogAdapter
    {
        private readonly HazeGlobalFogVolumeComponent _global;
        private readonly HazeOverridesVolumeComponent _noise;
        private readonly HazeDensityVolume _area;

        /// <param name="global">HAZE global fog override on the scene's fog Volume (use the Volume's runtime profile, not the asset).</param>
        /// <param name="noise">HAZE overrides component on the same profile (optional).</param>
        /// <param name="area">The scene's area density box (optional).</param>
        public HazeFogAdapter(HazeGlobalFogVolumeComponent global, HazeOverridesVolumeComponent noise, HazeDensityVolume area)
        {
            _global = global;
            _noise = noise;
            _area = area;
        }

        public bool HasGlobal => _global != null;

        /// <summary>Reads what the fog objects show right now.</summary>
        public void Capture(FogSettings fog)
        {
            if (_global != null)
            {
                HazeGlobalFogSettings g = fog.Global;
                fog.Enabled = _global.active;
                g.DensityMultiplier = _global.GlobalDensityMultiplier.value;
                g.DensityThreshold = _global.GlobalDensityThreshold.value;
                g.AmbientColor = _global.AmbientColor.value;
                g.MainLightContribution = _global.MainLightContribution.value;
                g.HeightFogFactor = _global.HeightFogFactor.value;
                g.MaxFogHeight = _global.MaxFogHeight.value;
                g.HeightFogSmoothness = _global.HeightFogSmoothness.value;
                g.CameraRelativeHeightFog = _global.CameraRelativeHeightFog.value;
                g.AdditionalLightContribution = _global.AdditionalLightContribution.value;
                g.ProbeVolumeContribution = _global.ProbeVolumeContribution.value;
                g.MainLightScattering = _global.MainLightScattering.value;
                g.MainLightDensityBoost = _global.GlobalMainLightDensityBoost.value;
                g.SecondaryLightDensityBoost = _global.GlobalSecondaryLightDensityBoost.value;
            }

            if (_noise != null)
            {
                HazeNoiseSettings n = fog.Noise;
                n.Override = _noise.active && _noise.NoiseTiling.overrideState;
                n.NoiseTiling = _noise.NoiseTiling.value;
                n.NoisePanningSpeed = _noise.NoisePanningSpeed.value;
                n.NoiseWeights = _noise.NoiseWeights.value;
                n.MultipleScatteringIntensity = _noise.MultipleScatteringIntensity.value;
                n.MultipleScatteringRadius = _noise.MultipleScatteringRadius.value;
                n.MultipleScatteringScatter = _noise.MultipleScatteringScatter.value;
                n.MultipleScatteringThreshold = _noise.MultipleScatteringThreshold.value;
                n.MaxMultipleScatteringIterations = _noise.MaxMultipleScatteringIterations.value;
            }

            if (_area != null)
            {
                HazeAreaFogSettings a = fog.Area;
                a.Weight = _area.enabled ? _area.Weight : 0f;
                a.Density = _area.Density;
                a.NoiseThreshold = _area.NoiseThreshold;
                a.AmbientColor = _area.AmbientColor;
                a.MainLightContribution = _area.MainLightContribution;
                a.GradientLightScattering = _area.GradientLightScattering;
                a.HeightFogFactor = _area.HeightFogFactor;
                a.MaxFogHeight = _area.MaxFogHeight;
                a.HeightFogSmoothness = _area.HeightFogSmoothness;
                a.AdditionalLightContribution = _area.AdditionalLightContribution;
                a.ProbeVolumeContribution = _area.ProbeVolumeContribution;
                a.MainLightScattering = _area.MainLightScattering;
                a.MainLightDensityBoost = _area.MainLightDensityBoost;
                a.SecondaryLightDensityBoost = _area.SecondaryLightDensityBoost;
            }
        }

        /// <summary>Writes a blend of two fog settings (t = 0 is a, 1 is b). Switches (on/off toggles) change when the blend completes.</summary>
        public void ApplyBlend(FogSettings a, FogSettings b, float t)
        {
            // "Off" counts as zero density at its end of the blend, so turning fog on or off fades.
            float onA = a.Enabled ? 1f : 0f;
            float onB = b.Enabled ? 1f : 0f;
            float on = Mathf.Lerp(onA, onB, t);

            if (_global != null)
            {
                HazeGlobalFogSettings ga = a.Global, gb = b.Global;
                _global.active = a.Enabled || b.Enabled;
                Set(_global.GlobalDensityMultiplier, Mathf.Lerp(ga.DensityMultiplier * onA, gb.DensityMultiplier * onB, t));
                Set(_global.GlobalDensityThreshold, Mathf.Lerp(ga.DensityThreshold, gb.DensityThreshold, t));
                Set(_global.AmbientColor, Color.Lerp(ga.AmbientColor, gb.AmbientColor, t));
                Set(_global.MainLightContribution, Color.Lerp(ga.MainLightContribution, gb.MainLightContribution, t));
                Set(_global.HeightFogFactor, Mathf.Lerp(ga.HeightFogFactor, gb.HeightFogFactor, t));
                Set(_global.MaxFogHeight, Mathf.Lerp(ga.MaxFogHeight, gb.MaxFogHeight, t));
                Set(_global.HeightFogSmoothness, Mathf.Lerp(ga.HeightFogSmoothness, gb.HeightFogSmoothness, t));
                _global.CameraRelativeHeightFog.overrideState = true;
                _global.CameraRelativeHeightFog.value = t >= 1f ? gb.CameraRelativeHeightFog : ga.CameraRelativeHeightFog;
                Set(_global.AdditionalLightContribution, Mathf.Lerp(ga.AdditionalLightContribution, gb.AdditionalLightContribution, t));
                Set(_global.ProbeVolumeContribution, Mathf.Lerp(ga.ProbeVolumeContribution, gb.ProbeVolumeContribution, t));
                Set(_global.MainLightScattering, Mathf.Lerp(ga.MainLightScattering, gb.MainLightScattering, t));
                Set(_global.GlobalMainLightDensityBoost, Mathf.Lerp(ga.MainLightDensityBoost, gb.MainLightDensityBoost, t));
                Set(_global.GlobalSecondaryLightDensityBoost, Mathf.Lerp(ga.SecondaryLightDensityBoost, gb.SecondaryLightDensityBoost, t));
            }

            if (_noise != null)
            {
                HazeNoiseSettings na = a.Noise, nb = b.Noise;
                bool overrideNoise = t >= 1f ? nb.Override : na.Override;
                _noise.active = overrideNoise;
                SetNoise(_noise.NoiseTiling, Mathf.Lerp(na.NoiseTiling, nb.NoiseTiling, t), overrideNoise);
                _noise.NoisePanningSpeed.overrideState = overrideNoise;
                _noise.NoisePanningSpeed.value = Vector3.Lerp(na.NoisePanningSpeed, nb.NoisePanningSpeed, t);
                _noise.NoiseWeights.overrideState = overrideNoise;
                _noise.NoiseWeights.value = Vector4.Lerp(na.NoiseWeights, nb.NoiseWeights, t);
                SetNoise(_noise.MultipleScatteringIntensity, Mathf.Lerp(na.MultipleScatteringIntensity, nb.MultipleScatteringIntensity, t), overrideNoise);
                SetNoise(_noise.MultipleScatteringRadius, Mathf.Lerp(na.MultipleScatteringRadius, nb.MultipleScatteringRadius, t), overrideNoise);
                SetNoise(_noise.MultipleScatteringScatter, Mathf.Lerp(na.MultipleScatteringScatter, nb.MultipleScatteringScatter, t), overrideNoise);
                SetNoise(_noise.MultipleScatteringThreshold, Mathf.Lerp(na.MultipleScatteringThreshold, nb.MultipleScatteringThreshold, t), overrideNoise);
                _noise.MaxMultipleScatteringIterations.overrideState = overrideNoise;
                _noise.MaxMultipleScatteringIterations.value =
                    Mathf.RoundToInt(Mathf.Lerp(na.MaxMultipleScatteringIterations, nb.MaxMultipleScatteringIterations, t));
            }

            if (_area != null)
            {
                HazeAreaFogSettings aa = a.Area, ab = b.Area;
                _area.enabled = on > 0f;
                _area.Weight = Mathf.Lerp(aa.Weight * onA, ab.Weight * onB, t);
                _area.Density = Mathf.Lerp(aa.Density, ab.Density, t);
                _area.NoiseThreshold = Mathf.Lerp(aa.NoiseThreshold, ab.NoiseThreshold, t);
                _area.AmbientColor = Color.Lerp(aa.AmbientColor, ab.AmbientColor, t);
                _area.MainLightContribution = Color.Lerp(aa.MainLightContribution, ab.MainLightContribution, t);
                _area.GradientLightScattering = Mathf.Lerp(aa.GradientLightScattering, ab.GradientLightScattering, t);
                _area.HeightFogFactor = Mathf.Lerp(aa.HeightFogFactor, ab.HeightFogFactor, t);
                _area.MaxFogHeight = Mathf.Lerp(aa.MaxFogHeight, ab.MaxFogHeight, t);
                _area.HeightFogSmoothness = Mathf.Lerp(aa.HeightFogSmoothness, ab.HeightFogSmoothness, t);
                _area.AdditionalLightContribution = Mathf.Lerp(aa.AdditionalLightContribution, ab.AdditionalLightContribution, t);
                _area.ProbeVolumeContribution = Mathf.Lerp(aa.ProbeVolumeContribution, ab.ProbeVolumeContribution, t);
                _area.MainLightScattering = Mathf.Lerp(aa.MainLightScattering, ab.MainLightScattering, t);
                _area.MainLightDensityBoost = Mathf.Lerp(aa.MainLightDensityBoost, ab.MainLightDensityBoost, t);
                _area.SecondaryLightDensityBoost = Mathf.Lerp(aa.SecondaryLightDensityBoost, ab.SecondaryLightDensityBoost, t);
            }
        }

        private static void Set(UnityEngine.Rendering.VolumeParameter<float> p, float value)
        {
            p.overrideState = true;
            p.value = value;
        }

        private static void Set(UnityEngine.Rendering.VolumeParameter<Color> p, Color value)
        {
            p.overrideState = true;
            p.value = value;
        }

        private static void SetNoise(UnityEngine.Rendering.VolumeParameter<float> p, float value, bool overrideOn)
        {
            p.overrideState = overrideOn;
            p.value = value;
        }
    }
}
