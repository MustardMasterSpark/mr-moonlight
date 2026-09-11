using UnityEngine;

namespace MrMoonlight.Enemies.Spotter
{
    /// <summary>
    /// Plays the pooled alert sound (<see cref="EnemyAudioHooks.PlayAlert"/>) the first time this
    /// Spotter's <see cref="BlazeAI"/> leaves its passive state after spawning — Carlos's ask for the
    /// island demo wrap-up audio pass (2026-09-10): "triggered whenever the spotter detects the
    /// player." Blaze has no detection event of its own to hook, so this polls
    /// <see cref="BlazeAI.state"/> once a frame and latches the first transition into
    /// <c>State.alert</c> or <c>State.attack</c>.
    ///
    /// Deliberately separate from <see cref="SpotterPanicCall"/> (fires on low health) and
    /// <see cref="SpotterFlareCall"/> (fires on the isolation timer, which also plays this same
    /// alert sound alongside its own flare cue) — this is the very first spot, not a reaction to a
    /// later combat event. Owner: island-demo-wrapup.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BlazeAI))]
    [AddComponentMenu("Mr. Moonlight/Enemies/Spotter/Spotter Detection Alert")]
    public sealed class SpotterDetectionAlert : MonoBehaviour
    {
        private BlazeAI _blaze;
        private EnemyAudioHooks _audio;
        private EnemyHealth _health;
        private bool _hasAlerted;

        private void Awake()
        {
            _blaze = GetComponent<BlazeAI>();
            _audio = GetComponent<EnemyAudioHooks>();
            _health = GetComponent<EnemyHealth>();
        }

        private void Update()
        {
            if (_hasAlerted || _blaze == null) return;
            if (_health != null && _health.IsDead) return;

            if (_blaze.state == BlazeAI.State.alert || _blaze.state == BlazeAI.State.attack)
            {
                _hasAlerted = true;
                _audio?.PlayAlert();
            }
        }
    }
}
