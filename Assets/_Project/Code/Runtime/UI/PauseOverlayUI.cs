using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// The dimmed backdrop and controls-reminder image shown while <see cref="MrMoonlight.Player.PauseController"/>
    /// is paused. Owner: MRM-19 follow-up (demo pause polish) — <c>PauseController</c> shipped with no UI panel
    /// by design, this is that fast follow's overlay half.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/UI/Pause Overlay")]
    public sealed class PauseOverlayUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        // Lazy, not Awake-resolved: this component's GameObject starts disabled in the scene
        // (the overlay is hidden until PauseController calls Show), and Awake never runs on a
        // GameObject that is already inactive when the scene loads.
        private GameObject Root => root != null ? root : (root = gameObject);

        public void Show() => Root.SetActive(true);

        public void Hide() => Root.SetActive(false);
    }
}
