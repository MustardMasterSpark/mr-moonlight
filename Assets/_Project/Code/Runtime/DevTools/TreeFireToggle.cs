using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using MrMoonlight.World.Weather;
using Debug = UnityEngine.Debug;

namespace MrMoonlight.DevTools
{
    /// <summary>
    /// Development experiment, <b>I</b>: the forest is on fire. Uses Ian's Fire Pack's (AST-015) "Burning Tree"
    /// effect without its own tree (<c>TreeFire_FX</c>). Press again to put the fires out. Made for the Lighting
    /// Test Scene (MRM-86), to see fire under the night weathers and to measure what it costs.
    /// Full history and results: <c>Docs/tree-fire-experiment.md</c>.
    ///
    /// <para><b>Strategy v2, a pool that follows the player</b> (2026-09-30). v1 put a fire, a light and a sound
    /// on every one of the 8,128 trees and ran under 20 fps. Now only <see cref="maxFires"/> fires exist; every
    /// <see cref="reassignSeconds"/> they move to the trees nearest the camera within <see cref="radius"/>, trees
    /// behind the camera counting as farther (<see cref="behindWeight"/>) and burning trees as nearer
    /// (<see cref="stickiness"/>, so fires do not hop between trees). A fire leaving a tree stops emitting and burns
    /// out naturally; a new one starts from nothing and grows (no prewarm, so no hitch and no pop).</para>
    ///
    /// <para>Only the <see cref="maxLights"/> nearest fires get a real point light (a small light pool that fades
    /// in and out and flickers like the vendor's LightFlicker), and only the <see cref="maxSounds"/> nearest get
    /// their fire sound. Everything else is particles. The lights' colour, intensity, range and shadows come from
    /// the current weather profile's "Tree Fires" special light source when its Override is on (blended with the
    /// weather), else from the effect's own light. The session log's [PERF] lines carry
    /// <see cref="BurningCount"/> and <see cref="LitCount"/>.</para>
    ///
    /// Owner: MRM-86 (experiment, Carlos 2026-09-30).
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Dev Tools/Tree Fire Toggle (experiment)")]
    public sealed class TreeFireToggle : MonoBehaviour
    {
        private const string SpawnContainerName = "Gaia Game Object Spawns";

        [Header("Effect")]
        [Tooltip("The fire effect put on each tree: AST-015's Burning Tree without the tree (TreeFire_FX).")]
        [SerializeField] private GameObject firePrefab;

        [Tooltip("Height of the tree the fire effect was made for, metres (AST-015's Scots pine is about 12 m). "
                 + "Each fire is scaled by its tree's height divided by this.")]
        [Min(0.1f)] [SerializeField] private float referenceTreeHeight = 12f;

        [Tooltip("Extra size on every fire on top of the tree-height scale (1.2 = 20% bigger). It may poke outside "
                 + "the tree. Applied live.")]
        [Min(0.1f)] [SerializeField] private float fireSizeMultiplier = 1.2f;

        [Tooltip("Show the effect's 'glow' sprite, the small bright ball at the heart of each fire. Off = flames "
                 + "and smoke only. Read at the first press.")]
        [SerializeField] private bool showGlowBall;

        [Tooltip("Vegetation shorter than this (stumps, logs, bushes) gets no fire, metres. Read at the first press.")]
        [Min(0f)] [SerializeField] private float minTreeHeight = 3f;

        [Header("Culling: which trees burn")]
        [Tooltip("Only trees within this distance of the camera can burn, metres.")]
        [Min(1f)] [SerializeField] private float radius = 80f;

        [Tooltip("How many fires exist at most. The main cost lever for particles. Read at the first press.")]
        [Min(1)] [SerializeField] private int maxFires = 60;

        [Tooltip("How often fires are moved to the nearest trees, seconds.")]
        [Min(0.02f)] [SerializeField] private float reassignSeconds = 0.25f;

        [Tooltip("Trees behind the camera count as this many times farther away. 1 = pure distance, "
                 + "higher = favour what you are looking at (but turning around shows fires starting).")]
        [Min(1f)] [SerializeField] private float behindWeight = 1.5f;

