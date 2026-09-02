using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Debug-only: stops enemy fire from killing Tracey, so a fight can be watched all the way
    /// through instead of ending after two shotgun shells. Toggle with <b>F4</b> or the inspector
    /// checkbox. Deliberately the same shape as <see cref="InfiniteStaminaDebugToggle"/> (F3) —
    /// same category of tool, same on-screen label, one key along.
    ///
    /// <para><b>It blocks the damage but not the hit.</b> Health never drops, yet every absorbed
    /// shot is counted and shown on screen. Silently eating damage would be worse than useless
    /// here: an enemy that is shooting and missing and an enemy that is shooting and being ignored
    /// look identical from behind an invulnerable player, and telling those apart is the whole
    /// reason to watch the fight. The counter is the test result.</para>
    ///
    /// <para>Unlike F3 this does <em>not</em> use <see cref="Stat.Lock"/>. Locking the health stat
    /// would also freeze healing, item effects and the red-tint feedback that reads from it — and
    /// would hide the hits. Gating at the damage entry point leaves the whole stat stack behaving
    /// normally underneath.</para>
    ///
    /// Owner: MRM-34, requested for the Spotter combat playtest.
    /// </summary>
    [RequireComponent(typeof(PlayerDamageReceiver))]
    public sealed class InvulnerableDebugToggle : MonoBehaviour
    {
        [SerializeField] private bool invulnerable;
        [SerializeField] private PlayerDamageReceiver receiver;

        [Tooltip("Seconds the last-hit flash stays on screen after a blocked shot.")]
        [SerializeField] private float hitFlashDuration = 0.6f;

        private float _blockedTotal;
        private int _blockedHits;
        private float _lastBlockedAt = -99f;
        private GUIStyle _style;
        private GUIStyle _flashStyle;

        private void Awake()
        {
            if (receiver == null) receiver = GetComponent<PlayerDamageReceiver>();
        }

        private void OnEnable()
        {
            if (receiver != null) receiver.DamageBlocked += OnDamageBlocked;
        }

        private void OnDisable()
        {
            if (receiver != null) receiver.DamageBlocked -= OnDamageBlocked;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame)
            {
                invulnerable = !invulnerable;
                if (invulnerable) ResetCounters();
            }

            if (receiver != null && receiver.Invulnerable != invulnerable)
            {
                receiver.Invulnerable = invulnerable;
            }
        }

        /// <summary>Clear the blocked-damage tally. Called on each toggle-on so a new fight starts from zero.</summary>
        public void ResetCounters()
        {
            _blockedTotal = 0f;
            _blockedHits = 0;
            _lastBlockedAt = -99f;
        }

        private void OnDamageBlocked(float amount)
        {
            _blockedTotal += amount;
            _blockedHits++;
            _lastBlockedAt = Time.time;
        }

        private void OnGUI()
        {
            if (!invulnerable) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.cyan }
            };

            // Sits below the F3 label rather than on top of it, so both can be on at once.
            var rect = new Rect(Screen.width / 2f - 250f, 40f, 500f, 30f);
            GUI.Label(rect, $"INVULNERABLE (F4) — blocked {_blockedHits} hits, {_blockedTotal:F0} damage", _style);

            if (Time.time - _lastBlockedAt > hitFlashDuration) return;

            _flashStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(1f, 0.35f, 0.25f) }
            };

            GUI.Label(new Rect(Screen.width / 2f - 250f, 70f, 500f, 40f), "HIT", _flashStyle);
        }
    }
}
