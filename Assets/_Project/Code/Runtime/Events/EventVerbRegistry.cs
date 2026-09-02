using System;
using System.Collections.Generic;
using MrMoonlight.Events.Verbs;
using UnityEngine;

namespace MrMoonlight.Events
{
    /// <summary>
    /// Every verb the event script understands, in one table. Owner: MRM-11.
    ///
    /// <para><b>This file is the vocabulary.</b> It is deliberately the only place a verb name
    /// appears outside its own class, so "what can I write in the script?" is answered by one
    /// grep, and so is "where is the code for this line?".</para>
    ///
    /// <para><b>Generic verbs vs custom ones.</b> A generic verb is a thing the game does often —
    /// show a message, set an objective, spawn a wave. A <b>custom verb starts with '!'</b> and is
    /// a single hand-written moment that belongs to one point in one level: <c>!seiko_alarm_off</c>,
    /// <c>!vernon_distraction</c>. The prefix earns its keep twice over — it tells Carlos at a
    /// glance which lines are bespoke, and it guarantees a one-off can never collide with a generic
    /// verb added later. Custom verbs go in the CUSTOM block at the bottom.</para>
    ///
    /// <para>Before asking for a custom verb, check whether <c>signal</c> already covers it: if the
    /// moment is pure scene wiring with no logic — enable an object, play a sound, open a door —
    /// an <see cref="EventSignalReceiver"/> does it with no code at all.</para>
    /// </summary>
    public static class EventVerbRegistry
    {
        private static readonly Dictionary<string, EventVerb> Verbs =
            new Dictionary<string, EventVerb>(StringComparer.OrdinalIgnoreCase);

        static EventVerbRegistry()
        {
            // ---- FLOW ------------------------------------------------------------------
            Register(new WaitVerb());
            Register(new RunVerb());
            Register(new StopVerb());
            Register(new LogVerb());

            // ---- TEXT AND OBJECTIVES ---------------------------------------------------
            Register(new MessageVerb());
            Register(new ObjectiveVerb());
            Register(new CompleteVerb());

            // ---- WORLD -----------------------------------------------------------------
            Register(new SpawnVerb());
            Register(new SignalVerb());

            // ---- ENDINGS ---------------------------------------------------------------
            Register(new EndLevelVerb("win", won: true));
            Register(new EndLevelVerb("lose", won: false));

            // ---- RESERVED, NOT IMPLEMENTED YET -----------------------------------------
            // Names are claimed now so the script format is stable and a future implementation
            // slots in without rewriting anyone's level. Each one names the issue that owes it.
            Register(new DeferredVerb("dialogue", "MRM-13", "spoken lines, subtitles and the voice-over pipeline"));
            Register(new DeferredVerb("sound", "MRM-15", "one-shot SFX by name"));
            Register(new DeferredVerb("music", "MRM-15", "music beds and tension layers"));
            Register(new DeferredVerb("vfx", "MRM-53/57", "named visual effects"));
            Register(new DeferredVerb("lighting", "MRM-47", "skybox swaps and lighting changes over a duration"));
            Register(new DeferredVerb("cutscene", "MRM-11", "cutscene begin/end and its control locks"));
            Register(new DeferredVerb("checkpoint", "MRM-45", "in-session respawn checkpoints"));
            Register(new DeferredVerb("grant", "MRM-41/42", "giving the player an item, weapon or capability"));
            Register(new DeferredVerb("stat", "MRM-12", "setting, locking and unlocking a player stat"));

            // ---- CUSTOM (one-off, level-specific) --------------------------------------
            // Add hand-written moments here, named with a leading '!'. None yet.
        }

        public static bool TryGet(string verb, out EventVerb handler) =>
            Verbs.TryGetValue(verb ?? string.Empty, out handler);

        public static IEnumerable<EventVerb> All => Verbs.Values;

        /// <summary>Comma-separated verb names, for the "unknown verb" error message.</summary>
        public static string DescribeVerbNames()
        {
            var names = new List<string>(Verbs.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", names);
        }

        private static void Register(EventVerb verb)
        {
            if (Verbs.ContainsKey(verb.Verb))
            {
                Debug.LogError($"[EventDirector] Two verbs are both registered as '{verb.Verb}'. The second one is ignored.");
                return;
            }

            Verbs.Add(verb.Verb, verb);
        }
    }
}
