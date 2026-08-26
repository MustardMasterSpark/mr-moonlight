using System;
using System.Collections;
using MrMoonlight.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace MrMoonlight.UI
{
    /// <summary>
    /// The Credits panel (MRM-18): fades in over its own opaque (black) background, scrolls a
    /// long placeholder TextMeshPro roll upward, and fades back out either when the roll clears
    /// the top or on any click/key/gamepad button. <see cref="panelGroup"/>.blocksRaycasts is set
    /// the instant <see cref="Show"/> is called - even mid-fade-in - which is what satisfies the
    /// issue's "while credits are showing they block raycasts" criterion: the panel is opaque and
    /// covers the menu buttons, so no separate raycast-blocker layer is needed. Owner: MRM-18
    /// </summary>
    public sealed class CreditsController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private RectTransform creditsContent;
        [SerializeField] private RectTransform viewport;

        /// <summary>Raised once the panel has fully faded back out. MainMenuController uses this to reveal the main buttons again.</summary>
        public event Action OnClosed;

        private Coroutine _routine;
        private IDisposable _anyButtonListener;
        private bool _skipRequested;

        private void Awake()
        {
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;
        }

        private void OnDisable()
        {
            _anyButtonListener?.Dispose();
            _anyButtonListener = null;
        }

        public void Show()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(RunCredits());
        }

        private IEnumerator RunCredits()
        {
            panelGroup.blocksRaycasts = true;

            yield return Fade(panelGroup.alpha, 1f, Tunables.I.MenuTransitionFadeDuration);

            ResetScroll();

            // Any click, key or gamepad button skips - onAnyButtonPress fires for a ButtonControl
            // on any connected device (mouse clicks included), same idiom InputDebugOverlay
            // already uses for "any button" detection. Owner: MRM-18
            _skipRequested = false;
            _anyButtonListener = InputSystem.onAnyButtonPress.Call(_ => _skipRequested = true);

            float endY = creditsContent.rect.height + viewport.rect.height;
            while (creditsContent.anchoredPosition.y < endY && !_skipRequested)
            {
                creditsContent.anchoredPosition += Vector2.up * (Tunables.I.CreditsScrollSpeed * Time.deltaTime);
                yield return null;
            }

            _anyButtonListener?.Dispose();
            _anyButtonListener = null;

            yield return Fade(panelGroup.alpha, 0f, Tunables.I.MenuTransitionFadeDuration);

            panelGroup.blocksRaycasts = false;
            _routine = null;
            OnClosed?.Invoke();
        }

        private void ResetScroll()
        {
            creditsContent.anchoredPosition = new Vector2(creditsContent.anchoredPosition.x, 0f);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                panelGroup.alpha = Mathf.Lerp(from, to, duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            panelGroup.alpha = to;
        }
    }
}
