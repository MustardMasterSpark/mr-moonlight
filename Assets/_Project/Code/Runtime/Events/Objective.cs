using MrMoonlight.Enemies;

namespace MrMoonlight.Events
{
    /// <summary>
    /// One objective's live state. Created by the <c>objective</c> verb, completed either by its
    /// own kill counter or by an explicit <c>complete</c> line. Owner: MRM-11, seeds MRM-14.
    /// </summary>
    public sealed class Objective
    {
        public Objective(string id, string text, int killTarget, EnemyKind? kind)
        {
            Id = id;
            Text = text;
            KillTarget = killTarget;
            Kind = kind;
        }

        /// <summary>Short handle used by <c>wait objective=</c> and <c>complete</c>. Not shown to the player.</summary>
        public string Id { get; }

        /// <summary>The line the player reads. Shown on announce and, once MRM-19 lands, at the top of the pause menu.</summary>
        public string Text { get; }

        /// <summary>How many kills complete this objective. 0 means it is not a kill objective.</summary>
        public int KillTarget { get; }

        /// <summary>Which enemy counts. Null counts every enemy.</summary>
        public EnemyKind? Kind { get; }

        public int Kills { get; internal set; }

        public bool Completed { get; internal set; }

        public bool IsKillObjective => KillTarget > 0;

        /// <summary>"Kill 3 old timers (2/3)" — the announce text plus progress, for HUD and debug readouts.</summary>
        public string ProgressText => IsKillObjective ? $"{Text} ({Kills}/{KillTarget})" : Text;
    }
}