        [Tooltip("A tree already burning counts as this fraction of its distance, so fires do not hop between "
                 + "trees at the same distance. 1 = no stickiness.")]
        [Range(0.3f, 1f)] [SerializeField] private float stickiness = 0.85f;

        [Header("Lights")]
        [Tooltip("How many of the nearest fires get a real point light. The main cost lever for lighting "
                 + "(each light also lights the HAZE fog and the smoke). Read at the first press.")]
        [Min(0)] [SerializeField] private int maxLights = 10;

        [Tooltip("Multiplies the weather profile's Tree Fires light intensity (the flicker is added after). "
                 + "Raise it so the fires read at night. Applied live.")]
        [Min(0f)] [SerializeField] private float lightIntensityMultiplier = 3f;

        [Tooltip("Seconds for a fire light to fade in or out.")]
        [Min(0.01f)] [SerializeField] private float lightFadeSeconds = 1f;

        [Tooltip("Intensity flicker added on top of the light's intensity (vendor LightFlicker 'amount').")]
        [Min(0f)] [SerializeField] private float flickerAmount = 0.3f;

        [Tooltip("Flicker speed (vendor LightFlicker 'speed').")]
        [Min(0f)] [SerializeField] private float flickerSpeed = 8f;

        [Tooltip("How far the light wobbles around its spot, metres (vendor LightFlicker 'locationAdjustAmount').")]
        [Min(0f)] [SerializeField] private float flickerWobble = 0.1f;

        [Header("Sound")]
        [Tooltip("How many of the nearest fires play their fire sound.")]
        [Min(0)] [SerializeField] private int maxSounds = 6;

        [Header("Read-only")]
        [SerializeField] private int treesKnown;
        [SerializeField] private int firesBurning;
        [SerializeField] private int lightsOn;
        [SerializeField] private int soundsOn;
        [Tooltip("Cost of the last reassignment pass, milliseconds (CPU, main thread).")]
        [SerializeField] private float lastReassignMs;

        private struct Tree
        {
            public Vector3 Position;
            public float Scale;
        }

        private sealed class Fire
        {
            public GameObject Go;
            public ParticleSystem[] Systems;
            public AudioSource Sound;
            public int Tree = -1;
            public bool Releasing;
            public float FreeAt;
            public FireLight Light;
        }

        private sealed class FireLight
        {
            public Light Light;
            public Fire Fire;
            public bool Leaving;
            public float Fade;
            public float Seed;
        }

        private Tree[] _trees;
        private float[] _scores;
        private Fire[] _fires;
        private FireLight[] _lights;
        private GameObject _root;
        private bool _on;
        private float _nextReassign;
        private float _burnOutSeconds;
        private Vector3 _lightLocalPosition;
        private readonly WorldLightSettings _prefabLight = new WorldLightSettings();
        private readonly WorldLightSettings _weatherLight = new WorldLightSettings();

        private readonly Dictionary<int, Fire> _byTree = new Dictionary<int, Fire>();
        private readonly List<int> _candidates = new List<int>();
        private readonly List<Fire> _wanted = new List<Fire>();
        private readonly HashSet<int> _wantedTrees = new HashSet<int>();
        private readonly HashSet<Fire> _lightWanted = new HashSet<Fire>();

        /// <summary>How many tree fires are burning right now in the loaded scene (0 when off or absent).</summary>
        public static int BurningCount { get; private set; }

        /// <summary>How many fire point lights are on right now (0 when off or absent).</summary>
        public static int LitCount { get; private set; }

        public bool IsOn => _on;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
            {
                SetOn(!_on);
            }

            if (!_on) return;

            if (Time.time >= _nextReassign)
            {
                _nextReassign = Time.time + reassignSeconds;
                Reassign();
            }

            UpdateBurnOut();
            UpdateLights();
        }

        private void OnDestroy()
        {
            BurningCount = 0;
            LitCount = 0;
        }

