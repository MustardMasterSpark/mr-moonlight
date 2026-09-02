using System.Collections;
using System.Collections.Generic;

namespace MrMoonlight.Events.Verbs
{
    /// <summary>
    /// <c>signal &lt;name&gt;</c> — raises a named signal. Owner: MRM-11.
    ///
    /// <para>Two things listen: any <see cref="EventSignalReceiver"/> in the scene, whose
    /// inspector-wired UnityEvent fires; and any <c>wait signal=&lt;name&gt;</c> in another
    /// sequence, which unblocks. Between them that covers most one-off level moments without
    /// anyone writing a new verb.</para>
    /// </summary>
    public sealed class SignalVerb : EventVerb
    {
        public override string Verb => "signal";

        public override string Summary => "signal <name> — raise a named signal for scene receivers and waiting sequences.";

        public override IEnumerator Run(EventStep step, EventContext context)
        {
            EventSignals.Raise(step.GetValueOr("name"));
            yield break;
        }

        public override void Validate(EventStep step, EventScript script, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(step.GetValueOr("name")))
            {
                errors.Add($"{step.Where}: signal needs a name.\n    {step.SourceText}");
            }
        }
    }
}
