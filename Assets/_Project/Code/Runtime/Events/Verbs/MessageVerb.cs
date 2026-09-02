using System.Collections;
using System.Collections.Generic;
using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.Events.Verbs
{
    /// <summary>
    /// <c>message "text" [for=seconds] [color=#66CCFF]</c> — a system message, centre-bottom, in
    /// the style of Silent Hill 1's subtitles. Owner: MRM-11, seeds MRM-14.
    ///
    /// <para>Does not block. A message is something the player reads while carrying on; if the
    /// script needs to hold on it, the next line is a <c>wait</c>.</para>
    /// </summary>
    public sealed class MessageVerb : EventVerb
    {
        public override string Verb => "message";

        public override string Summary => "message \"text\" [for=seconds] [color=#RRGGBB] — centre-bottom system message.";

        public override IEnumerator Run(EventStep step, EventContext context)
        {
            string text = step.GetValueOr("text");
            float duration = step.GetFloat("for", Tunables.I.SystemMessageDefaultDuration);

            if (context.Director.Messages == null)
            {
                Debug.LogError($"[EventDirector] {step.Where}: no System Message UI assigned on the director — \"{text}\" was never shown.");
                yield break;
            }

            context.Director.Messages.Show(text, duration, step.GetColor("color"));
        }

        public override void Validate(EventStep step, EventScript script, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(step.GetValueOr("text")))
            {
                errors.Add($"{step.Where}: message has no text. Write it as the first value: message \"...\"\n    {step.SourceText}");
            }
        }
    }
}
