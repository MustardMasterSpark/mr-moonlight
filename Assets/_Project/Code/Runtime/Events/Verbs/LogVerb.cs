using System.Collections;
using UnityEngine;

namespace MrMoonlight.Events.Verbs
{
    /// <summary>
    /// <c>log "text"</c> — a console line. Owner: MRM-11.
    ///
    /// <para>Costs nothing and is worth its weight when a sequence does not fire: dropping a
    /// <c>log</c> either side of a suspect line answers "did the director even get here?" without
    /// anyone opening a C# file.</para>
    /// </summary>
    public sealed class LogVerb : EventVerb
    {
        public override string Verb => "log";

        public override string Summary => "log \"text\" — print a line to the console. Authoring aid.";

        public override IEnumerator Run(EventStep step, EventContext context)
        {
            Debug.Log($"[EventDirector] {context.Sequence.Name}: {step.GetValueOr("text", string.Empty)}");
            yield break;
        }
    }
}
