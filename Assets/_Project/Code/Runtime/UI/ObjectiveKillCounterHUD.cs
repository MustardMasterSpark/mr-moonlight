using MrMoonlight.Events;
using TMPro;
using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// TEMPORARY, homework-submission only — not part of the intended HUD. Shows how many more
    /// kills the level's current kill-objective needs, live, so a grader can see progress toward
    /// the win condition without opening the console. Remove once MRM-14 builds the real
    /// objective HUD/pause-menu list.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/UI/Objective Kill Counter HUD (temp)")]
    public sealed class ObjectiveKillCounterHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private void Awake()
        {
            if (label == null) label = GetComponent<TMP_Text>();
        }

        private void Update()
        {
            if (label == null) return;

            EventDirector director = EventDirector.Active;
            Objective current = director != null ? director.Objectives.Current : null;

            if (current == null || !current.IsKillObjective)
            {
                label.text = string.Empty;
                return;
            }

            int remaining = Mathf.Max(0, current.KillTarget - current.Kills);
            label.text = $"Old Timers remaining: {remaining}";
        }
    }
}
