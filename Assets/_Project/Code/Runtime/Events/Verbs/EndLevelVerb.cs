using System.Collections;

namespace MrMoonlight.Events.Verbs
{
    /// <summary>
    /// <c>win "text"</c> and <c>lose "text"</c> — end the level and show the end panel. Owner: MRM-11.
    ///
    /// <para>One class serving both, because the two endings differ only in a bool and a line of
    /// text; the panel, the pause and the two buttons are the same screen either way.</para>
    ///
    /// <para>The <i>ordinary</i> loss — the player dying — does not come through here. That path
    /// belongs to MRM-17's <c>DeathSequence</c>, which has a whole fall-and-scream sequence to run
    /// first and lands on the same panel at the end of it. <c>lose</c> is for a scripted failure:
    /// a timer running out, an objective failed on purpose.</para>
    /// </summary>
    public sealed class EndLevelVerb : EventVerb
    {
        private readonly bool _won;
        private readonly string _verb;

        public EndLevelVerb(string verb, bool won)
        {
            _verb = verb;
            _won = won;
        }

        public override string Verb => _verb;

        public override string Summary => _won
            ? "win \"text\" — end the level as a victory, pause, and show the end panel."
            : "lose \"text\" — end the level as a scripted failure. Dying is handled by the death sequence, not this.";

        public override IEnumerator Run(EventStep step, EventContext context)
        {
            context.Director.EndLevel(_won, step.GetValueOr("text", string.Empty));
            yield break;
        }
    }
}
