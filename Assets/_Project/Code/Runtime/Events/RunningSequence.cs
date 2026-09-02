using UnityEngine;

namespace MrMoonlight.Events
{
    /// <summary>
    /// A sequence that is currently executing. Owner: MRM-11.
    ///
    /// <para><see cref="Index"/> is public and settable through <see cref="JumpTo"/> because
    /// MRM-45 (in-session respawn checkpoints) needs to rewind the director to the step a
    /// checkpoint was taken at. Designed in now rather than bolted on later, per that issue's
    /// note on this one.</para>
    /// </summary>
    public sealed class RunningSequence
    {
        public RunningSequence(EventSequence sequence)
        {
            Sequence = sequence;
        }

        public EventSequence Sequence { get; }

        public string Name => Sequence.Name;

        /// <summary>Index of the step currently running.</summary>
        public int Index { get; internal set; }

        /// <summary>Set by the <c>stop</c> verb. The run loop checks it after every step.</summary>
        public bool StopRequested { get; private set; }

        /// <summary>Set by <see cref="JumpTo"/>; consumed and cleared by the run loop.</summary>
        internal int? PendingJump { get; private set; }

        public Coroutine Coroutine { get; internal set; }

        public EventStep CurrentStep =>
            Index >= 0 && Index < Sequence.Steps.Count ? Sequence.Steps[Index] : null;

        /// <summary>"main [3/9] wait objective=kill_spotters" — the director's runtime inspector readout.</summary>
        public string Describe()
        {
            EventStep step = CurrentStep;
            string body = step == null ? "(finished)" : step.SourceText;
            return $"{Name} [{Index + 1}/{Sequence.Steps.Count}] {body}";
        }

        public void Stop() => StopRequested = true;

        /// <summary>Continue from a different step after the current one finishes. Checkpoint rewind (MRM-45).</summary>
        public void JumpTo(int stepIndex) => PendingJump = stepIndex;

        internal bool TryTakeJump(out int stepIndex)
        {
            if (PendingJump.HasValue)
            {
                stepIndex = PendingJump.Value;
                PendingJump = null;
                return true;
            }

            stepIndex = 0;
            return false;
        }
    }
}
