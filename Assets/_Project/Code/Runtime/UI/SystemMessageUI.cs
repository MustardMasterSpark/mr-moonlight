using System.Collections;
using DG.Tweening;
using MrMoonlight.Data;
using TMPro;
using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// The centre-bottom text channel — objectives, system messages, and eventually dialogue
    /// subtitles — laid out like Silent Hill 1's subtitles. Owner: MRM-11, seeds MRM-14.
    ///
    /// <para>One message at a time. A second one replaces the first rather than queueing, because
    /// queueing means a message can appear seconds after the moment it was about, which reads as a
    /// bug. If two messages genuinely need to be read in sequence, the script says so with a
    /// <c>wait</c> between them.</para>
    ///
    /// <para>Fades run on unscaled time so a message that is still on screen when the game pauses
    /// finishes its fade instead of freezing mid-way.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/UI/System Message UI")]
    public sealed class SystemMessageUI : MonoBehaviour
    {
        [Tooltip("The text itself. Uses the project font (Special Elite).")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Group the message fades on. Found on this GameObject if left empty.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("Push tunables into the label's size and colour on Awake. Untick to hand-style this one label in the inspector.")]
        [SerializeField] private bool applyTunableStyle = true;

        private Coroutine _routine;
        private Tween _fade;

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();

            if (label == null)
            {
                Debug.LogError($"[{nameof(SystemMessageUI)}] '{name}' has no label assigned — nothing will ever be readable.", this);
                return;
            }

            if (applyTunableStyle)
            {
                label.fontSize = Tunables.I.SystemMessageFontSize;
                label.color = Tunables.I.SystemMessageColor;
            }

            label.text = string.Empty;
            if (group != null) group.alpha = 0f;
        }

        /// <summary>Shows a message for <paramref name="duration"/> seconds, then fades it out.</summary>
        public void Show(string text, float duration, Color? color = null)
        {
            if (label == null) return;

            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning($"[{nameof(SystemMessageUI)}] Asked to show an empty message; ignored.", this);
                return;
            }

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(text, duration, color));
        }

        /// <summary>Clears whatever is on screen right now, without waiting for its duration.</summary>
        public void Clear()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            FadeTo(0f);
        }

        private IEnumerator ShowRoutine(string text, float duration, Color? color)
        {
            label.text = text;
            if (color.HasValue) label.color = color.Value;
            else if (applyTunableStyle) label.color = Tunables.I.SystemMessageColor;

            FadeTo(1f);
            yield return new WaitForSecondsRealtime(Tunables.I.SystemMessageFadeDuration + Mathf.Max(0f, duration));

            FadeTo(0f);
            _routine = null;
        }

        private void FadeTo(float target)
        {
            if (group == null) return;

            _fade?.Kill();
            _fade = DOTween.To(() => group.alpha, a => group.alpha = a, target, Tunables.I.SystemMessageFadeDuration)
                .SetUpdate(isIndependentUpdate: true)
                .SetLink(gameObject);
        }
    }
}
