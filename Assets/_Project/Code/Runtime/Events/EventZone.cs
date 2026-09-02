using UnityEngine;

namespace MrMoonlight.Events
{
    /// <summary>
    /// A collider Carlos drops in the world so the event script can react to the player being
    /// somewhere. Owner: MRM-11.
    ///
    /// <para>Entering it raises a signal named after the zone, which two different lines can then
    /// use — <c>wait zone=glade_edge</c> to gate the main sequence on the player arriving, or
    /// <c>run</c> from this component's own <see cref="sequenceToRun"/> field to fire an ambush
    /// without the main sequence knowing anything about it.</para>
    ///
    /// <para>Named <c>EventZone</c> rather than the obvious <c>EventTrigger</c> because
    /// <c>UnityEngine.EventSystems.EventTrigger</c> already owns that name and a collision would
    /// mean every UI file in the project needing a qualified using.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Mr. Moonlight/Events/Event Zone")]
    public sealed class EventZone : MonoBehaviour
    {
        [Tooltip("Name the event script refers to: 'wait zone=<this>'. Defaults to the GameObject's name when left empty.")]
        [SerializeField] private string zoneName;

        [Tooltip("Optional. Sequence to start when the player enters. Leave empty if the zone only raises its signal.")]
        [SerializeField] private string sequenceToRun;

        [Tooltip("Fire only the first time. Off means the sequence restarts on every entry — note the zone's signal latches either way.")]
        [SerializeField] private bool once = true;

        [Tooltip("Draw the zone in the scene view even when it is not selected.")]
        [SerializeField] private bool alwaysDrawGizmo = true;

        private bool _fired;

        public string ZoneName => string.IsNullOrWhiteSpace(zoneName) ? name : zoneName;

        private void Reset()
        {
            // A zone that is not a trigger would shove the player off it instead of firing.
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // The tag lives on the collider, never on a parent — a tag on an untagged-collider
            // parent is invisible to physics. Learned the hard way on the Spotter's vision, 2026-09-02.
            if (!other.CompareTag("Player")) return;
            if (once && _fired) return;

            _fired = true;
            EventSignals.Raise(ZoneName);

            if (string.IsNullOrWhiteSpace(sequenceToRun)) return;

            EventDirector director = EventDirector.Active;
            if (director == null)
            {
                Debug.LogError($"[EventDirector] Zone '{ZoneName}' wants to run sequence '{sequenceToRun}' but there is no Event Director in the scene.", this);
                return;
            }

            director.RunSequence(sequenceToRun);
        }

        private void OnDrawGizmos()
        {
            if (alwaysDrawGizmo) DrawGizmo(0.12f);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmo(0.3f);
        }

        private void DrawGizmo(float alpha)
        {
            if (!TryGetComponent(out Collider zoneCollider)) return;

            Bounds bounds = zoneCollider.bounds;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, alpha);
            Gizmos.DrawCube(bounds.center, bounds.size);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, Mathf.Min(1f, alpha * 3f));
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
