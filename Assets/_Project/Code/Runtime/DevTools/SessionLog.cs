using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text;
using MrMoonlight.Data;
using MrMoonlight.Enemies;
using PolymindGames.WieldableSystem;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace MrMoonlight.DevTools
{
    /// <summary>
    /// Timestamped, scene-tagged session log for build-mode perf tests. Installs itself before the
    /// first scene loads (no scene wiring, works from any scene), so it is always on in a build.
    /// Writes <c>session-yyyyMMdd-HHmmss.log</c> under <c>Application.persistentDataPath/Logs</c>
    /// (Windows build: <c>%USERPROFILE%\AppData\LocalLow\&lt;company&gt;\&lt;product&gt;\Logs</c>).
    /// <list type="bullet">
    /// <item>Every Unity log line (Debug.Log, warnings, errors, exceptions) is copied into the file
    /// with a UTC timestamp, seconds since startup, frame number and the ACTIVE SCENE's name, so
    /// filtering by <c>[Island_Legion]</c> isolates that scene.</item>
    /// <item><c>[SCENE]</c> marker lines at every load, unload and active-scene switch, with a
    /// per-scene summary (duration, frames, avg fps, 1% low, worst frame, enemies, kills by weapon)
    /// when leaving a scene or quitting.</item>
    /// <item><c>[PERF]</c> windowed lines every <see cref="MoonlightTunables.SessionLogPerfSampleSeconds"/>:
    /// fps and frame-time stats, CPU/GPU frame time, draw calls / SetPass / triangles / batches, managed
    /// memory, player position and biome, the equipped weapon, live enemies and corpses.</item>
    /// <item><c>[ENEMY]</c> SPAWN and KILL lines (kills name the killer and the weapon), <c>[PLAYER]</c>
    /// weapon switches, <c>[APP]</c> focus changes (fps while unfocused is not trustworthy).</item>
    /// </list>
    /// The markers also go through Debug.Log, so they show up in Player.log too. First version of a
    /// system that is expected to change a lot; see <c>Docs/performance-sessions.md</c>. Owner: MRM-84
    /// </summary>
    public sealed class SessionLog : MonoBehaviour
    {
        private const string LogFolderName = "Logs";
        private const string NoScene = "(none)";
        private const string Unknown = "?";
        private const float OnePercentLowFraction = 0.99f;
        private const string CloneSuffix = "(Clone)";

        private sealed class SceneStats
        {
            public int Spawned;
            public int Killed;
            public readonly Dictionary<string, int> KillsByWeapon = new Dictionary<string, int>();
        }

        private static SessionLog _instance;
        private static StreamWriter _writer;
        private static readonly object WriterLock = new object();

        // The threaded log callback cannot touch Time/SceneManager, so the main thread publishes these.
        private static volatile string _sceneTag = NoScene;
        private static volatile int _frame;
        private static long _realtimeMillis;

        // Frame times of the active scene (warm-up excluded); a window is a slice of this list.
        private readonly List<float> _sceneFrameTimes = new List<float>(4096);
        private int _windowStartIndex;
        private float _windowElapsed;
        private float _sceneEnteredAt;
        private double _sceneSumDt;
        private float _sceneWorstDt;
        private int _activeSceneHandle = -1;

        // Per-window render / timing accumulators.
        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _setPassCalls;
        private ProfilerRecorder _triangles;
        private ProfilerRecorder _batches;
        private double _drawSum, _setPassSum, _triSum, _batchSum;
        private int _renderSamples;
        private double _cpuSum, _gpuSum;
        private float _cpuMax, _gpuMax;
        private int _timingSamples;
        private readonly FrameTiming[] _timing = new FrameTiming[1];

        // Live enemy tally. A set, not a counter, so an enemy that dies and is then disabled or
        // destroyed by corpse cleanup is only subtracted once. Corpses are kept by the game, so they
        // are tracked separately: the count of dead-but-still-present enemies.
        private readonly HashSet<EnemyIdentity> _aliveEnemies = new HashSet<EnemyIdentity>();
        private readonly HashSet<EnemyHealth> _corpses = new HashSet<EnemyHealth>();

        // Spawned/killed keyed by the enemy's own scene handle: a scene's first enemies spawn a few
        // frames before it becomes the active scene, so the active-scene tag would misfile them.
        private readonly Dictionary<int, SceneStats> _sceneStats = new Dictionary<int, SceneStats>();

        private WieldablesController _wieldables;

        // Self-measured cost of this logger (its own Update work and the periodic window write), so
        // "does logging hurt fps?" is answered by the log itself.
        private long _updateTicks;
        private int _updateSamples;
        private long _lastWindowWriteTicks;

        // Display state, to catch the window/mode changes that are otherwise invisible in the log.
        private int _lastWidth, _lastHeight;
        private FullScreenMode _lastMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_instance != null || !Tunables.I.SessionLogEnabled)
                return;

            var host = new GameObject("SessionLog");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<SessionLog>();
        }

        private void Awake()
        {
            OpenFile();

            Application.logMessageReceivedThreaded += OnLogMessage;
            Application.focusChanged += OnFocusChanged;
            Application.quitting += OnQuitting;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            EnemyIdentity.AnySpawned += OnEnemySpawned;
            EnemyIdentity.AnyDespawned += OnEnemyDespawned;
            EnemyHealth.AnyDied += OnEnemyDied;

            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");

            WriteHeader();
        }

        private void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLogMessage;
            Application.focusChanged -= OnFocusChanged;
            Application.quitting -= OnQuitting;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            EnemyIdentity.AnySpawned -= OnEnemySpawned;
            EnemyIdentity.AnyDespawned -= OnEnemyDespawned;
            EnemyHealth.AnyDied -= OnEnemyDied;
            UnsubscribeWieldables();

            _drawCalls.Dispose();
            _setPassCalls.Dispose();
            _triangles.Dispose();
            _batches.Dispose();

            CloseFile();
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            PublishClock();
            CheckDisplayChange();

            // Scene-load hitches are real but say nothing about steady-state cost: skip them.
            if (Time.realtimeSinceStartup - _sceneEnteredAt < Tunables.I.SessionLogPerfWarmupSeconds)
                return;

            long start = Stopwatch.GetTimestamp();
            float dt = Time.unscaledDeltaTime;
            _sceneFrameTimes.Add(dt);
            _sceneSumDt += dt;
            _windowElapsed += dt;
            if (dt > _sceneWorstDt)
                _sceneWorstDt = dt;

            SampleRenderStats();
            SampleFrameTiming();
            _updateTicks += Stopwatch.GetTimestamp() - start;
            _updateSamples++;

            float interval = Tunables.I.SessionLogPerfSampleSeconds;
            if (interval > 0f && _windowElapsed >= interval)
                WritePerfWindow("window");
        }

        private void CheckDisplayChange()
        {
            if (Screen.width == _lastWidth && Screen.height == _lastHeight && Screen.fullScreenMode == _lastMode)
                return;

            bool first = _lastWidth == 0;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastMode = Screen.fullScreenMode;
            if (!first)
                Debug.Log($"[APP] display changed: {_lastWidth}x{_lastHeight} {_lastMode} (window/mode switch - a visible flicker if it happened on a scene load)");
        }

        // Refreshed before every marker too, so scene-switch lines carry the switch frame, not the last Update's.
        private static void PublishClock()
        {
            _frame = Time.frameCount;
            _realtimeMillis = (long)(Time.realtimeSinceStartup * 1000f);
        }

        // --- scene events ---------------------------------------------------------------------

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PublishClock();
            Debug.Log($"[SCENE] LOADED {scene.name} (buildIndex {scene.buildIndex}, mode {mode}, path {scene.path})");
        }

        private void OnSceneUnloaded(Scene scene)
        {
            PublishClock();
            Debug.Log($"[SCENE] UNLOADED {scene.name}");
        }

        private void OnActiveSceneChanged(Scene from, Scene to)
        {
            PublishClock();
            // Close out the scene we are leaving under ITS tag, then switch the tag.
            if (_sceneFrameTimes.Count > 0)
                WritePerfWindow("final window");
            WriteSceneSummary();
            _sceneStats.Remove(_activeSceneHandle);

            _sceneTag = string.IsNullOrEmpty(to.name) ? NoScene : to.name;
            _activeSceneHandle = to.handle;
            ResetSceneStats();
            Debug.Log($"[SCENE] ===== ACTIVE {_sceneTag} (from {(string.IsNullOrEmpty(from.name) ? NoScene : from.name)}, t={Time.realtimeSinceStartup:0.000}s) =====");
        }

        private void OnFocusChanged(bool hasFocus)
        {
            PublishClock();
            Debug.Log(hasFocus ? "[APP] window regained focus" : "[APP] window LOST focus - fps in this stretch is not trustworthy");
        }

        private void OnQuitting()
        {
            PublishClock();
            if (_sceneFrameTimes.Count > 0)
                WritePerfWindow("final window");
            WriteSceneSummary();
            Debug.Log("[APP] quitting");
        }

        // --- player ---------------------------------------------------------------------------

        private bool TryResolveWieldables()
        {
            if (_wieldables != null)
                return true;

            _wieldables = FindAnyObjectByType<WieldablesController>();
            if (_wieldables == null)
                return false;

            _wieldables.EquippingStopped += OnWeaponEquipped;
            Debug.Log($"[PLAYER] found player rig, weapon={WeaponName()}");
            return true;
        }

        private void UnsubscribeWieldables()
        {
            if (_wieldables != null)
                _wieldables.EquippingStopped -= OnWeaponEquipped;
            _wieldables = null;
        }

        private void OnWeaponEquipped(IWieldable wieldable)
        {
            PublishClock();
            Debug.Log($"[PLAYER] weapon -> {NameOf(wieldable)}");
        }

        private string WeaponName()
        {
            if (_wieldables == null)
                return Unknown;

            IWieldable active = _wieldables.ActiveWieldable;
            return active == null ? "unarmed" : NameOf(active);
        }

        private static string NameOf(IWieldable wieldable)
        {
            var component = wieldable as Component;
            string n = component != null ? component.gameObject.name : Unknown;
            return n.EndsWith(CloneSuffix, StringComparison.Ordinal) ? n.Substring(0, n.Length - CloneSuffix.Length) : n;
        }

        private string PositionAndBiome()
        {
            if (_wieldables == null)
                return "pos ? biome ?";

            Vector3 p = _wieldables.transform.position;
            return $"pos ({p.x:0},{p.y:0},{p.z:0}) biome {BiomeAt(p)}";
        }

        // Dominant terrain layer under the player. The biome masks are painted terrain layers, so the
        // layer with the highest weight names the biome.
        private static string BiomeAt(Vector3 world)
        {
            try
            {
                Terrain terrain = Terrain.activeTerrain;
                if (terrain == null)
                    return "-";

                TerrainData data = terrain.terrainData;
                Vector3 local = world - terrain.transform.position;
                int x = Mathf.Clamp((int)(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
                int z = Mathf.Clamp((int)(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
                float[,,] weights = data.GetAlphamaps(x, z, 1, 1);

                int best = 0;
                for (int i = 1; i < weights.GetLength(2); i++)
                {
                    if (weights[0, 0, i] > weights[0, 0, best])
                        best = i;
                }

                TerrainLayer[] layers = data.terrainLayers;
                return best < layers.Length && layers[best] != null ? layers[best].name : Unknown;
            }
            catch (Exception)
            {
                return Unknown;
            }
        }

        // --- enemies ------------------------------------------------------------------------

        private void OnEnemySpawned(EnemyIdentity enemy)
        {
            if (!_aliveEnemies.Add(enemy))
                return;

            PublishClock();
            Scene scene = enemy.gameObject.scene;
            StatsFor(scene.handle).Spawned++;
            Vector3 p = enemy.transform.position;
            Debug.Log($"[ENEMY] SPAWN {enemy.Kind} '{enemy.name}' at ({p.x:0},{p.y:0},{p.z:0}) scene={scene.name} | {EnemyCounts(scene.handle)}");
        }

        private void OnEnemyDied(EnemyHealth health)
        {
            var enemy = health.GetComponent<EnemyIdentity>();
            if (enemy != null)
                _aliveEnemies.Remove(enemy);
            _corpses.Add(health);

            PublishClock();
            Scene scene = health.gameObject.scene;
            SceneStats stats = StatsFor(scene.handle);
            stats.Killed++;

            TryResolveWieldables();
            GameObject attacker = health.LastAttacker;
            bool byPlayer = _wieldables != null && attacker != null && attacker == _wieldables.transform.root.gameObject;
            string killer = attacker == null ? "unknown" : byPlayer ? "Player" : attacker.name;
            string weapon = byPlayer ? WeaponName() : "-";
            if (byPlayer)
            {
                stats.KillsByWeapon.TryGetValue(weapon, out int n);
                stats.KillsByWeapon[weapon] = n + 1;
            }

            Debug.Log($"[ENEMY] KILL {(enemy != null ? enemy.Kind.ToString() : Unknown)} '{health.name}' by={killer} weapon={weapon} scene={scene.name} | {EnemyCounts(scene.handle)}");
        }

        // Silent on purpose: scene unloads and corpse cleanup would flood the log. The tally is
        // still corrected, and every SPAWN/KILL/PERF line carries the live count.
        private void OnEnemyDespawned(EnemyIdentity enemy) => _aliveEnemies.Remove(enemy);

        private SceneStats StatsFor(int sceneHandle)
        {
            if (!_sceneStats.TryGetValue(sceneHandle, out SceneStats stats))
                _sceneStats[sceneHandle] = stats = new SceneStats();
            return stats;
        }

        private int AliveIn(int sceneHandle)
        {
            int count = 0;
            foreach (EnemyIdentity e in _aliveEnemies)
            {
                if (e != null && e.gameObject.scene.handle == sceneHandle)
                    count++;
            }

            return count;
        }

        private int CorpsesIn(int sceneHandle)
        {
            int count = 0;
            foreach (EnemyHealth h in _corpses)
            {
                if (h != null && h.gameObject.scene.handle == sceneHandle)
                    count++;
            }

            return count;
        }

        private string EnemyCounts(int sceneHandle)
        {
            var perKind = new int[Enum.GetValues(typeof(EnemyKind)).Length];
            int total = 0;
            foreach (EnemyIdentity e in _aliveEnemies)
            {
                if (e == null || e.gameObject.scene.handle != sceneHandle)
                    continue;

                perKind[(int)e.Kind]++;
                total++;
            }

            var kinds = new StringBuilder();
            for (int i = 0; i < perKind.Length; i++)
            {
                if (perKind[i] > 0)
                    kinds.Append(kinds.Length > 0 ? ", " : "").Append((EnemyKind)i).Append(' ').Append(perKind[i]);
            }

            SceneStats stats = StatsFor(sceneHandle);
            return $"alive {total}{(kinds.Length > 0 ? $" ({kinds})" : "")}, corpses {CorpsesIn(sceneHandle)} | scene so far: {stats.Spawned} spawned, {stats.Killed} killed";
        }

        // --- perf ---------------------------------------------------------------------------

        private void ResetSceneStats()
        {
            _sceneFrameTimes.Clear();
            _windowStartIndex = 0;
            _windowElapsed = 0f;
            _sceneEnteredAt = Time.realtimeSinceStartup;
            _sceneSumDt = 0.0;
            _sceneWorstDt = 0f;
            ResetWindowAccumulators();
        }

        private void ResetWindowAccumulators()
        {
            _drawSum = _setPassSum = _triSum = _batchSum = 0.0;
            _renderSamples = 0;
            _cpuSum = _gpuSum = 0.0;
            _cpuMax = _gpuMax = 0f;
            _timingSamples = 0;
            _updateTicks = 0;
            _updateSamples = 0;
        }

        private void SampleRenderStats()
        {
            if (!_drawCalls.Valid)
                return;

            _drawSum += _drawCalls.LastValue;
            _setPassSum += _setPassCalls.Valid ? _setPassCalls.LastValue : 0;
            _triSum += _triangles.Valid ? _triangles.LastValue : 0;
            _batchSum += _batches.Valid ? _batches.LastValue : 0;
            _renderSamples++;
        }

        private void SampleFrameTiming()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) < 1)
                return;

            float cpu = (float)_timing[0].cpuFrameTime;
            float gpu = (float)_timing[0].gpuFrameTime;
            if (cpu <= 0f)
                return;

            _cpuSum += cpu;
            _gpuSum += gpu;
            if (cpu > _cpuMax) _cpuMax = cpu;
            if (gpu > _gpuMax) _gpuMax = gpu;
            _timingSamples++;
        }

        private void WritePerfWindow(string label)
        {
            int count = _sceneFrameTimes.Count - _windowStartIndex;
            if (count <= 0)
            {
                _windowElapsed = 0f;
                return;
            }

            double sum = 0.0;
            float worst = 0f;
            float best = float.MaxValue;
            var window = new float[count];
            for (int i = 0; i < count; i++)
            {
                float dt = _sceneFrameTimes[_windowStartIndex + i];
                window[i] = dt;
                sum += dt;
                if (dt > worst) worst = dt;
                if (dt < best) best = dt;
            }

            Array.Sort(window);
            float p99 = window[Mathf.Min(count - 1, (int)(count * OnePercentLowFraction))];
            double avgDt = sum / count;
            long gcMb = GC.GetTotalMemory(false) / (1024 * 1024);

            string render = _renderSamples > 0
                ? $"draws {_drawSum / _renderSamples:0} setpass {_setPassSum / _renderSamples:0} tris {_triSum / _renderSamples / 1000.0:0}k batches {_batchSum / _renderSamples:0}"
                : "render stats n/a";
            string timing = _timingSamples > 0
                ? $"cpu {_cpuSum / _timingSamples:0.0}/{_cpuMax:0.0} ms gpu {_gpuSum / _timingSamples:0.0}/{_gpuMax:0.0} ms (avg/max)"
                : "cpu/gpu timing n/a";

            long writeStart = Stopwatch.GetTimestamp();
            double logUsPerFrame = _updateSamples > 0 ? _updateTicks * 1e6 / Stopwatch.Frequency / _updateSamples : 0.0;
            double lastWriteMs = _lastWindowWriteTicks * 1e3 / Stopwatch.Frequency;

            TryResolveWieldables();
            Debug.Log(
                $"[PERF] {label}: {count} frames over {sum:0.0}s | avg {1.0 / avgDt:0.0} fps ({avgDt * 1000.0:0.00} ms) | " +
                $"1% low {1f / p99:0.0} fps ({p99 * 1000f:0.00} ms) | best {1f / best:0.0} fps | worst frame {worst * 1000f:0.0} ms | " +
                $"{timing} | {render} | managed {gcMb} MB | {PositionAndBiome()} | weapon {WeaponName()} | enemies {EnemyCounts(_activeSceneHandle)} | logger cost {logUsPerFrame:0.0} us/frame, last window write {lastWriteMs:0.00} ms");

            _lastWindowWriteTicks = Stopwatch.GetTimestamp() - writeStart;
            _windowStartIndex = _sceneFrameTimes.Count;
            _windowElapsed = 0f;
            ResetWindowAccumulators();
        }

        private void WriteSceneSummary()
        {
            int count = _sceneFrameTimes.Count;
            if (count == 0)
                return;

            var sorted = _sceneFrameTimes.ToArray();
            Array.Sort(sorted);
            float p99 = sorted[Mathf.Min(count - 1, (int)(count * OnePercentLowFraction))];
            double avgDt = _sceneSumDt / count;

            SceneStats stats = StatsFor(_activeSceneHandle);
            var weapons = new StringBuilder();
            foreach (KeyValuePair<string, int> kv in stats.KillsByWeapon)
                weapons.Append(weapons.Length > 0 ? ", " : "").Append(kv.Key).Append(' ').Append(kv.Value);

            Debug.Log(
                $"[SCENE] SUMMARY {_sceneTag}: {Time.realtimeSinceStartup - _sceneEnteredAt:0.0}s in scene, {count} frames (warm-up excluded) | " +
                $"avg {1.0 / avgDt:0.0} fps | 1% low {1f / p99:0.0} fps | worst frame {_sceneWorstDt * 1000f:0.0} ms | " +
                $"enemies: {stats.Spawned} spawned, {stats.Killed} killed, {AliveIn(_activeSceneHandle)} alive, {CorpsesIn(_activeSceneHandle)} corpses at exit | " +
                $"kills by weapon: {(weapons.Length > 0 ? weapons.ToString() : "none")}");
        }

        // --- file ---------------------------------------------------------------------------

        private static void OpenFile()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, LogFolderName);
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                Debug.Log($"[SESSIONLOG] writing to {path}");
            }
            catch (Exception e)
            {
                _writer = null;
                Debug.LogWarning($"[SESSIONLOG] could not open a log file, Player.log only: {e.Message}");
            }
        }

        private static void CloseFile()
        {
            lock (WriterLock)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }

        private static void WriteHeader()
        {
            var sb = new StringBuilder();
            sb.Append($"[SESSION] {Application.productName} {Application.version} | Unity {Application.unityVersion} | ")
              .Append($"{Application.platform} | {(Application.isEditor ? "EDITOR" : "BUILD")}{(Debug.isDebugBuild ? " (development)" : "")}\n");
            sb.Append($"[SESSION] GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB, {SystemInfo.graphicsDeviceType}) | ")
              .Append($"CPU {SystemInfo.processorType} x{SystemInfo.processorCount} | RAM {SystemInfo.systemMemorySize} MB\n");
            sb.Append($"[SESSION] {Screen.currentResolution} | fullscreen {Screen.fullScreenMode} | vSync {QualitySettings.vSyncCount} | ")
              .Append($"targetFrameRate {Application.targetFrameRate} | quality '{QualitySettings.names[QualitySettings.GetQualityLevel()]}' | ")
              .Append($"frame timing {(FrameTimingManager.IsFeatureEnabled() ? "on" : "OFF")}");
            Debug.Log(sb.ToString());
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (_writer == null)
                return;

            var line = new StringBuilder(condition.Length + 64);
            line.Append(DateTime.UtcNow.ToString("HH:mm:ss.fff"))
                .Append("Z t=").Append((_realtimeMillis / 1000.0).ToString("0.000"))
                .Append("s f=").Append(_frame)
                .Append(" [").Append(_sceneTag).Append("] ")
                .Append(type == LogType.Log ? "INFO" : type.ToString().ToUpperInvariant())
                .Append(' ').Append(condition);

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                line.Append('\n').Append(stackTrace);

            lock (WriterLock)
            {
                try { _writer?.WriteLine(line.ToString()); }
                catch (IOException) { /* disk full / file locked: never let logging break the game */ }
            }
        }
    }
}
