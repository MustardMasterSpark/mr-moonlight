using System.Collections;
using System.Collections.Generic;
using MrMoonlight.Data;
using MrMoonlight.Enemies;
using UnityEngine;

namespace MrMoonlight.Events.Verbs
{
    /// <summary>
    /// <c>objective &lt;id&gt; text="..." [kills=N] [kind=Spotter] [for=seconds] [announce=false] [byplayer=true]</c>
    /// Owner: MRM-11, seeds MRM-14.
    ///
    /// <para>Sets the current objective <i>and</i> announces it through the system message channel
    /// in one line, because MRM-14 specifies both halves and splitting them across two lines would
    /// mean the announcement text and the pause-menu text could silently drift apart.</para>
    ///
    /// <para>Does not block. Gate on it with <c>wait objective=&lt;id&gt;</c> when the script
    /// should not continue until it is done.</para>
    /// </summary>
    public sealed class ObjectiveVerb : EventVerb
    {
        public override string Verb => "objective";

        public override string Summary =>
            "objective <id> text=\"...\" [kills=N] [kind=Spotter] [for=seconds] [announce=false] [byplayer=true] — set and announce an objective.";

        public override IEnumerator Run(EventStep step, EventContext context)
        {
            ObjectiveTracker tracker = context.Director.Objectives;
            if (tracker == null)
            {
                Debug.LogError($"[EventDirector] {step.Where}: no Objective Tracker on the director.");
                yield break;
            }

            string id = step.GetValueOr("id");
            string text = step.GetString("text", id);
            int kills = step.GetInt("kills", 0);
            EnemyKind? kind = step.GetEnum<EnemyKind>("kind");
            bool playerOnly = step.GetBool("byplayer", false);

            Objective objective = tracker.Set(id, text, kills, kind, playerOnly);
            if (objective == null) yield break;

            if (!step.GetBool("announce", true)) yield break;

            if (context.Director.Messages == null)
            {
                Debug.LogWarning($"[EventDirector] {step.Where}: objective '{id}' was set but there is no System Message UI to announce it through.");
                yield break;
            }

            context.Director.Messages.Show(
                text,
                step.GetFloat("for", Tunables.I.ObjectiveAnnounceDuration),
                step.GetColor("color"));
        }

        public override void Validate(EventStep step, EventScript script, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(step.GetValueOr("id")))
            {
                errors.Add($"{step.Where}: objective has no id. Write it as the first value: objective kill_spotters text=\"...\"\n    {step.SourceText}");
            }

            if (step.Has("kills") && step.GetInt("kills", 0) <= 0)
            {
                errors.Add($"{step.Where}: kills must be 1 or more. Leave it off entirely for an objective that is not a kill count.\n    {step.SourceText}");
            }
        }
    }
}
