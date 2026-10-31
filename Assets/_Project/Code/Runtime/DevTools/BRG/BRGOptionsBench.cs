using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BRGInstancedRenderer;
using PolymindGames;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace MrMoonlight.DevTools.BRG
{
    /// <summary>
    /// Debug-only A/B bench for AST-314 BRG Instanced Renderer. Every option BRG (and URP's shadows) can
    /// change <i>while the game runs</i> is a row on an <b>F11</b> panel; each row is switched one at a time
    /// and measured at fixed benchmark spots, in the editor or in a build. Owner: MRM-88 (BRG options
    /// session, 2026-10-30, see <c>Docs/brg-options.md</c>).
    ///
    /// <para><b>Keys</b> (only while the panel is open, none of them are bound by the game):
    /// PgUp/PgDn pick a row, Home/End change its value (applied live), Delete measures the current
    /// settings once, Insert runs a quick sweep (each row at its most aggressive value) at the current
    /// spot, Shift+Insert a full sweep (every value), Ctrl+Insert a quick sweep at every spot,
    /// Backspace puts every row back to its baseline, Shift+PgUp/PgDn teleport to the previous/next
    /// spot, Shift+Home saves where you stand over the current spot (this session only, logged).</para>
    ///
    /// <para><b>Output.</b> Every measurement is a <c>[BRGBENCH]</c> Debug.Log line (so it lands in the
    /// SessionLog) and a row in <c>Logs/brgbench-yyyyMMdd-HHmmss.csv</c> next to the session logs. A sweep
    /// re-measures the baseline every few tests and compares each test against the latest one, so
    /// thermal or streaming drift does not read as an option's effect.</para>
    ///
    /// <para><b>Options that need a re-convert</b> (distance density, max shadow cascade, min shadow LOD,
    /// min screen size) cannot change at runtime; they are edited on the <c>BRGBakedOptions</c> asset
    /// and applied with <i>Mr. Moonlight/BRG/Apply Groups And Baked Options</i>. The group rows here need
    /// that menu to have run once, so that prototypes belong to a group.</para>
    ///
    /// <para><b>Why it restores on exit.</b> The BRG config, the URP asset and the groups' shadow fields
    /// are assets; play mode does not roll them back. Their startup values are put back in
    /// <see cref="OnDisable"/> (same reason as <c>SceneEffectsDebugToggle</c>).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/Dev Tools/BRG Options Bench")]
    public sealed class BRGOptionsBench : MonoBehaviour
    {
        [Serializable]
        public struct Spot
        {
            public string name;
            public Vector3 position;
            [Tooltip("Body yaw in degrees.")] public float yaw;
            [Tooltip("Camera pitch as the look handler stores it (its _viewAngles.x).")] public float pitch;
        }

        [Tooltip("Benchmark spots, taken from build 60's session log (2026-10-30).")]
        [SerializeField] private Spot[] spots =
        {
            new Spot { name = "a dense forest", position = new Vector3(24f, 51f, 81f), yaw = 252f },
            new Spot { name = "b far view (slowest, 141 fps b60)", position = new Vector3(-204f, 55f, 37f), yaw = 12f },
            new Spot { name = "c open ground", position = new Vector3(50f, 51f, 87f), yaw = 271f },
        };

        [Tooltip("Prototype groups to expose. Assigned by the setup menu; if empty, every loaded group is used.")]
        [SerializeField] private BRGPrototypeGroup[] groups;

        [Tooltip("Seconds to wait after a change (or a teleport) before measuring: lets chunks, HZB and density re-pack settle.")]
        [SerializeField] private float settleSeconds = 1.5f;

        [Tooltip("Seconds measured per test.")]
        [SerializeField] private float measureSeconds = 4f;

        [Tooltip("Re-measure the baseline after this many tests in a sweep.")]
        [SerializeField] private int baselineEvery = 6;

        [Tooltip("Seconds to settle after teleporting before the first baseline (streaming of terrain details). 4 s was too short for the first spot after Play: its first baseline read 109 fps, the later ones 122-124.")]
        [SerializeField] private float teleportSettleSeconds = 8f;

        [Tooltip("Debug overlay font, matches the other cheat labels.")]
        [SerializeField] private Font font;

        private sealed class Option
        {
            public string Name;
            public string[] Labels;
            public Action<int> Apply;
            public int Baseline;
            public int Current;
        }

        private struct Result
        {
            public float Fps, Low1, Cpu, Gpu;
        }

        private readonly List<Option> _options = new List<Option>();
        private readonly List<float> _frameTimes = new List<float>(4096);
        private readonly List<string> _recent = new List<string>();
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private readonly List<Action> _restore = new List<Action>();

        private bool _panelOpen;
        private int _selected;
        private int _spot;
        private bool _busy;
        private string _status = "";
        private string _csvPath;
        private GUIStyle _style;
        private GUIStyle _boxStyle;

        private BRGInstancedRendererConfig Config => BRGInstancedRendererConfig.Instance;

        private void Start()
        {
            useGUILayout = false;
            if (groups == null || groups.Length == 0)
                groups = Resources.FindObjectsOfTypeAll<BRGPrototypeGroup>();
            BuildOptions();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _busy = false;
            // Reverse order so a value touched twice ends on its original.
            for (int i = _restore.Count - 1; i >= 0; i--)
            {
                try { _restore[i](); }
                catch (Exception e) { Debug.LogWarning($"[BRGBENCH] restore failed: {e.Message}"); }
            }
            _restore.Clear();
        }

        // ----- options -------------------------------------------------------------------------

        private void BuildOptions()
        {
            var cfg = Config;
            if (cfg != null)
            {
                bool gpuOcc = cfg.UseGPUOcclusionCulling;
                _restore.Add(() => SetGpuOcclusion(gpuOcc));
                AddBool("BRG GPU occlusion (HZB)", gpuOcc, SetGpuOcclusion);

                var tight = cfg.hzbTightness;
                _restore.Add(() => Config.hzbTightness = tight);
                AddOption("HZB tightness", new[] { "Loose", "Tight", "Aggressive" }, (int)tight,
                    i => Config.hzbTightness = (HZBTightness)i);

                bool retest = cfg.useHZBRetestPass;
                _restore.Add(() => Config.useHZBRetestPass = retest);
                AddBool("HZB retest pass", retest, v => Config.useHZBRetestPass = v);

                float expand = cfg.hzbPixelExpansion;
                _restore.Add(() => Config.hzbPixelExpansion = expand);
                AddFloats("HZB pixel expansion", new[] { 2f, 3f, 1f }, expand, v => Config.hzbPixelExpansion = v);

                bool shadowOcc = cfg.shadowCasterOcclusion;
                _restore.Add(() => Config.shadowCasterOcclusion = shadowOcc);
                AddBool("Shadow caster occlusion", shadowOcc, v => Config.shadowCasterOcclusion = v);

                int segments = cfg.hzbShadowSegments;
                _restore.Add(() => Config.hzbShadowSegments = segments);
                AddInts("HZB shadow segments", new[] { 8, 4, 16 }, segments, v => Config.hzbShadowSegments = v);

                bool baked = cfg.useBakedOcclusionCulling;
                _restore.Add(() => Config.useBakedOcclusionCulling = baked);
                AddBool("Baked (Umbra) occlusion [needs a bake]", baked, v => Config.useBakedOcclusionCulling = v);

                bool skip = cfg.skipIntermediateLODs;
                _restore.Add(() => Config.skipIntermediateLODs = skip);
                AddBool("Skip intermediate LODs", skip, v => Config.skipIntermediateLODs = v);

                int xfade = cfg.maxShadowCrossfadeCascade;
                _restore.Add(() => Config.maxShadowCrossfadeCascade = xfade);
                AddInts("Shadow crossfade cascades", new[] { 4, 2, 1, 0 }, xfade, v => Config.maxShadowCrossfadeCascade = v);
            }

            // baselines are read from the scene, so the bench measures around whatever profile is applied
            float camBias = CameraLodBias();
            _restore.Add(() => SetCameraLodBias(camBias));
            AddFloats("BRG LOD bias (camera)", WithValue(new[] { 1f, 0.75f, 0.6f, 0.5f, 0.35f }, camBias), camBias, SetCameraLodBias);
            bool excluded = OtherCamerasExcluded();
            _restore.Add(() => SetExcludeOtherCameras(excluded));
            AddBool("Exclude non-main cameras from BRG", excluded, SetExcludeOtherCameras);

            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                float dist = urp.shadowDistance;
                int cascades = urp.shadowCascadeCount;
                _restore.Add(() => { urp.shadowDistance = dist; urp.shadowCascadeCount = cascades; });
                AddFloats("URP shadow distance (m)", new[] { dist, 60f, 45f, 30f }, dist, v => urp.shadowDistance = v);
                AddInts("URP shadow cascades", new[] { cascades, 2, 1 }, cascades, v => urp.shadowCascadeCount = v);
            }

            if (groups == null) return;
            foreach (var g in groups)
            {
                if (g == null) continue;
                var group = g;
                string n = group.name.Replace("BRGGroup_", "");

                bool improper = group.useImproperShadowFrustumCulling;
                float padding = group.improperShadowFrustumPadding;
                bool distCull = group.useShadowCasterDistanceCulling;
                bool useScene = group.useSceneShadowDistance;
                float maxDist = group.maxShadowDistance;
                _restore.Add(() =>
                {
                    group.SetLodBias(1f);
                    group.SetDensity(1f);
                    if (group.forceDisableShadows) group.SetShadowsEnabled(true);
                    group.SetImproperShadowFrustumCulling(improper, padding);
                    if (distCull) group.SetMaxShadowDistance(maxDist, useScene);
                    else group.SetShadowCasterDistanceCulling(false);
                });

                AddBool($"[{n}] shadows", true, group.SetShadowsEnabled);
                AddFloats($"[{n}] density", new[] { 1f, 0.75f, 0.5f, 0.25f }, 1f, group.SetDensity);
                AddFloats($"[{n}] LOD bias", new[] { 1f, 0.75f, 0.5f }, 1f, group.SetLodBias);
                // 0 = off; the profile's own distance is added if it is not one of the steps
                var distList = new List<float> { 0f };
                distList.AddRange(WithValue(new[] { 60f, 45f, 30f, 25f }, distCull && !useScene ? maxDist : 60f));
                float[] dists = distList.ToArray();
                var distLabels = new string[dists.Length];
                int distBase = 0;
                for (int i = 0; i < dists.Length; i++)
                {
                    distLabels[i] = dists[i] <= 0f ? "off" : $"{dists[i]:0} m";
                    if (distCull && !useScene && Mathf.Approximately(dists[i], maxDist)) distBase = i;
                }
                AddOption($"[{n}] shadow caster distance", distLabels, distBase, i =>
                {
                    if (dists[i] <= 0f) group.SetShadowCasterDistanceCulling(false);
                    else group.SetMaxShadowDistance(dists[i]);
                });
                AddBool($"[{n}] off-screen shadow cull", improper, v => group.SetImproperShadowFrustumCulling(v));
            }
        }

        // the value list with v inserted in order if missing, so a row's baseline is always one of its steps
        private static float[] WithValue(float[] values, float v)
        {
            foreach (var x in values) if (Mathf.Approximately(x, v)) return values;
            var list = new List<float>(values) { v };
            list.Sort((a, b) => b.CompareTo(a));
            return list.ToArray();
        }

        private static float CameraLodBias()
        {
            var cam = Camera.main;
            return cam != null && cam.TryGetComponent(out BRGCameraSettings s) && s.enabled ? s.lodBiasMultiplier : 1f;
        }

        private void AddOption(string name, string[] labels, int baseline, Action<int> apply)
        {
            baseline = Mathf.Clamp(baseline, 0, labels.Length - 1);
            _options.Add(new Option { Name = name, Labels = labels, Apply = apply, Baseline = baseline, Current = baseline });
        }

        private void AddBool(string name, bool baseline, Action<bool> apply) =>
            AddOption(name, new[] { "OFF", "ON" }, baseline ? 1 : 0, i => apply(i == 1));

        private void AddFloats(string name, float[] values, float baseline, Action<float> apply)
        {
            var labels = new string[values.Length];
            int b = 0;
            for (int i = 0; i < values.Length; i++)
            {
                labels[i] = values[i].ToString("0.##");
                if (Mathf.Approximately(values[i], baseline)) b = i;
            }
            AddOption(name, labels, b, i => apply(values[i]));
        }

        private void AddInts(string name, int[] values, int baseline, Action<int> apply)
        {
            var labels = new string[values.Length];
            int b = 0;
            for (int i = 0; i < values.Length; i++)
            {
                labels[i] = values[i].ToString();
                if (values[i] == baseline) b = i;
            }
            AddOption(name, labels, b, i => apply(values[i]));
        }

        private void SetValue(Option o, int index)
        {
            index = Mathf.Clamp(index, 0, o.Labels.Length - 1);
            if (index == o.Current) return;
            o.Current = index;
            o.Apply(index);
            Debug.Log($"[BRGBENCH] SET {o.Name} = {o.Labels[index]}");
        }

        private void ResetAll()
        {
            foreach (var o in _options) SetValue(o, o.Baseline);
        }

        private void SetGpuOcclusion(bool on) => Config.SetGPUOcclusionCulling(on);

        private void SetCameraLodBias(float bias)
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (!cam.TryGetComponent(out BRGCameraSettings settings))
                settings = cam.gameObject.AddComponent<BRGCameraSettings>();
            settings.lodBiasMultiplier = bias;
        }

        // BRG culls for every camera that renders, including overlay cameras that never draw foliage
        // (DamageNumbersOverlayCamera in scene 09).
        private static bool OtherCamerasExcluded()
        {
            foreach (var cam in Camera.allCameras)
            {
                if (cam == Camera.main) continue;
                if (!cam.TryGetComponent(out BRGCameraSettings settings) || !settings.ExcludeFromRendering) return false;
            }
            return true;
        }

        private static void SetExcludeOtherCameras(bool exclude)
        {
            foreach (var cam in Camera.allCameras)
            {
                if (cam == Camera.main) continue;
                if (!cam.TryGetComponent(out BRGCameraSettings settings))
                    settings = cam.gameObject.AddComponent<BRGCameraSettings>();
                settings.ExcludeFromRendering = exclude;
            }
        }

        // ----- input ---------------------------------------------------------------------------

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f11Key.wasPressedThisFrame)
            {
                _panelOpen = !_panelOpen;
                // so [PERF] windows with the panel on screen can be told apart in the SessionLog
                Debug.Log($"[BRGBENCH] PANEL {(_panelOpen ? "open" : "closed")}");
            }
            if (!_panelOpen || _options.Count == 0) return;

            bool shift = kb.shiftKey.isPressed;
            bool ctrl = kb.ctrlKey.isPressed;

            if (shift && kb.pageUpKey.wasPressedThisFrame) { if (!_busy) StartCoroutine(TeleportRoutine(_spot - 1)); return; }
            if (shift && kb.pageDownKey.wasPressedThisFrame) { if (!_busy) StartCoroutine(TeleportRoutine(_spot + 1)); return; }
            if (shift && kb.homeKey.wasPressedThisFrame) { SaveSpot(); return; }

            if (kb.pageUpKey.wasPressedThisFrame) _selected = (_selected - 1 + _options.Count) % _options.Count;
            if (kb.pageDownKey.wasPressedThisFrame) _selected = (_selected + 1) % _options.Count;

            if (_busy)
            {
                if (kb.escapeKey.wasPressedThisFrame) { StopAllCoroutines(); _busy = false; ResetAll(); _status = "sweep cancelled"; }
                return;
            }

            var sel = _options[_selected];
            if (kb.homeKey.wasPressedThisFrame) SetValue(sel, sel.Current - 1);
            if (kb.endKey.wasPressedThisFrame) SetValue(sel, sel.Current + 1);
            if (kb.backspaceKey.wasPressedThisFrame) ResetAll();
            if (kb.deleteKey.wasPressedThisFrame) StartCoroutine(MeasureOnceRoutine());
            if (kb.insertKey.wasPressedThisFrame)
            {
                if (ctrl) StartCoroutine(AllSpotsRoutine());
                else StartCoroutine(SweepRoutine(!shift));
            }
        }

        // ----- spots ---------------------------------------------------------------------------

        private IEnumerator TeleportRoutine(int index)
        {
            if (spots == null || spots.Length == 0) yield break;
            _spot = (index % spots.Length + spots.Length) % spots.Length;
            var s = spots[_spot];
            var player = FindPlayer();
            if (player == null) { _status = "no local player"; yield break; }

            player.SetPositionAndRotation(s.position, Quaternion.Euler(0f, s.yaw, 0f));
            yield return null; // the look handler resets its angles on the motor's Teleported event
            SetPitch(player, s.pitch);
            Debug.Log($"[BRGBENCH] SPOT {s.name} pos {s.position} yaw {s.yaw:0} pitch {s.pitch:0}");
            _status = $"at spot {s.name}";
        }

        private void SaveSpot()
        {
            var player = FindPlayer();
            if (player == null || spots == null || spots.Length == 0) return;
            var s = spots[_spot];
            s.position = player.transform.position;
            s.yaw = player.transform.eulerAngles.y;
            s.pitch = GetPitch(player);
            spots[_spot] = s;
            Debug.Log($"[BRGBENCH] SAVED SPOT {s.name} = pos ({s.position.x:0.#},{s.position.y:0.#},{s.position.z:0.#}) yaw {s.yaw:0.#} pitch {s.pitch:0.#}");
            _status = $"saved spot {s.name} (this session)";
        }

        private static Player FindPlayer()
        {
            if (GameMode.HasInstance && GameMode.Instance.LocalPlayer != null) return GameMode.Instance.LocalPlayer;
            return FindFirstObjectByType<Player>();
        }

        private static FieldInfo ViewAnglesField(ICharacter player, out Component handler)
        {
            handler = player.transform.GetComponentInChildren<CharacterLookHandler>(true);
            return handler == null ? null
                : typeof(CharacterLookHandler).GetField("_viewAngles", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static void SetPitch(ICharacter player, float pitch)
        {
            var f = ViewAnglesField(player, out var h);
            if (f == null) return;
            var v = (Vector2)f.GetValue(h);
            v.x = pitch;
            f.SetValue(h, v);
        }

        private static float GetPitch(ICharacter player)
        {
            var f = ViewAnglesField(player, out var h);
            return f == null ? 0f : ((Vector2)f.GetValue(h)).x;
        }

        // ----- measuring -----------------------------------------------------------------------

        private IEnumerator Measure(Action<Result> done)
        {
            yield return new WaitForSecondsRealtime(settleSeconds);
            _frameTimes.Clear();
            double cpu = 0, gpu = 0;
            int timed = 0;
            float t = 0f;
            while (t < measureSeconds)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                t += dt;
                _frameTimes.Add(dt);
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
                {
                    cpu += _timing[0].cpuFrameTime;
                    gpu += _timing[0].gpuFrameTime;
                    timed++;
                }
            }

            _frameTimes.Sort();
            float p99 = _frameTimes[Mathf.Clamp(Mathf.CeilToInt(_frameTimes.Count * 0.99f) - 1, 0, _frameTimes.Count - 1)];
            done(new Result
            {
                Fps = _frameTimes.Count / t,
                Low1 = 1f / p99,
                Cpu = timed > 0 ? (float)(cpu / timed) : 0f,
                Gpu = timed > 0 ? (float)(gpu / timed) : 0f,
            });
        }

        private IEnumerator MeasureOnceRoutine()
        {
            _busy = true;
            _status = "measuring current settings...";
            Result r = default;
            yield return Measure(x => r = x);
            Record(SpotName, "current", Describe(), r, null);
            _busy = false;
            _status = $"measured {r.Fps:0.0} fps";
        }

        private IEnumerator AllSpotsRoutine()
        {
            for (int i = 0; i < spots.Length; i++)
            {
                _spotsLeftAfterThis = spots.Length - 1 - i;
                yield return TeleportRoutine(i);
                yield return SweepRoutine(true);
            }
        }

        private IEnumerator SweepRoutine(bool quick)
        {
            _busy = true;
            ResetAll();
            yield return new WaitForSecondsRealtime(teleportSettleSeconds);

            var tests = new List<KeyValuePair<Option, int>>();
            foreach (var o in _options)
            {
                if (quick)
                {
                    // the value furthest from the baseline in the "cheaper" direction = the last label,
                    // or the flipped state for an on/off row
                    int v = o.Labels.Length == 2 ? 1 - o.Baseline
                        : o.Baseline == o.Labels.Length - 1 ? 0 : o.Labels.Length - 1;
                    if (v != o.Baseline) tests.Add(new KeyValuePair<Option, int>(o, v));
                }
                else
                {
                    for (int v = 0; v < o.Labels.Length; v++)
                        if (v != o.Baseline) tests.Add(new KeyValuePair<Option, int>(o, v));
                }
            }

            Debug.Log($"[BRGBENCH] SWEEP start ({(quick ? "quick" : "full")}, {tests.Count} tests) at {SpotName}");
            Result baseline = default;
            for (int i = 0; i < tests.Count; i++)
            {
                if (i % Mathf.Max(1, baselineEvery) == 0)
                {
                    _status = $"{SpotName}: baseline ({i}/{tests.Count}){Remaining(tests.Count - i)}";
                    yield return Measure(x => baseline = x);
                    Record(SpotName, "baseline", "", baseline, null);
                }

                var o = tests[i].Key;
                int v = tests[i].Value;
                _status = $"{SpotName}: {o.Name} = {o.Labels[v]} ({i + 1}/{tests.Count}){Remaining(tests.Count - i)}";
                SetValue(o, v);
                Result r = default;
                yield return Measure(x => r = x);
                Record(SpotName, o.Name, o.Labels[v], r, baseline);
                SetValue(o, o.Baseline);
            }

            Debug.Log($"[BRGBENCH] SWEEP done at {SpotName}, csv: {_csvPath}");
            _spotsLeftAfterThis = 0;
            _status = $"sweep done ({tests.Count} tests) - csv in Logs";
            _busy = false;
        }

        private int _spotsLeftAfterThis;

        // rough time left: this spot's remaining tests (+ their baselines) and, in an all-spots run, the later spots
        private string Remaining(int testsLeft)
        {
            float per = settleSeconds + measureSeconds;
            float spotTests = _options.Count * (1f + 1f / Mathf.Max(1, baselineEvery));
            float secs = testsLeft * (1f + 1f / Mathf.Max(1, baselineEvery)) * per
                         + _spotsLeftAfterThis * (spotTests * per + teleportSettleSeconds);
            return $" - about {Mathf.CeilToInt(secs / 60f)} min left";
        }

        private string SpotName => spots != null && spots.Length > 0 ? spots[_spot].name : "here";

        private string Describe()
        {
            var sb = new StringBuilder();
            foreach (var o in _options)
                if (o.Current != o.Baseline) sb.Append(o.Name).Append('=').Append(o.Labels[o.Current]).Append("; ");
            return sb.Length == 0 ? "baseline" : sb.ToString();
        }

        private void Record(string spot, string option, string value, Result r, Result? baseline)
        {
            string delta = baseline.HasValue && baseline.Value.Fps > 0f
                ? $"{(r.Fps / baseline.Value.Fps - 1f) * 100f:+0.0;-0.0}%"
                : "";
            string line = $"{spot} | {option} {value} | {r.Fps:0.0} fps {delta} | 1% low {r.Low1:0.0} | cpu {r.Cpu:0.00} ms gpu {r.Gpu:0.00} ms";
            Debug.Log("[BRGBENCH] " + line);

            _recent.Insert(0, line);
            if (_recent.Count > 12) _recent.RemoveAt(_recent.Count - 1);

            try
            {
                if (_csvPath == null)
                {
                    string dir = Path.Combine(Application.persistentDataPath, "Logs");
                    Directory.CreateDirectory(dir);
                    _csvPath = Path.Combine(dir, $"brgbench-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
                    File.WriteAllText(_csvPath, "time,mode,spot,option,value,fps,low1,cpu_ms,gpu_ms,base_fps,delta_pct\n");
                }
                string bf = baseline.HasValue ? baseline.Value.Fps.ToString("0.0") : "";
                string dp = baseline.HasValue && baseline.Value.Fps > 0f ? ((r.Fps / baseline.Value.Fps - 1f) * 100f).ToString("0.0") : "";
                File.AppendAllText(_csvPath,
                    $"{DateTime.Now:HH:mm:ss},{(Application.isEditor ? "editor" : "build")},\"{spot}\",\"{option}\",\"{value}\"," +
                    $"{r.Fps:0.0},{r.Low1:0.0},{r.Cpu:0.00},{r.Gpu:0.00},{bf},{dp}\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BRGBENCH] csv write failed: {e.Message}");
            }
        }

        // ----- panel ---------------------------------------------------------------------------

        // The panel text is rebuilt only when something on it changes, and drawn only on Repaint with
        // useGUILayout off: the first version rebuilt a 30-line rich-text string on every IMGUI event
        // and cost ~4 ms a frame in the editor (fps 170 -> 100 with the panel open, sweep of 2026-10-30).
        private string _panelText;
        private string _panelKey;

        private void OnGUI()
        {
            if (!_panelOpen || Event.current.type != EventType.Repaint) return;

            _style ??= new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, richText = true, normal = { textColor = Color.cyan } };
            _boxStyle ??= new GUIStyle(GUI.skin.box);

            string key = $"{_selected}|{_status}|{_recent.Count}|{(_recent.Count > 0 ? _recent[0] : "")}|{_busy}|{_spot}|{CurrentValuesKey()}";
            if (key != _panelKey)
            {
                _panelKey = key;
                _panelText = BuildPanelText();
            }

            float height = _busy ? 150f : 760f;
            var rect = new Rect(20f, 230f, 980f, height);
            GUI.Box(rect, GUIContent.none, _boxStyle);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f), _panelText, _style);
        }

        private string CurrentValuesKey()
        {
            int h = 17;
            foreach (var o in _options) h = h * 31 + o.Current;
            return h.ToString();
        }

        private string BuildPanelText()
        {
            var sb = new StringBuilder();
            sb.Append("<b>BRG OPTIONS BENCH</b> (F11 closes - the panel itself costs a little fps)   spot: ").Append(SpotName).Append('\n');

            if (_busy)
            {
                // during a sweep only the progress, so the panel adds as little as possible to what is measured
                sb.Append("<color=orange><b>TEST RUNNING - hands off mouse and keyboard. Esc cancels. Don't stop Play.</b></color>\n");
                sb.Append(_status).Append('\n');
                if (_recent.Count > 0) sb.Append(_recent[0]).Append('\n');
                return sb.ToString();
            }

            sb.Append("<b>PgUp / PgDn</b> = move the  >  up / down      <b>Home / End</b> = move the [ ] left / right on that row (applies at once)\n");
            sb.Append("<b>Ins</b> test here · <b>Ctrl+Ins</b> test all 3 spots (~11 min) · <b>Del</b> measure now · <b>Backspace</b> back to profile · <b>Shift+PgUp/PgDn</b> go to spot\n");
            sb.Append("<color=yellow>yellow</color> = changed from the profile\n\n");

            int first = Mathf.Clamp(_selected - 9, 0, Mathf.Max(0, _options.Count - 20));
            int last = Mathf.Min(_options.Count, first + 20);
            if (first > 0) sb.Append("   ...\n");
            for (int i = first; i < last; i++)
            {
                var o = _options[i];
                sb.Append(i == _selected ? "<b>></b> " : "   ").Append(o.Name).Append(":  ");
                for (int v = 0; v < o.Labels.Length; v++)
                {
                    if (v > 0) sb.Append("  ");
                    if (v == o.Current)
                        sb.Append(o.Current == o.Baseline ? $"<b>[{o.Labels[v]}]</b>" : $"<color=yellow><b>[{o.Labels[v]}]</b></color>");
                    else
                        sb.Append("<color=#5f8f8f>").Append(o.Labels[v]).Append("</color>");
                }
                sb.Append('\n');
            }
            if (last < _options.Count) sb.Append("   ...\n");

            sb.Append('\n').Append(_status).Append('\n');
            foreach (var line in _recent) sb.Append(line).Append('\n');
            return sb.ToString();
        }
    }
}
