using DG.Tweening;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Debug-only: Call-of-Duty-style health regen — take a hit, wait a couple of seconds, then
    /// heal back up gradually while the shipped health system (bandages, no passive regen)
    /// underneath is untouched and resumes normally the moment this is toggled off. Built so
    /// Carlos can recover vision (the red damage tint) quickly between playtest passes on the
    /// Spotter fight without needing F4 invulnerability or a bandage every time. Toggle with
    /// <b>F5</b> (keyboard) or the inspector checkbox. Same category as F3/F4 — see
    /// <c>Docs/debug-tools.md</c>. Owner: MRM-34 Spotter playtest, 2026-09-02.
    ///
    /// <para>Uses DOTween's own delay (<c>SetDelay</c>) for the wait, rather than a hand-rolled
    /// timer — every hit kills whatever regen tween is running and starts a fresh delayed one, so
    /// the wait always restarts from a clean state instead of a manually-tracked float drifting
    /// out of sync with it. Tweens <see cref="Stat.BaseValue"/> directly at a fixed rate
    /// (<see cref="regenPerSecond"/>), computing the tween's duration from however much health is
    /// actually missing, so it always reads as the same speed regardless of how much needs
    /// healing.</para>
    /// </summary>
    [RequireComponent(typeof(PlayerDamageReceiver))]
    public sealed class HealthRegenDebugToggle : MonoBehaviour
    {
        [SerializeField] private bool regenEnabled;

        [Tooltip("Lives on 'MrMoonlight Systems', a child of Player — not on this object, so it's found rather than required.")]
        [SerializeField] private PlayerStats stats;

        [SerializeField] private PlayerDamageReceiver receiver;

        [Tooltip("Seconds after the last hit before regen starts.")]
        [SerializeField] private float delayAfterHit = 2f;

        [Tooltip("Health recovered per second once regen kicks in.")]
        [SerializeField] private float regenPerSecond = 25f;

        [Tooltip("Debug overlay font, so it doesn't read as generic default-Unity text. Carlos, 2026-09-02 (MRM-76).")]
        [SerializeField] private Font font;

        private Tween _regenTween;
        private GUIStyle _style;

        private void Awake()
        {
            if (stats == null) stats = GetComponentInChildren<PlayerStats>();
            if (receiver == null) receiver = GetComponent<PlayerDamageReceiver>();
        }

        private void OnEnable()
        {
            if (receiver != null) receiver.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (receiver != null) receiver.Damaged -= OnDamaged;
            _regenTween?.Kill();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.f5Key.wasPressedThisFrame) return;

            regenEnabled = !regenEnabled;
            if (regenEnabled)
            {
                StartRegenTween();
            }
            else
            {
                _regenTween?.Kill();
            }
        }

        private void OnDamaged(float amount)
        {
            if (regenEnabled) StartRegenTween();
        }

        private void StartRegenTween()
        {
            _regenTween?.Kill();
            if (stats == null) return;

            float missing = stats.Health.MaxValue - stats.Health.BaseValue;
            if (missing <= 0f) return;

            _regenTween = DOTween.To(
                    () => stats.Health.BaseValue,
                    v => stats.Health.BaseValue = v,
                    stats.Health.MaxValue,
                    missing / regenPerSecond)
                .SetDelay(delayAfterHit)
                .SetEase(Ease.Linear)
                .SetLink(gameObject);
        }

        private void OnGUI()
        {
            if (!regenEnabled) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(0.4f, 1f, 0.5f) }
            };

            var rect = new Rect(Screen.width / 2f - 250f, 100f, 500f, 30f);
            GUI.Label(rect, "HEALTH REGEN ON (F5)", _style);
        }
    }
}
