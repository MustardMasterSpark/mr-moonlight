using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MrMoonlight.Events.Verbs
{
    /// <summary>
    /// A verb the format reserves but the runtime cannot honour yet, because the system behind it
    /// is still Backlog. Owner: MRM-11.
    ///
    /// <para><b>Why these exist as real, registered verbs instead of being left unknown.</b>
    /// MRM-11's acceptance criteria ask that every verb either work or be "explicitly deferred with
    /// a reason". Registering them does exactly that: the name is reserved so a future
    /// implementation cannot collide with something else, Carlos can write the line today and have
    /// it stay written, and the console tells him — once, naming the issue that owes him the
    /// behaviour — instead of an "unknown verb" error that reads like he mistyped it.</para>
    /// </summary>
    public sealed class DeferredVerb : EventVerb
    {
        private readonly string _verb;
        private readonly string _owner;
        private readonly string _what;
        private bool _warned;

        public DeferredVerb(string verb, string owner, string what)
        {
            _verb = verb;
            _owner = owner;
            _what = what;
        }

        public override string Verb => _verb;

        public override string Summary => $"{_verb} — {_what} (not implemented yet; {_owner} owns it).";

        public override IEnumerator Run(EventStep step, EventContext context)
        {
            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning(
                    $"[EventDirector] {step.Where}: '{_verb}' is reserved but does nothing yet — {_owner} owns {_what}. " +
                    "The line is valid and will start working when that issue lands; nothing else is wrong.\n    " + step.SourceText);
            }

            yield break;
        }

        public override void Validate(EventStep step, EventScript script, List<string> errors)
        {
        }
    }
}
