using System;
using System.Collections.Generic;
using MrMoonlight.Enemies;
using UnityEngine;

namespace MrMoonlight.Events
{
    /// <summary>
    /// Holds the level's objectives and counts kills against them. Owner: MRM-11, seeds MRM-14
    /// (which owns the pause-menu display and the full 17-objective list).
    ///
    /// <para>Kills are counted from <see cref="EnemyHealth.AnyDied"/> rather than from per-enemy
    /// UnityEvents wired in the inspector. That is the only way a runtime-spawned enemy counts —
    /// a reinforcement wave has no inspector to wire — and it means Carlos never has to remember
    /// to hook up a newly placed Spotter.</para>
    ///
    /// <para>Every death counts, not only the player's. A Spotter caught in a friendly crossfire
    /// still leaves one fewer old timer on the island, and making the player prove authorship of
    /// each kill would read as the objective being broken. Pass <c>byplayer=true</c> on the
    /// <c>objective</c> line if a future objective genuinely needs the stricter rule.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/Events/Objective Tracker")]
    public sealed class ObjectiveTracker : MonoBehaviour
    {
        private readonly List<Objective> _objectives = new List<Objective>();
        private readonly Dictionary<string, Objective> _byId = new Dictionary<string, Objective>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _playerOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised when an objective is set, so the HUD can announce it.</summary>
        public event Action<Objective> ObjectiveSet;

        /// <summary>Raised when a kill objective's count changes. MRM-14's HUD ticker hooks here.</summary>
        public event Action<Objective> ObjectiveProgressed;

        /// <summary>Raised once per objective, when it completes.</summary>
        public event Action<Objective> ObjectiveCompleted;

        /// <summary>Every objective the level has set so far, in the order it set them.</summary>
        public IReadOnlyList<Objective> All => _objectives;

        /// <summary>The most recently set objective that is not finished yet. Null once everything is done.</summary>
        public Objective Current
        {
            get
            {
                for (int i = _objectives.Count - 1; i >= 0; i--)
                {
                    if (!_objectives[i].Completed) return _objectives[i];
                }

                return null;
            }
        }

        private void OnEnable() => EnemyHealth.AnyDied += HandleEnemyDied;

        private void OnDisable() => EnemyHealth.AnyDied -= HandleEnemyDied;

        public Objective Set(string id, string text, int killTarget, EnemyKind? kind, bool playerKillsOnly)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("[EventDirector] An objective was set with no id. Give it one so later lines can refer to it.", this);
                return null;
            }

            if (_byId.TryGetValue(id, out Objective existing))
            {
                Debug.LogWarning($"[EventDirector] Objective '{id}' was set twice. Keeping the first one — use a different id, or 'complete {id}' before re-setting it.", this);
                return existing;
            }

            var objective = new Objective(id, text, killTarget, kind);
            _objectives.Add(objective);
            _byId.Add(id, objective);
            if (playerKillsOnly) _playerOnly.Add(id);

            ObjectiveSet?.Invoke(objective);
            return objective;
        }

        public bool TryGet(string id, out Objective objective) =>
            _byId.TryGetValue(id ?? string.Empty, out objective);

        /// <summary>True when the objective exists and is finished. An unknown id is never complete — see <see cref="Exists"/>.</summary>
        public bool IsComplete(string id) => TryGet(id, out Objective objective) && objective.Completed;

        public bool Exists(string id) => _byId.ContainsKey(id ?? string.Empty);

        /// <summary>Completes an objective outright, for objectives whose condition is not a kill count.</summary>
        public void Complete(string id)
        {
            if (!TryGet(id, out Objective objective))
            {
                Debug.LogError($"[EventDirector] Cannot complete objective '{id}' — no objective by that id has been set.", this);
                return;
            }

            CompleteInternal(objective);
        }

        private void HandleEnemyDied(EnemyHealth enemy)
        {
            if (enemy == null) return;

            EnemyKind? kind = enemy.TryGetComponent(out EnemyIdentity identity) ? identity.Kind : (EnemyKind?)null;
            bool killedByPlayer = enemy.LastAttacker != null && enemy.LastAttacker.CompareTag("Player");

            // Snapshot: completing an objective can set the next one, which would otherwise
            // mutate the list we are walking.
            for (int i = _objectives.Count - 1; i >= 0; i--)
            {
                Objective objective = _objectives[i];
                if (objective.Completed || !objective.IsKillObjective) continue;
                if (objective.Kind.HasValue && objective.Kind != kind) continue;
                if (_playerOnly.Contains(objective.Id) && !killedByPlayer) continue;

                objective.Kills++;
                ObjectiveProgressed?.Invoke(objective);

                if (objective.Kills >= objective.KillTarget) CompleteInternal(objective);
            }
        }

        private void CompleteInternal(Objective objective)
        {
            if (objective.Completed) return;

            objective.Completed = true;
            ObjectiveCompleted?.Invoke(objective);
        }
    }
}
