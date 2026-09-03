using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.AI;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// Tilts the Spotter's carried lamp on its local X axis while it's socketed, so it reads as
    /// hanging from a handle and swinging as he walks rather than being welded rigidly to the hip —
    /// still at rest, swinging wider and faster the closer he is to his NavMeshAgent's cruising
    /// speed. MRM-34, added when the lamp moved from the hand socket to the hip socket
    /// (2026-09-03).
    ///
    /// Watches for a <see cref="Rigidbody"/> the same way <see cref="LampFireEffect"/> does — that's
    /// <see cref="EnemyDeathDrop"/> taking over once the lamp hits the ground, at which point this
    /// stops fighting real physics with a scripted rotation.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Enemies/Lamp Sway Effect")]
    public sealed class LampSwayEffect : MonoBehaviour
    {
        [Tooltip("Leave empty to find it on a parent automatically.")]
        [SerializeField] private NavMeshAgent agent;

        private Rigidbody _body;
        private Quaternion _restLocalRotation;
        private float _phase;

        private void Awake()
        {
            if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
            _restLocalRotation = transform.localRotation;
        }

        private void Update()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_body != null)
            {
                enabled = false;
                return;
            }

            if (agent == null) return;

            float speedFraction = Mathf.Clamp01(agent.velocity.magnitude / Tunables.I.SpotterLampSwayReferenceSpeed);

            // A stationary lamp should still drift very slightly rather than freeze mid-swing —
            // Max(speedFraction, small) keeps the phase advancing at rest instead of snapping still.
            _phase += Time.deltaTime * Tunables.I.SpotterLampSwayFrequency * Mathf.Max(speedFraction, 0.05f);

            float angle = Mathf.Sin(_phase * Mathf.PI * 2f) * Tunables.I.SpotterLampSwayMaxAngle * speedFraction;
            transform.localRotation = _restLocalRotation * Quaternion.AngleAxis(angle, Vector3.right);
        }
    }
}
