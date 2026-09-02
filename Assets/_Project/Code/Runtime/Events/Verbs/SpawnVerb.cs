using System.Collections;
using System.Collections.Generic;
using MrMoonlight.Enemies;
using UnityEngine;

namespace MrMoonlight.Events.Verbs
{
    /// <summary>
    /// <c>spawn [kind] at=&lt;point&gt; [count=N] [chase=false] [group=name]</c> — puts enemies in
    /// the world at a named <see cref="EnemySpawnPoint"/>. Owner: MRM-11.
    ///
    /// <para><c>chase</c> defaults to true: enemies spawned by a script line are almost always
    /// spawned <i>because</i> of the player, and one that stands around waiting to be noticed
    /// reads as broken. Set <c>chase=false</c> for a patrol that should not know yet.</para>
    ///
    /// <para><c>group=</c> names the wave so a later <c>wait group=&lt;name&gt;</c> can hold until
    /// all of them are dead — MRM-11's "kills all in group" condition.</para>
    ///
    /// <para>The optional positional <c>kind</c> is a cross-check, not a selector: the prefab lives
    /// on the spawn point (a text file cannot hold an asset reference), so naming the kind here
    /// only asks the director to complain if the point is pointed at something else. That catches
    /// the case where a point gets re-purposed and a script line silently starts spawning wolves.</para>
    /// </summary>
    public sealed class SpawnVerb : EventVerb
    {
        public override string Verb => "spawn";

        public override string Summary =>
            "spawn [kind] at=<point> [count=N] [chase=false] [group=name] — spawn enemies at a named spawn point.";

        public override IEnumerator Run(EventStep step, EventContext context)
        {
            string pointName = step.GetString("at");
            EnemySpawnPoint point = EnemySpawnPoint.Find(pointName);

            if (point == null)
            {
                Debug.LogError(
                    $"[EventDirector] {step.Where}: no active Enemy Spawn Point named '{pointName}'. " +
                    $"Points in the scene: {DescribePoints()}\n    {step.SourceText}");
                yield break;
            }

            int count = Mathf.Max(1, step.GetInt("count", 1));
            bool chase = step.GetBool("chase", true);

            List<EnemyHealth> spawned = point.Spawn(count, chase ? context.Director.Player : null);

            WarnOnKindMismatch(step, spawned);

            string group = step.GetString("group");
            if (!string.IsNullOrWhiteSpace(group)) context.Director.AddToGroup(group, spawned);
        }

        public override void Validate(EventStep step, EventScript script, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(step.GetString("at")))
            {
                errors.Add($"{step.Where}: spawn needs at=<spawn point name>.\n    {step.SourceText}");
            }

            if (step.Has("count") && step.GetInt("count", 1) < 1)
            {
                errors.Add($"{step.Where}: count must be 1 or more.\n    {step.SourceText}");
            }
        }

        private static void WarnOnKindMismatch(EventStep step, List<EnemyHealth> spawned)
        {
            if (string.IsNullOrWhiteSpace(step.Value) || spawned.Count == 0) return;
            if (!System.Enum.TryParse(step.Value, ignoreCase: true, out EnemyKind expected)) return;

            if (!spawned[0].TryGetComponent(out EnemyIdentity identity) || identity.Kind == expected) return;

            Debug.LogWarning(
                $"[EventDirector] {step.Where}: the line says '{expected}' but spawn point '{step.GetString("at")}' " +
                $"is set up to spawn {identity.Kind}. The point wins — fix whichever one is wrong.\n    {step.SourceText}");
        }

        private static string DescribePoints()
        {
            IReadOnlyList<EnemySpawnPoint> points = EnemySpawnPoint.AllActive;
            if (points.Count == 0) return "(none)";

            var names = new List<string>(points.Count);
            for (int i = 0; i < points.Count; i++) names.Add(points[i].PointName);
            return string.Join(", ", names);
        }
    }
}
