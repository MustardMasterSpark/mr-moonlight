using System;
using System.Collections;
using System.Collections.Generic;
using MrMoonlight.Data;
using MrMoonlight.Enemies;
using UnityEngine;

namespace MrMoonlight.Events.Verbs
{
    /// <summary>
    /// <c>wait seconds=N | objective=id | kills=N [kind=Spotter] | zone=name | signal=name | group=name | sequence=name</c>
    /// with an optional <c>timeout=seconds</c>. Owner: MRM-11.
    ///
    /// <para><b>The only verb that blocks.</b> Everything else fires and the sequence moves on, so
    /// the flow of a script is legible by scanning the left column for this one word.</para>
    ///
    /// <para><b>Deadlock safety</b> is MRM-11's first-class concern — "the most likely way the demo
    /// breaks in front of a stranger" — and the policy here is per-condition rather than one blanket
    /// timeout, because the two kinds of wait fail in opposite directions:</para>
    /// <list type="bullet">
    /// <item><description><b>World waits</b> (<c>zone</c>, <c>signal</c>, <c>sequence</c>) can hang
    /// on a mis-typed name or a volume the player squeezed past, and nothing on screen would say
    /// so. They time out after <see cref="MoonlightTunables.EventWaitDefaultTimeout"/>, log a loud
    /// error naming the file and line, and continue.</description></item>
    /// <item><description><b>Progress waits</b> (<c>objective</c>, <c>kills</c>, <c>group</c>) wait
    /// forever by default, deliberately. They resolve only when the player does the thing, so a
    /// timeout would hand out a win nobody earned — a worse failure than a pause. They cannot hang
    /// on a typo either: an unknown objective or group id is a load-time error, not a silent
    /// stall. That is the written justification MRM-11's acceptance criteria ask for.</description></item>
    /// </list>
    /// <para>Either default can be overridden per line with <c>timeout=N</c>, and
    /// <c>timeout=0</c> means "wait forever" explicitly.</para>
    /// </summary>
    public sealed class WaitVerb : EventVerb
    {
        public override string Verb => "wait";

        public override string Summary =>
            "wait seconds=N | objective=id | kills=N [kind=] | zone=name | signal=name | group=name | sequence=name [timeout=N] — the only line that blocks.";

        public override IEnumerator Run(EventStep step, EventContext context)
        {
            if (step.Has("seconds"))
            {
                yield return new WaitForSeconds(step.GetFloat("seconds", 0f));
                yield break;
            }

            // Its own branch, because it is the only condition that has to hold a subscription for
            // the length of the wait and hand it back afterwards — including on a timeout.
            if (step.Has("kills"))
            {
                yield return WaitForKills(step);
                yield break;
            }

            if (!TryBuildCondition(step, context, out Func<bool> ready, out string description, out bool isProgressWait))
            {
                Debug.LogError($"[EventDirector] {step.Where}: wait has no condition it can act on — skipped.\n    {step.SourceText}");
                yield break;
            }

            float timeout = ResolveTimeout(step, isProgressWait);
            float elapsed = 0f;

            while (!ready())
            {
                elapsed += Time.deltaTime;

                if (timeout > 0f && elapsed >= timeout)
                {
                    Debug.LogError(
                        $"[EventDirector] {step.Where}: waited {timeout:0.#}s for {description} and it never happened. " +
                        "Carrying on so the level is not stuck — but the condition did not actually resolve, so what follows may be wrong.\n" +
                        $"    {step.SourceText}");
                    yield break;
                }

                yield return null;
            }
        }

        public override void Validate(EventStep step, EventScript script, List<string> errors)
        {
            bool hasCondition =
                step.Has("seconds") || step.Has("objective") || step.Has("kills") ||
                step.Has("zone") || step.Has("signal") || step.Has("group") || step.Has("sequence");

            if (!hasCondition)
            {
                errors.Add($"{step.Where}: wait needs a condition — seconds, objective, kills, zone, signal, group or sequence.\n    {step.SourceText}");
            }

            string sequenceName = step.GetString("sequence");
            if (!string.IsNullOrWhiteSpace(sequenceName) && !script.Contains(sequenceName))
            {
                errors.Add($"{step.Where}: waits on sequence '{sequenceName}', which this script does not declare.\n    {step.SourceText}");
            }
        }

        /// <summary>
        /// <c>wait kills=N [kind=]</c> — the objective-less form: "N more have to die, starting
        /// now". Counted here rather than in <see cref="ObjectiveTracker"/> precisely because
        /// there is no objective to hang the count on.
        /// </summary>
        private static IEnumerator WaitForKills(EventStep step)
        {
            int target = step.GetInt("kills", 1);
            EnemyKind? kind = step.GetEnum<EnemyKind>("kind");
            int counted = 0;

            void OnDied(EnemyHealth enemy)
            {
                if (kind.HasValue && (!enemy.TryGetComponent(out EnemyIdentity identity) || identity.Kind != kind.Value)) return;
                counted++;
            }

            EnemyHealth.AnyDied += OnDied;
            try
            {
                float timeout = ResolveTimeout(step, isProgressWait: true);
                float elapsed = 0f;

                while (counted < target)
                {
                    elapsed += Time.deltaTime;
                    if (timeout > 0f && elapsed >= timeout)
                    {
                        Debug.LogError(
                            $"[EventDirector] {step.Where}: waited {timeout:0.#}s for {target} kills and only counted {counted}. " +
                            "Carrying on so the level is not stuck.\n    " + step.SourceText);
                        yield break;
                    }

                    yield return null;
                }
            }
            finally
            {
                EnemyHealth.AnyDied -= OnDied;
            }
        }

        private static float ResolveTimeout(EventStep step, bool isProgressWait)
        {
            if (step.Has("timeout")) return Mathf.Max(0f, step.GetFloat("timeout", 0f));

            return isProgressWait ? 0f : Tunables.I.EventWaitDefaultTimeout;
        }

        private static bool TryBuildCondition(
            EventStep step,
            EventContext context,
            out Func<bool> ready,
            out string description,
            out bool isProgressWait)
        {
            EventDirector director = context.Director;

            string objectiveId = step.GetString("objective");
            if (!string.IsNullOrWhiteSpace(objectiveId))
            {
                ObjectiveTracker tracker = director.Objectives;
                ready = () => tracker != null && tracker.IsComplete(objectiveId);
                description = $"objective '{objectiveId}' to complete";
                isProgressWait = true;
                return tracker != null;
            }

            string groupName = step.GetString("group");
            if (!string.IsNullOrWhiteSpace(groupName))
            {
                ready = () => director.IsGroupClear(groupName);
                description = $"every enemy in group '{groupName}' to die";
                isProgressWait = true;
                return true;
            }

            // zone and signal are the same channel: an EventZone raises a signal named after itself.
            string signalName = step.GetString("zone") ?? step.GetString("signal");
            if (!string.IsNullOrWhiteSpace(signalName))
            {
                ready = () => EventSignals.HasFired(signalName);
                description = $"signal '{signalName}'";
                isProgressWait = false;
                return true;
            }

            string sequenceName = step.GetString("sequence");
            if (!string.IsNullOrWhiteSpace(sequenceName))
            {
                ready = () => !director.IsRunning(sequenceName);
                description = $"sequence '{sequenceName}' to finish";
                isProgressWait = false;
                return true;
            }

            ready = null;
            description = null;
            isProgressWait = false;
            return false;
        }
    }
}
