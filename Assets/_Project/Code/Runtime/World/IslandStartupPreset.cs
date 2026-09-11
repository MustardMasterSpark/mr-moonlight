using MrMoonlight.Data;
using MrMoonlight.Runtime;
using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// DEMO-ONLY, LEGACY — added 2026-09-10 for the class-demo wrap-up. Every time the Island scene
    /// starts, picks one of exactly two looks at random and applies it instantly, before the player
    /// can do anything:
    /// <list type="bullet">
    /// <item>Morning, fog on</item>
    /// <item>Night, fog off</item>
    /// </list>
    /// Carlos's ask was specifically these two combinations, not "any preset with any fog state" —
    /// Sunset and Apocalypse are still reachable via the F8 cheat cycle, just never chosen as the
    /// scene's own starting condition. Cheat keys (F6 fog, F7 CRT, F8 time-of-day) all read the
    /// live state of <see cref="TimeManager"/>/<see cref="SceneEffectsToggle"/> rather than a cached
    /// value, so they keep working normally after this runs once at startup.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/World/Island Startup Preset (LEGACY, demo-only)")]
    public sealed class IslandStartupPreset : MonoBehaviour
    {
        [SerializeField] private TimeManager timeManager;
        [SerializeField] private SceneEffectsToggle sceneEffects;

        private void Awake()
        {
            if (timeManager == null) timeManager = FindFirstObjectByType<TimeManager>(FindObjectsInactive.Include);
            if (sceneEffects == null) sceneEffects = FindFirstObjectByType<SceneEffectsToggle>(FindObjectsInactive.Include);

            if (timeManager == null || sceneEffects == null)
            {
                Debug.LogWarning($"[{nameof(IslandStartupPreset)}] Missing TimeManager or SceneEffectsToggle — startup preset skipped.", this);
                return;
            }

            bool morning = Random.value < 0.5f;
            timeManager.ApplyPreset(morning ? "Morning" : "Night", 0f);
            sceneEffects.FogEnabled = morning;
        }
    }
}