        /// <summary>Lights (true) or puts out (false) the forest.</summary>
        public void SetOn(bool on)
        {
            if (on == _on) return;

            if (on && _root == null)
            {
                if (firePrefab == null)
                {
                    Debug.LogError("[MRM-86] TreeFireToggle: no fire prefab assigned.", this);
                    return;
                }

                Build();
            }

            if (_root == null) return;

            _on = on;
            if (on)
            {
                _root.SetActive(true);
                _nextReassign = 0f;
            }
            else
            {
                PutEverythingOut();
            }

            Debug.Log("[MRM-86] Tree fires " + (on ? "ON" : "OFF") + ": pool of " + _fires.Length + " fires (max "
                      + maxFires + " burning), " + _lights.Length + " lights, " + maxSounds + " sounds, radius "
                      + radius + " m, " + treesKnown + " trees known", this);
        }

        // --- build -----------------------------------------------------------------------------------------

        private void Build()
        {
            var clock = Stopwatch.StartNew();

            var trees = new List<Tree>();
            var renderers = new List<Renderer>();
            foreach (Transform tree in FindTrees())
            {
                float height = TreeHeight(tree, renderers);
                if (height < minTreeHeight) continue;
                trees.Add(new Tree { Position = tree.position, Scale = height / referenceTreeHeight });
            }

            _trees = trees.ToArray();
            _scores = new float[_trees.Length];
            treesKnown = _trees.Length;

            _root = new GameObject("Tree Fires (I)");
            _root.SetActive(false);

            // A few more fires than may burn at once, so burning-out fires do not starve new ones.
            int poolSize = Mathf.CeilToInt(maxFires * 1.5f);
            _fires = new Fire[poolSize];
            Light template = null;
            for (int i = 0; i < poolSize; i++)
            {
                GameObject go = Instantiate(firePrefab, _root.transform);
                go.name = "Tree Fire " + i;

                // The vendor light (with its LightFlicker, which caches a world position and would pin the light
                // where the fire was first placed) is replaced by the light pool below.
                foreach (Light light in go.GetComponentsInChildren<Light>(true))
                {
                    if (template == null)
                    {
                        _lightLocalPosition = light.transform.localPosition;
                        _prefabLight.Capture(light);
                        template = Instantiate(light);
                        template.gameObject.SetActive(false);
                        foreach (MonoBehaviour behaviour in template.GetComponents<MonoBehaviour>())
                        {
                            // Only the vendor flicker; URP's own light data stays.
                            if (behaviour.GetType().Name == "LightFlicker") DestroyImmediate(behaviour);
                        }
                    }

                    Destroy(light.gameObject);
                }

                if (!showGlowBall)
                {
                    // Every 'glow' sprite: the main fire's and the base fire's. Immediate: Systems is collected right below.
                    foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (child != go.transform && child.name.ToLowerInvariant() == "glow")
                        {
                            DestroyImmediate(child.gameObject);
                        }
                    }
                }

                var fire = new Fire { Go = go, Systems = go.GetComponentsInChildren<ParticleSystem>(true) };
                foreach (ParticleSystem system in fire.Systems)
                {
                    ParticleSystem.MainModule main = system.main;
                    main.prewarm = false; // a new fire grows from nothing: no hitch, and it reads as catching fire
                    _burnOutSeconds = Mathf.Max(_burnOutSeconds, main.startLifetime.constantMax);
                }

                fire.Sound = go.GetComponentInChildren<AudioSource>(true);
                if (fire.Sound != null)
                {
                    fire.Sound.playOnAwake = false;
                    fire.Sound.enabled = false;
                }

                go.SetActive(false);
                _fires[i] = fire;
            }

            _lights = new FireLight[template != null ? maxLights : 0];
            for (int i = 0; i < _lights.Length; i++)
            {
                Light light = Instantiate(template, _root.transform);
                light.name = "Tree Fire Light " + i;
                light.gameObject.SetActive(true);
                light.enabled = false;
                _lights[i] = new FireLight { Light = light, Seed = Random.value * 100f };
            }

            if (template != null) Destroy(template.gameObject);

