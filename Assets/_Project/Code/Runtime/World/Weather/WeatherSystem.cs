using System.Collections;
using Haze.Runtime;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MrMoonlight.World.Weather
{
    /// <summary>
    /// Applies <see cref="WeatherProfile"/>s from a <see cref="WeatherProfileLibrary"/> to the scene: the sky
    /// (through <see cref="SkyBlender"/>), the global directional light, the ambient light, the world lights
    /// (<see cref="WeatherLightSource"/>) and the HAZE fog (<see cref="HazeFogAdapter"/>).
    ///
    /// <para><b>Transitions.</b> A caller (the Lighting Test Scene's <see cref="SkyProximityCircuit"/>)
    /// picks a target with <see cref="BeginTransition"/>, drives <see cref="SetBlend"/> 0-1, and calls
    /// <see cref="Lock"/> at 1. The blend runs FROM the current weather (the scene's starting look before the
    /// first lock) TO the target, everything at once.</para>
    ///
    /// <para><b>Live editing</b> (Play Mode). While no blend is running, the current weather is live:</para>
    /// <list type="bullet">
    ///   <item>edit its entry in the profile library and the scene follows immediately, one section at a time
    ///   (only the section you changed is re-applied);</item>
    ///   <item>or edit the SUN light, the Lighting window's Environment values, the fog Volume or the fog box
    ///   directly, then press Save Lighting Values / Save Fog Values (<see cref="WeatherTuningPanel"/>) to copy
    ///   them into the current profile. Unsaved direct edits are lost when the next blend starts.</item>
    /// </list>
    /// World lights (lamps, flares, moon glow) are tuned in the profile only: one edit restyles every lamp.
    ///
    /// <para>Saving writes the library asset, so it works in the editor only (Play Mode edits to an asset
    /// persist). A build can apply profiles but not save them.</para>
    ///
    /// Owner: MRM-86.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/World/Weather System")]
    public sealed class WeatherSystem : MonoBehaviour
    {
        [Tooltip("The weather profiles.")]
        [SerializeField] private WeatherProfileLibrary library;

        [Tooltip("Cross-fades the sky.")]
        [SerializeField] private SkyBlender skyBlender;

        [Tooltip("The scene's global directional light (the SUN).")]
        [SerializeField] private Light sun;

        [Tooltip("The global Volume carrying the HAZE Global Fog override.")]
        [SerializeField] private Volume fogVolume;

        [Tooltip("The HAZE density box covering the playable area (optional).")]
        [SerializeField] private HazeDensityVolume areaFog;

        [Header("Read-only, for tuning")]
        [Tooltip("The weather showing now (when not blending), and what Save writes to.")]
        [SerializeField] private string currentWeather = "(scene start)";

        [Tooltip("The weather being blended toward.")]
        [SerializeField] private string targetWeather;

        [SerializeField, Range(0f, 1f)] private float blend;

        [SerializeField] private string lastSave;

        private HazeFogAdapter _fog;
        private WeatherProfile _sceneStart;
        private int _current = -1;
        private int _target = -1;
        private bool _ready;
        private bool _wasBlending;
        private float _nextAmbientRefresh;

        // What was last applied for each section of the current profile, to spot live edits.
        private string _appliedSun, _appliedEnvironment, _appliedLights, _appliedFog;
        private Material _appliedSky;

        /// <summary>The WeatherSystem in the loaded scene, if any.</summary>
        public static WeatherSystem Active { get; private set; }

        public WeatherProfileLibrary Library => library;

        /// <summary>True once the starting sky has been captured and transitions can run.</summary>
        public bool IsReady => _ready;

        /// <summary>Index of the weather showing now, -1 for the scene's starting look.</summary>
        public int CurrentIndex => _current;

        /// <summary>Name of the weather showing now.</summary>
        public string CurrentName => currentWeather;

        /// <summary>Name of the weather being blended toward (empty when none).</summary>
        public string TargetName => targetWeather;

        /// <summary>Current blend toward the target, 0-1.</summary>
        public float Blend => blend;

        /// <summary>True while a blend is under way (the save buttons are off).</summary>
        public bool IsBlending => blend > 0f;

        /// <summary>Message about the last save, for the tuning panel.</summary>
        public string LastSave => lastSave;

        private void Awake()
        {
            Active = this;

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

            _fog = new HazeFogAdapter(global, noise, areaFog);
        }

        private IEnumerator Start()
        {
            if (library == null || skyBlender == null || sun == null)
            {
                Debug.LogError("[MRM-86] WeatherSystem: needs a profile library, a SkyBlender and the sun light.", this);
                enabled = false;
                yield break;
            }

            _sceneStart = CaptureScene("(scene start)");

            yield return skyBlender.CaptureCurrentSky();
            skyBlender.SetFromCaptured();
            _ready = true;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        /// <summary>Picks the weather to blend toward from the current one. The blend starts at 0.</summary>
        public void BeginTransition(int targetIndex)
        {
            if (library == null || targetIndex < 0 || targetIndex >= library.Count) return;
            _target = targetIndex;
            targetWeather = library[targetIndex].Name;
            skyBlender.SetTo(library[targetIndex].Skybox);
            blend = 0f;
        }

        /// <summary>Blends everything from the current weather toward the target, 0-1.</summary>
        public void SetBlend(float t)
        {
            if (!_ready || _target < 0) return;
            blend = Mathf.Clamp01(t);

            if (blend > 0f)
            {
                ApplyBlend(CurrentProfile, library[_target], blend);
                skyBlender.SetBlend(blend);
                RefreshAmbient(false);
                _wasBlending = true;
            }
            else if (_wasBlending)
            {
                // Walked back out before the lock: return to the current weather exactly.
                _wasBlending = false;
                ApplyCurrent();
            }
        }

        /// <summary>Completes the blend: the target becomes the current weather and is applied exactly.</summary>
        public void Lock()
        {
            if (_target < 0) return;
            _current = _target;
            _target = -1;
            currentWeather = library[_current].Name;
            targetWeather = string.Empty;
            blend = 0f;
            _wasBlending = false;
            ApplyCurrent();
        }

        /// <summary>Applies the current weather's values to a world light (called when one enables).</summary>
        public void ApplyTo(WeatherLightSource source)
        {
            if (!_ready || source == null || source.Light == null) return;
            WeatherProfile from = CurrentProfile;
            WorldLightSettings a = Effective(from, source);
            if (_target >= 0 && blend > 0f)
            {
                WorldLightSettings.ApplyBlend(source.Light, a, Effective(library[_target], source), blend);
            }
            else
            {
                a.Apply(source.Light);
            }
        }

        /// <summary>Copies the live sun and environment light into the current weather. Editor only.</summary>
        public bool SaveLighting()
        {
            if (!CanSave(out string reason)) { lastSave = reason; return false; }
            WeatherProfile profile = library[_current];
            profile.Sun.Capture(sun);
            profile.Environment.Capture();
            RememberApplied(profile);
            return WriteLibrary("Lighting saved to " + profile.Name);
        }

        /// <summary>Copies the live fog into the current weather. Editor only.</summary>
        public bool SaveFog()
        {
            if (!CanSave(out string reason)) { lastSave = reason; return false; }
            WeatherProfile profile = library[_current];
            _fog.Capture(profile.Fog);
            RememberApplied(profile);
            return WriteLibrary("Fog saved to " + profile.Name);
        }

        /// <summary>Whether Save can run right now, and why not.</summary>
        public bool CanSave(out string reason)
        {
#if !UNITY_EDITOR
            reason = "Saving only works in the Unity editor.";
            return false;
#else
            if (!_ready) { reason = "Starting up..."; return false; }
            if (_current < 0) { reason = "Reach the first green sphere first: nothing to save into yet."; return false; }
            if (IsBlending) { reason = "Step out of the pink sphere to save (a blend is running)."; return false; }
            reason = string.Empty;
            return true;
#endif
        }

        private void Update()
        {
            if (!_ready || _current < 0 || IsBlending) return;

            // Live editing: re-apply only the section of the current profile that changed.
            WeatherProfile p = library[_current];

            if (p.Skybox != _appliedSky)
            {
                skyBlender.SetFrom(p.Skybox);
                skyBlender.SetBlend(0f);
                _appliedSky = p.Skybox;
                RefreshAmbient(true);
            }

            string json = JsonUtility.ToJson(p.Sun);
            if (json != _appliedSun) { p.Sun.Apply(sun); _appliedSun = json; }

            json = JsonUtility.ToJson(p.Environment);
            if (json != _appliedEnvironment) { p.Environment.Apply(); _appliedEnvironment = json; RefreshAmbient(true); }

            json = LightsJson(p);
            if (json != _appliedLights) { ApplyWorldLights(p, p, 1f); _appliedLights = json; }

            json = JsonUtility.ToJson(p.Fog);
            if (json != _appliedFog) { _fog.ApplyBlend(p.Fog, p.Fog, 1f); _appliedFog = json; }
        }

        private WeatherProfile CurrentProfile => _current >= 0 ? library[_current] : _sceneStart;

        private void ApplyCurrent()
        {
            WeatherProfile p = CurrentProfile;
            ApplyBlend(p, p, 1f);
            if (_current >= 0)
            {
                skyBlender.SetFrom(p.Skybox);
                skyBlender.SetBlend(0f);
                RememberApplied(p);
            }
            else
            {
                skyBlender.SetFromCaptured();
                skyBlender.SetBlend(0f);
            }

            RefreshAmbient(true);
        }

        private void ApplyBlend(WeatherProfile a, WeatherProfile b, float t)
        {
            SunLightSettings.ApplyBlend(sun, a.Sun, b.Sun, t);
            EnvironmentLightSettings.ApplyBlend(a.Environment, b.Environment, t);
            ApplyWorldLights(a, b, t);
            _fog.ApplyBlend(a.Fog, b.Fog, t);
        }

        private static void ApplyWorldLights(WeatherProfile a, WeatherProfile b, float t)
        {
            foreach (WeatherLightSource source in WeatherLightSource.All)
            {
                if (source.Light == null) continue;
                WorldLightSettings.ApplyBlend(source.Light, Effective(a, source), Effective(b, source), t);
            }
        }

        /// <summary>A profile's values for this light: its category's override, or the light's own prefab values.</summary>
        private static WorldLightSettings Effective(WeatherProfile profile, WeatherLightSource source)
        {
            WorldLightSettings settings = profile.GetWorldLights(source.Category);
            return settings.Override ? settings : source.PrefabValues;
        }

        /// <summary>Recomputes skybox-driven ambient light. Throttled while blending (it costs a few ms).</summary>
        private void RefreshAmbient(bool force)
        {
            if (RenderSettings.ambientMode != AmbientMode.Skybox) return;
            if (!force && Time.unscaledTime < _nextAmbientRefresh) return;
            _nextAmbientRefresh = Time.unscaledTime + Tunables.I.WeatherAmbientRefreshSeconds;
            DynamicGI.UpdateEnvironment();
        }

        private WeatherProfile CaptureScene(string profileName)
        {
            var p = new WeatherProfile { Name = profileName, Skybox = RenderSettings.skybox };
            p.Sun.Capture(sun);
            p.Environment.Capture();
            _fog.Capture(p.Fog);
            // World lights keep their prefab values at scene start.
            return p;
        }

        private void RememberApplied(WeatherProfile p)
        {
            _appliedSky = p.Skybox;
            _appliedSun = JsonUtility.ToJson(p.Sun);
            _appliedEnvironment = JsonUtility.ToJson(p.Environment);
            _appliedLights = LightsJson(p);
            _appliedFog = JsonUtility.ToJson(p.Fog);
        }

        private static string LightsJson(WeatherProfile p) =>
            JsonUtility.ToJson(p.Lamps) + JsonUtility.ToJson(p.Flares) + JsonUtility.ToJson(p.MoonGlow);

        private bool WriteLibrary(string message)
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            lastSave = message + " (" + System.DateTime.Now.ToString("HH:mm:ss") + ")";
            Debug.Log("[MRM-86] " + lastSave, this);
            return true;
#else
            return false;
#endif
        }
    }
}
