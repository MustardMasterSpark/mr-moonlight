using System.Collections;
using System.Collections.Generic;

namespace MrMoonlight.Events.Verbs
{
    /// <summary>
    /// <c>run &lt;sequence&gt;</c> — starts another sequence alongside this one. Owner: MRM-11.
    ///
    /// <para>Does not block, which is the point: this is how a background beat or an optional
    /// branch runs without the main line having to interleave it. Follow it with
    /// <c>wait sequence=&lt;name&gt;</c> when the main line does need to hold for it.</para>
    /// </summary>
    public sealed class RunVerb : EventVerb
    {
        public override string Verb => "run";

        public override string Summary => "run <sequence> — start another sequence in parallel. Never blocks.";

        public override IEnumerator Run(EventStep step, EventContext context)
        {
            context.Director.RunSequence(step.GetValueOr("sequence"));
            yield break;
        }

        public override void Validate(EventStep step, EventScript script, List<string> errors)
        {
            string name = step.GetValueOr("sequence");
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"{step.Where}: run needs a sequence name.\n    {step.SourceText}");
                return;
            }

            if (!script.Contains(name))
            {
                errors.Add($"{step.Where}: no sequence named '{name}' in this script.\n    {step.SourceText}");
            }
        }
    }
}