            Debug.Log("[MRM-86] Tree fires built: " + treesKnown + " trees, pool of " + poolSize + " fires, "
                      + _lights.Length + " lights in " + clock.ElapsedMilliseconds + " ms", this);
        }

        private static IEnumerable<Transform> FindTrees()
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != SpawnContainerName) continue;
                    foreach (Transform spawner in t)
                    {
                        foreach (Transform instance in spawner)
                        {
                            if (instance.name.Contains("Rock")) continue;
                            yield return instance;
                        }
                    }
                }
            }
        }

        /// <summary>Height of the tree's top above its base, from its renderers' bounds (valid even when Flora draws them).</summary>
        private static float TreeHeight(Transform tree, List<Renderer> buffer)
        {
            tree.GetComponentsInChildren(true, buffer);
            float top = tree.position.y;
            foreach (Renderer r in buffer) top = Mathf.Max(top, r.bounds.max.y);
            return top - tree.position.y;
        }

        // --- culling ---------------------------------------------------------------------------------------

        private void Reassign()
        {
            var clock = Stopwatch.StartNew();

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 eye = cam.transform.position;
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            // Score every tree in range: distance, farther if behind the camera, nearer if already burning.
            _candidates.Clear();
            float radiusSq = radius * radius;
            for (int i = 0; i < _trees.Length; i++)
            {
                Vector3 offset = _trees[i].Position - eye;
                float distanceSq = offset.sqrMagnitude;
                if (distanceSq > radiusSq) continue;

                float score = Mathf.Sqrt(distanceSq);
                offset.y = 0f;
                if (Vector3.Dot(offset, forward) < 0f) score *= behindWeight;
                if (_byTree.ContainsKey(i)) score *= stickiness;
                _scores[i] = score;
                _candidates.Add(i);
            }

            _candidates.Sort((a, b) => _scores[a].CompareTo(_scores[b]));

            _wantedTrees.Clear();
            int count = Mathf.Min(maxFires, _candidates.Count);
            for (int i = 0; i < count; i++) _wantedTrees.Add(_candidates[i]);

            // Fires on trees that dropped out burn out.
            foreach (Fire fire in _fires)
            {
                if (fire.Tree >= 0 && !fire.Releasing && !_wantedTrees.Contains(fire.Tree)) Release(fire);
            }

            // Wanted trees without a fire get one, nearest first; the rest wait for a fire to burn out.
            _wanted.Clear();
            for (int i = 0; i < count; i++)
            {
                int tree = _candidates[i];
                if (!_byTree.TryGetValue(tree, out Fire fire))
                {
                    fire = FindFreeFire();
                    if (fire == null) continue;
                    Ignite(fire, tree);
                }

                _wanted.Add(fire);
            }

            AssignLights();
            AssignSounds();

            firesBurning = _byTree.Count;
            BurningCount = firesBurning;
            lastReassignMs = (float)clock.Elapsed.TotalMilliseconds;
        }

        private Fire FindFreeFire()
        {
            foreach (Fire fire in _fires)
            {
                if (fire.Tree < 0 && !fire.Releasing) return fire;
            }

            return null;
        }

        private void Ignite(Fire fire, int tree)
        {
            fire.Tree = tree;
            _byTree[tree] = fire;
            fire.Go.transform.SetPositionAndRotation(_trees[tree].Position,
                Quaternion.Euler(0f, (tree * 137.5f) % 360f, 0f));
            fire.Go.transform.localScale = Vector3.one * (_trees[tree].Scale * fireSizeMultiplier);
            fire.Go.SetActive(true);
            foreach (ParticleSystem system in fire.Systems)
            {
                system.Clear(false);
                system.Play(false);
            }
        }

        private void Release(Fire fire)
        {
            _byTree.Remove(fire.Tree);
            fire.Tree = -1;
            fire.Releasing = true;
            fire.FreeAt = Time.time + _burnOutSeconds;
            foreach (ParticleSystem system in fire.Systems) system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            if (fire.Sound != null) fire.Sound.enabled = false;
            if (fire.Light != null) fire.Light.Leaving = true;
        }

        private void UpdateBurnOut()
        {
            foreach (Fire fire in _fires)
            {
                if (!fire.Releasing || Time.time < fire.FreeAt) continue;
                fire.Releasing = false;
                fire.Go.SetActive(false);
            }
        }

        private void PutEverythingOut()
        {
            foreach (Fire fire in _fires)
            {
                fire.Tree = -1;
                fire.Releasing = false;
                fire.Light = null;
                if (fire.Sound != null) fire.Sound.enabled = false;
                fire.Go.SetActive(false);
            }

            foreach (FireLight light in _lights)
            {
                light.Fire = null;
                light.Leaving = false;
                light.Fade = 0f;
                light.Light.enabled = false;
            }

            _byTree.Clear();
            _root.SetActive(false);
            BurningCount = firesBurning = 0;
            LitCount = lightsOn = 0;
            soundsOn = 0;
        }

        // --- lights and sounds -----------------------------------------------------------------------------

        private void AssignLights()
        {
            _lightWanted.Clear();
            for (int i = 0; i < _wanted.Count && _lightWanted.Count < _lights.Length; i++) _lightWanted.Add(_wanted[i]);

            foreach (FireLight light in _lights)
            {
                if (light.Fire != null && !_lightWanted.Contains(light.Fire)) light.Leaving = true;
            }

            foreach (Fire fire in _lightWanted)
            {
                if (fire.Light != null)
                {
                    fire.Light.Leaving = false;
                    continue;
                }

                foreach (FireLight light in _lights)
                {
                    if (light.Fire != null) continue;
                    light.Fire = fire;
                    light.Leaving = false;
                    light.Fade = 0f;
                    light.Light.enabled = true;
                    fire.Light = light;
                    break;
                }
            }
        }

        private void UpdateLights()
        {
            int on = 0;
            float step = Time.deltaTime / lightFadeSeconds;

            // The weather's Tree Fires light section when it overrides them, else the effect's own light.
            WeatherSystemBase weather = WeatherSystemBase.Active;
            WorldLightSettings look = weather != null && weather.TryGetTreeFireLights(_prefabLight, _weatherLight)
                ? _weatherLight
                : _prefabLight;

            foreach (FireLight light in _lights)
            {
                if (light.Fire == null) continue;

                light.Fade = Mathf.MoveTowards(light.Fade, light.Leaving ? 0f : 1f, step);
                if (light.Leaving && light.Fade <= 0f)
                {
                    light.Fire.Light = null;
                    light.Fire = null;
                    light.Light.enabled = false;
                    continue;
                }

                // Same flicker as the vendor's LightFlicker: Perlin noise on intensity and on position.
                float t = Time.time * flickerSpeed;
                float noise = Mathf.PerlinNoise(t, light.Seed);
                Transform fire = light.Fire.Go.transform;
                var wobble = new Vector3(Mathf.PerlinNoise(t, light.Seed + 5f) - 0.5f, noise - 0.5f,
                    Mathf.PerlinNoise(t, light.Seed + 10f) - 0.5f);
                light.Light.transform.position = fire.TransformPoint(_lightLocalPosition) + wobble * (flickerWobble * 2f);
                light.Light.color = look.Color;
                light.Light.range = look.Range;
                light.Light.shadows = look.Shadows;
                light.Light.shadowStrength = look.ShadowStrength;
                light.Light.intensity = (look.Intensity * lightIntensityMultiplier + noise * flickerAmount) * light.Fade;
                on++;
            }

            lightsOn = on;
            LitCount = on;
        }

        private void AssignSounds()
        {
            int on = 0;
            for (int i = 0; i < _wanted.Count; i++)
            {
                AudioSource sound = _wanted[i].Sound;
                if (sound == null) continue;

                bool audible = i < maxSounds;
                if (audible && !sound.enabled)
                {
                    sound.enabled = true;
                    sound.Play();
                }
                else if (!audible && sound.enabled)
                {
                    sound.enabled = false;
                }

                if (audible) on++;
            }

            soundsOn = on;
        }
    }
}
