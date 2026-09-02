using System;
using System.Collections.Generic;
using UnityEngine;

namespace MrMoonlight.Events
{
    /// <summary>
    /// The one string-keyed channel in the project, and it is string-keyed on purpose: the event
    /// script is authored in a text file, so the names it waits on cannot be C# symbols. Owner: MRM-11.
    ///
    /// <para><b>This is not the general message bus that <c>Docs/csharp-conventions.md</c> forbids.</b>
    /// Nothing in gameplay code subscribes here. It exists solely so an authored line
    /// (<c>wait signal=radio_answered</c>) and a scene object (<see cref="EventZone"/>, a pickup,
    /// an <see cref="EventSignalReceiver"/>) can meet by name. Systems still talk to each other
    /// with typed events.</para>
    ///
    /// <para><b>Signals latch.</b> Once raised, a signal stays raised for the rest of the level,
    /// and a <c>wait</c> on an already-raised signal returns immediately. This is deliberate
    /// deadlock protection: the alternative — the player walks through the trigger volume a
    /// second before the director reaches the line that waits on it — hangs the level forever
    /// with no feedback, which MRM-11 calls out as the most likely way the demo breaks in front
    /// of a stranger. The cost is that a signal cannot be waited on twice; if that is ever
    /// needed, raise a second, differently-named signal.</para>
    /// </summary>
    public static class EventSignals
    {
        private static readonly HashSet<string> Raised = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Fired every time a signal is raised, including repeats. Carries the signal name.</summary>
        public static event Action<string> SignalRaised;

        public static void Raise(string signal)
        {
            if (string.IsNullOrWhiteSpace(signal))
            {
                Debug.LogError("[EventDirector] EventSignals.Raise called with an empty name.");
                return;
            }

            signal = signal.Trim();
            Raised.Add(signal);
            SignalRaised?.Invoke(signal);
        }

        /// <summary>True if this signal has been raised at least once since the level loaded.</summary>
        public static bool HasFired(string signal) =>
            !string.IsNullOrWhiteSpace(signal) && Raised.Contains(signal.Trim());

        /// <summary>
        /// Forgets which signals have been raised, so a scene restart does not resolve every wait
        /// on the first frame. Called by the director in <c>Awake</c>.
        ///
        /// <para>Deliberately does <b>not</b> drop subscribers. Awake/OnEnable order between scene
        /// objects is undefined, so an <see cref="EventSignalReceiver"/> that happens to wake up
        /// before the director would have its subscription silently deleted — a receiver that
        /// works or does not depending on hierarchy order is exactly the kind of bug nobody finds.
        /// Stale subscribers are handled by <see cref="ResetOnLoad"/> instead, which runs before
        /// any scene object exists.</para>
        /// </summary>
        public static void ResetLatched()
        {
            Raised.Clear();
        }

        /// <summary>
        /// Full reset, subscribers included. Runs before the first scene of a session — which is
        /// what covers Enter Play Mode with domain reload switched off, where a subscriber from
        /// the previous session would otherwise survive into this one.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Raised.Clear();
            SignalRaised = null;
        }
    }
}
