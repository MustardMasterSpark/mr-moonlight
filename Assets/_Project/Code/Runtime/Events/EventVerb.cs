using System.Collections;
using System.Collections.Generic;

namespace MrMoonlight.Events
{
    /// <summary>What a verb is handed when it runs: the director, and the sequence it is running inside. Owner: MRM-11.</summary>
    public readonly struct EventContext
    {
        public EventContext(EventDirector director, RunningSequence sequence)
        {
            Director = director;
            Sequence = sequence;
        }

        public EventDirector Director { get; }

        public RunningSequence Sequence { get; }
    }

    /// <summary>
    /// One verb the event script can use. Owner: MRM-11.
    ///
    /// <para>Verbs are stateless and shared — one instance each, held by
    /// <see cref="EventVerbRegistry"/>. All state lives on the director or in the scene, so two
    /// sequences running in parallel can use the same verb without stepping on each other.</para>
    ///
    /// <para><b>Adding a verb is one file.</b> Subclass this, return the name from
    /// <see cref="Verb"/>, add a line to <see cref="EventVerbRegistry"/>. That is the whole
    /// procedure, and it is the reason custom one-off events are cheap to ask for.</para>
    /// </summary>
    public abstract class EventVerb
    {
        /// <summary>The word that appears in the script, lower-case. Custom one-offs keep a leading '!'.</summary>
        public abstract string Verb { get; }

        /// <summary>One line for the reference doc and the validator's "did you mean" output.</summary>
        public abstract string Summary { get; }

        /// <summary>
        /// Does the thing. Instant verbs simply <c>yield break</c> when done; only
        /// <see cref="Verbs.WaitVerb"/> is expected to yield for any length of time, which is what
        /// makes "everything runs immediately unless the line says wait" true by construction
        /// rather than by convention.
        /// </summary>
        public abstract IEnumerator Run(EventStep step, EventContext context);

        /// <summary>
        /// Checked once at load, before anything runs, so a typo is a console error at play time
        /// rather than a silent no-op ten minutes into a playtest. Add to <paramref name="errors"/>.
        /// </summary>
        public virtual void Validate(EventStep step, EventScript script, List<string> errors)
        {
        }
    }
}
