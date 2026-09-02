using System.Collections;

namespace MrMoonlight.Events.Verbs
{
    /// <summary>
    /// <c>stop [sequence]</c> — ends this sequence here, or another one by name. Owner: MRM-11.
    ///
    /// <para>Useful mainly as an early exit inside a branch, so a sequence can end without the
    /// author having to arrange for the file to run out of lines at the right moment.</para>
    /// </summary>
    public sealed class StopVerb : EventVerb
    {
        public override string Verb => "stop";

        public override string Summary => "stop [sequence] — end this sequence, or another one by name.";

        public override IEnumerator Run(EventStep step, EventContext context)
        {
            string target = step.GetValueOr("sequence");

            if (string.IsNullOrWhiteSpace(target)) context.Sequence.Stop();
            else context.Director.StopSequence(target);

            yield break;
        }
    }
}
