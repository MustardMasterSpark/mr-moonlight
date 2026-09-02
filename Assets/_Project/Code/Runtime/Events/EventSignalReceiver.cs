using UnityEngine;

namespace MrMoonlight.Events
{
    /// <summary>
    /// Turns a <c>signal</c> line in the event script into whatever Carlos wires in the inspector:
    /// enable a GameObject, start a particle system, open a door, play an AudioSource. Owner: MRM-11.
    ///
    /// <para>This is the escape hatch for one-off level moments that need <i>no logic</i>, only
    /// wiring. It exists so the answer to "can the script do X here?" is usually "yes, drop one of
    /// these" instead of "I'll write you a verb". A one-off that needs real <i>behaviour</i> still
    /// gets a custom <c>!verb</c> — see <see cref="EventVerbRegistry"/>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/Events/Event Signal Receiver")]
    public sealed class EventSignalReceiver : MonoBehaviour
    {
        [Tooltip("Signal name this listens for: 'signal <this>' in the event script. Defaults to the GameObject's name when left empty.")]
        [SerializeField] private string signalName;

        [Tooltip("Fires when the signal is raised. Wire anything here.")]
        [SerializeField] private UnityEngine.Events.UnityEvent onSignal;

        [Tooltip("Respond only the first time. The signal itself latches regardless.")]
        [SerializeField] private bool once = true;

        private bool _fired;

        public string SignalName => string.IsNullOrWhiteSpace(signalName) ? name : signalName;

        private void OnEnable()
        {
            EventSignals.SignalRaised += HandleSignal;

            // A signal raised before this object woke up would otherwise be missed entirely.
            if (EventSignals.HasFired(SignalName)) HandleSignal(SignalName);
        }

        private void OnDisable() => EventSignals.SignalRaised -= HandleSignal;

        private void HandleSignal(string raised)
        {
            if (!string.Equals(raised, SignalName, System.StringComparison.OrdinalIgnoreCase)) return;
            if (once && _fired) return;

            _fired = true;
            onSignal?.Invoke();
        }
    }
}
