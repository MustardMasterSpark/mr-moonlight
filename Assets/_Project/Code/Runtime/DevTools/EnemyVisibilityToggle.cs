using System.Collections.Generic;
using MrMoonlight.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.DevTools
{
    /// <summary>
    /// Development toggle, <b>O</b>: hides every enemy (alive or dead) and pauses enemy spawning; press again to
    /// bring the same enemies back exactly where they were, carrying on with what they were doing. Made for the
    /// Lighting Test Scene (MRM-86), where a full Legion population gets in the way of tuning the weather.
    ///
    /// <para>Hiding is <c>SetActive(false)</c>, not destroying, so nothing is lost: AI state, health and position
    /// come back with the object. The spawner (<see cref="DemoSpotterPopulationManager"/>) is disabled while
    /// hidden, so it does not top the population back up behind your back. The session log records the hide and
    /// show as enemy despawn/spawn lines.</para>
    ///
    /// Owner: MRM-86.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Dev Tools/Enemy Visibility Toggle")]
    public sealed class EnemyVisibilityToggle : MonoBehaviour
    {
        private readonly List<GameObject> _hidden = new List<GameObject>();
        private readonly List<Behaviour> _pausedSpawners = new List<Behaviour>();

        /// <summary>True while the enemies are hidden.</summary>
        public bool EnemiesHidden { get; private set; }

        /// <summary>How many enemies are hidden right now.</summary>
        public int HiddenCount => _hidden.Count;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
            {
                SetHidden(!EnemiesHidden);
            }
        }

        /// <summary>Hides (true) or brings back (false) every enemy, and pauses or resumes spawning.</summary>
        public void SetHidden(bool hidden)
        {
            if (hidden == EnemiesHidden) return;
            EnemiesHidden = hidden;

            if (hidden)
            {
                foreach (DemoSpotterPopulationManager spawner in
                         FindObjectsByType<DemoSpotterPopulationManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!spawner.enabled) continue;
                    spawner.enabled = false;
                    _pausedSpawners.Add(spawner);
                }

                foreach (EnemyIdentity enemy in FindObjectsByType<EnemyIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    enemy.gameObject.SetActive(false);
                    _hidden.Add(enemy.gameObject);
                }
            }
            else
            {
                foreach (GameObject enemy in _hidden)
                {
                    if (enemy != null) enemy.SetActive(true);
                }

                foreach (Behaviour spawner in _pausedSpawners)
                {
                    if (spawner != null) spawner.enabled = true;
                }

                _hidden.Clear();
                _pausedSpawners.Clear();
            }

            Debug.Log("[MRM-86] Enemies " + (hidden ? "hidden (" + _hidden.Count + ")" : "back"), this);
        }
    }
}
