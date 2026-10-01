using System;
using System.Collections;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MrMoonlight.World.Weather
{
    /// <summary>
    /// Applies weather profiles from a <see cref="WeatherProfileLibraryBase"/> to the scene: the sky
    /// (through <see cref="SkyBlender"/>), the global directional light, the ambient light, the world lights
    /// (<see cref="WeatherLightSource"/>) and the fog (through an <see cref="IFogAdapter"/>).
    ///
    /// <para><b>Transitions.</b> A caller (the Lighting Test Scene's <see cref="SkyProximityCircuit"/>)
    /// picks a target with <see cref="BeginTransition"/>, drives <see cref="SetBlend"/> 0-1, and calls
    /// <see cref="Lock"/> at 1. The blend runs FROM the current weather (the scene's starting look before the
    /// first lock) TO the target, everything at once.</para>
    ///
    /// <para><b>Control board</b> (Play Mode). This component's Inspector is the place to tune the weather: the
    /// <c>live</c> profile at the top has a section for everything a weather holds (sky, sun, environment, special
    /// light sources, fog, and more later). It owns none of it: every change is written straight to the real sun,
    /// RenderSettings, world lights and fog, in real time. While no blend runs:</para>
    /// <list type="bullet">
    ///   <item>edit the board and the scene follows, one section at a time;</item>
    ///   <item>edit the current weather's entry in the profile asset and the board follows it;</item>
    ///   <item>press Save (Inspector buttons or the P panel) to copy the board into the current profile.
    ///   "Revert" reloads the board from the profile. The board is reloaded from the profile whenever a
    ///   blend locks, so unsaved edits are lost then.</item>
    /// </list>
    /// Editing the SUN light or the fog Volume directly still shows on screen, but the board does not see it and
    /// Save will not keep it: tune through the board.
    ///
    /// <para>Saving writes the library asset, so it works in the editor only (Play Mode edits to an asset
    /// persist). A build can apply profiles but not save them.</para>
    ///
    /// Owner: MRM-86.
    /// </summary>
    public abstract class WeatherSystemBase : MonoBehaviour
    {
        [Header("Wiring (set once)")]
        [Tooltip("Cross-fades the sky.")]
        [SerializeField] private SkyBlender skyBlender;

        [Tooltip("The scene's global directional light (the SUN).")]
        [SerializeField] private Light sun;

        [Header("Read-only, for tuning")]
        [Tooltip("The weather showing now (when not blending), and what Save writes to.")]
        [SerializeField] private string currentWeather = "(scene start)";

        [Tooltip("The weather being blended toward.")]
        [SerializeField] private string targetWeather;

        [SerializeField, Range(0f, 1f)] private float blend;

        [SerializeField] private string lastSave;

        private IFogAdapter _fog;
        private WeatherProfileBase _sceneStart;
        private int _current = -1;
        private int _target = -1;
        private bool _ready;
        private bool _wasBlending;
        private float _nextAmbientRefresh;

        private Section[] _sections;

        /// <summary>One part of a weather (sky, sun...): how to read it, copy it, apply it, and what was last seen.</summary>
        private sealed class Section
        {
            public Func<WeatherProfileBase, string> Json;
            public Action<WeatherProfileBase, WeatherProfileBase> Copy; // (from, to)
            public Action Apply;                                // writes the board's values to the scene
            public string Profile, Live;                        // last seen in the profile / on the board
        }

        /// <summary>The WeatherSystem in the loaded scene, if any.</summary>
        public static WeatherSystemBase Active { get; private set; }

        /// <summary>The control board: the values being applied to the scene right now (the concrete system owns the serialized field).</summary>
        protected abstract WeatherProfileBase Live { get; }

        /// <summary>The weather profiles (the concrete system owns the serialized field).</summary>
        protected abstract WeatherProfileLibraryBase Lib { get; }

        /// <summary>Builds the adapter that reads and writes this scene's fog asset. Called once, from Awake.</summary>
        protected abstract IFogAdapter CreateFogAdapter();

        /// <summary>A new, empty profile of this system's kind (used to capture the scene's starting look).</summary>
        protected abstract WeatherProfileBase NewProfile();

        public WeatherProfileLibraryBase Library => Lib;

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

        protected virtual void Awake()
        {
            Active = this;

            _fog = CreateFogAdapter();

            _sections = new[]
            {
                new Section
                {
                    Json = p => p.Skybox != null ? p.Skybox.GetInstanceID().ToString() : string.Empty,
                    Copy = (from, to) => to.Skybox = from.Skybox,
                    Apply = () =>
                    {
                        if (Live.Skybox == null) return;
                        skyBlender.SetFrom(Live.Skybox);
                        skyBlender.SetBlend(0f);
                        RefreshAmbient(true);
                    },
                },
                new Section
                {
                    Json = p => JsonUtility.ToJson(p.Sun),
                    Copy = (from, to) => Overwrite(from.Sun, to.Sun),
                    Apply = () => Live.Sun.Apply(sun),
                },
                new Section
                {
                    Json = p => JsonUtility.ToJson(p.Environment),
                    Copy = (from, to) => Overwrite(from.Environment, to.Environment),
                    Apply = () => { Live.Environment.Apply(); RefreshAmbient(true); },
                },
                new Section
                {
                    Json = LightsJson,
                    Copy = (from, to) =>
                    {
                        Overwrite(from.Lamps, to.Lamps);
                        Overwrite(from.Flares, to.Flares);
                        Overwrite(from.MoonGlow, to.MoonGlow);
                        Overwrite(from.TreeFires, to.TreeFires);
                    },
                    Apply = () => ApplyWorldLights(Live, Live, 1f),
                },
                new Section
                {
                    Json = p => p.FogJson(),
                    Copy = (from, to) => to.CopyFogFrom(from),
                    Apply = () => _fog.ApplyBlend(Live, Live, 1f),
                },
            };
        }

        private const int SkySection = 0, SunSection = 1, EnvironmentSection = 2, LightsSection = 3, FogSection = 4;

        private static void Overwrite(object from, object to) => JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from), to);

        private IEnumerator Start()
        {
            if (Lib == null || skyBlender == null || sun == null)
            {
                Debug.LogError("[MRM-86] Weather system: needs a profile library, a SkyBlender and the sun light.", this);
                enabled = false;
                yield break;
            }

            _sceneStart = CaptureScene("(scene start)");

            yield return skyBlender.CaptureCurrentSky();
            skyBlender.SetFromCaptured();
            LoadLive();
            _ready = true;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        /// <summary>Picks the weather to blend toward from the current one. The blend starts at 0.</summary>
        public void BeginTransition(int targetIndex)
        {
            if (Lib == null || targetIndex < 0 || targetIndex >= Lib.Count) return;
            _target = targetIndex;
            targetWeather = Lib[targetIndex].Name;
            skyBlender.SetTo(Lib[targetIndex].Skybox);
            blend = 0f;
        }

        /// <summary>Blends everything from the current weather toward the target, 0-1.</summary>
        public void SetBlend(float t)
        {
            if (!_ready || _target < 0) return;
            blend = Mathf.Clamp01(t);

            if (blend > 0f)
            {
                ApplyBlend(CurrentProfile, Lib[_target], blend);
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
            currentWeather = Lib[_current].Name;
            targetWeather = string.Empty;
            blend = 0f;
            _wasBlending = false;
            ApplyCurrent();
        }

        /// <summary>Applies the current weather's values to a world light (called when one enables).</summary>
        public void ApplyTo(WeatherLightSource source)
        {
            if (!_ready || source == null || source.Light == null) return;
            WeatherProfileBase from = IsBlending ? CurrentProfile : Live;
            WorldLightSettings a = Effective(from, source);
            if (_target >= 0 && blend > 0f)
            {
                WorldLightSettings.ApplyBlend(source.Light, a, Effective(Lib[_target], source), blend);
            }
            else
            {
                a.Apply(source.Light);
            }
        }

        /// <summary>
        /// The tree fire light values for right now: the control board's when no blend runs, a blend of the two
        /// weathers during one. A weather whose Tree Fires override is off contributes <paramref name="prefab"/>
        /// (the fire effect's own light). Returns false when neither weather overrides them (use the prefab values).
        /// Read every frame by <see cref="MrMoonlight.DevTools.TreeFireToggle"/>, which owns those lights.
        /// </summary>
        public bool TryGetTreeFireLights(WorldLightSettings prefab, WorldLightSettings result)
        {
            if (!_ready) return false;

            if (IsBlending && _target >= 0)
            {
                WorldLightSettings a = CurrentProfile.TreeFires, b = Lib[_target].TreeFires;
                if (!a.Override && !b.Override) return false;
                WorldLightSettings.Lerp(a.Override ? a : prefab, b.Override ? b : prefab, blend, result);
                return true;
            }

            if (!Live.TreeFires.Override) return false;
            WorldLightSettings.Lerp(Live.TreeFires, Live.TreeFires, 1f, result);
            return true;
        }

        /// <summary>Copies the board's sky, sun, ambient and special lights into the current weather. Editor only.</summary>
        public bool SaveLighting()
        {
            return Save("Lighting saved to ", SkySection, SunSection, EnvironmentSection, LightsSection);
        }

        /// <summary>Copies the board's fog into the current weather. Editor only.</summary>
        public bool SaveFog() => Save("Fog saved to ", FogSection);

        /// <summary>Copies every section of the board into the current weather. Editor only.</summary>
        public bool SaveAll()
        {
            return Save("Everything saved to ", SkySection, SunSection, EnvironmentSection, LightsSection, FogSection);
        }

        /// <summary>Reloads the board from the current weather's profile, dropping unsaved edits.</summary>
        public void RevertToProfile()
        {
            if (!_ready || IsBlending) return;
            ApplyCurrent();
        }

        private bool Save(string message, params int[] sections)
        {
            if (!CanSave(out string reason)) { lastSave = reason; return false; }
            WeatherProfileBase profile = Lib[_current];
            foreach (int i in sections)
            {
                _sections[i].Copy(Live, profile);
                _sections[i].Profile = _sections[i].Json(profile);
            }

            return WriteLibrary(message + profile.Name);
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
            if (!_ready || IsBlending) return;

            // One section at a time: a change to the profile asset flows onto the board, a change on the board
            // flows to the scene.
            WeatherProfileBase p = CurrentProfile;
            foreach (Section section in _sections)
            {
                string profileJson = section.Json(p);
                if (profileJson != section.Profile)
                {
                    section.Copy(p, Live);
                    section.Profile = profileJson;
                    section.Live = section.Json(Live);
                    section.Apply();
                    continue;
                }

                string liveJson = section.Json(Live);
                if (liveJson != section.Live)
                {
                    section.Live = liveJson;
                    section.Apply();
                }
            }
        }

        private WeatherProfileBase CurrentProfile => _current >= 0 ? Lib[_current] : _sceneStart;

        private void ApplyCurrent()
        {
            WeatherProfileBase p = CurrentProfile;
            ApplyBlend(p, p, 1f);
            if (_current >= 0)
            {
                skyBlender.SetFrom(p.Skybox);
                skyBlender.SetBlend(0f);
            }
            else
            {
                skyBlender.SetFromCaptured();
                skyBlender.SetBlend(0f);
            }

            LoadLive();
            RefreshAmbient(true);
        }

        /// <summary>Fills the control board from the current profile (no scene writes) and records what was seen.</summary>
        private void LoadLive()
        {
            WeatherProfileBase p = CurrentProfile;
            Live.Name = p.Name;
            foreach (Section section in _sections)
            {
                section.Copy(p, Live);
                section.Profile = section.Json(p);
                section.Live = section.Json(Live);
            }
        }

        private void ApplyBlend(WeatherProfileBase a, WeatherProfileBase b, float t)
        {
            SunLightSettings.ApplyBlend(sun, a.Sun, b.Sun, t);
            EnvironmentLightSettings.ApplyBlend(a.Environment, b.Environment, t);
            ApplyWorldLights(a, b, t);
            _fog.ApplyBlend(a, b, t);
        }

        private static void ApplyWorldLights(WeatherProfileBase a, WeatherProfileBase b, float t)
        {
            foreach (WeatherLightSource source in WeatherLightSource.All)
            {
                if (source.Light == null) continue;
                WorldLightSettings.ApplyBlend(source.Light, Effective(a, source), Effective(b, source), t);
            }
        }

        /// <summary>A profile's values for this light: its category's override, or the light's own prefab values.</summary>
        private static WorldLightSettings Effective(WeatherProfileBase profile, WeatherLightSource source)
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

        private WeatherProfileBase CaptureScene(string profileName)
        {
            WeatherProfileBase p = NewProfile();
            p.Name = profileName;
            p.Skybox = RenderSettings.skybox;
            p.Sun.Capture(sun);
            p.Environment.Capture();
            _fog.Capture(p);
            // World lights keep their prefab values at scene start.
            return p;
        }

        private static string LightsJson(WeatherProfileBase p) =>
            JsonUtility.ToJson(p.Lamps) + JsonUtility.ToJson(p.Flares) + JsonUtility.ToJson(p.MoonGlow)
            + JsonUtility.ToJson(p.TreeFires);

        private bool WriteLibrary(string message)
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(Lib);
            AssetDatabase.SaveAssetIfDirty(Lib);
            lastSave = message + " (" + System.DateTime.Now.ToString("HH:mm:ss") + ")";
            Debug.Log("[MRM-86] " + lastSave, this);
            return true;
#else
            return false;
#endif
        }
    }
}
