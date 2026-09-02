using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MrMoonlight.Events.Verbs
{
    /// <summary>
    /// <c>complete &lt;objective_id&gt;</c> — finishes an objective outright. Owner: MRM-11.
    ///
    /// <para>For objectives whose condition is not a kill count: reaching a place, answering the
    /// radio, getting the boots on. The script decides when they are done, because only the script
    /// knows.</para>
    /// </summary>
    public sealed class CompleteVerb : EventVerb
    {
        public override string Verb => "complete";

        public override string Summary => "complete <objective_id> — finish an objective that is not a kill count.";

        public override IEnumerator Run(EventStep step, EventContext context)
        {
            ObjectiveTracker tracker = context.Director.Objectives;
            if (tracker == null)
            {
                Debug.LogError($"[EventDirector] {step.Where}: no Objective Tracker on the director.");
                yield break;
            }

            tracker.Complete(step.GetValueOr("id"));
        }

        public override void Validate(EventStep step, EventScript script, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(step.GetValueOr("id")))
            {
                errors.Add($"{step.Where}: complete needs the objective's id.\n    {step.SourceText}");
            }
        }
    }
}
