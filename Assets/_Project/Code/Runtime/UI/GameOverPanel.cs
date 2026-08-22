using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// Minimal, unstyled landing point for MRM-17's death sequence - "lands on the game over
    /// screen every time, with no way to regain control mid-sequence" per its acceptance
    /// criteria. MRM-19 (Backlog) owns the real game over screen, with Restart / Return-to-menu
    /// buttons; this is a functional stub so MRM-17 has somewhere to land in the meantime,
    /// matching MRM-18/19's own "unstyled first" pattern. When MRM-19 starts, extend this
    /// component (or its GameObject) rather than building a second, parallel game-over canvas -
    /// see the note left on that issue. Owner: MRM-17
    /// </summary>
    public sealed class GameOverPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            root.SetActive(false);
        }

        /// <summary>Reveals the panel. No buttons yet - MRM-19 adds Restart / Return to menu. Owner: MRM-17</summary>
        public void Show()
        {
            root.SetActive(true);
        }
    }
}
