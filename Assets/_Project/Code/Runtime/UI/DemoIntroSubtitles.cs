using System.Collections;
using DG.Tweening;
using MrMoonlight.Data;
using TMPro;
using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// ELVTR-DEMO-ONLY. Plays the opening subtitle crawl inside the bottom letterbox bar — Carlos's
    /// ask (2026-09-10): a second of black bars, then the lines from <see cref="introLines"/> one
    /// at a time (fade in, hold long enough to read, fade out, a short gap), then
    /// <see cref="LetterboxController.Retract"/> pulls both bars away. Source text lives at
    /// Assets/_Project/Data/Demo/DemoIntroLines.txt — see that folder's README for why it's marked
    /// demo-only rather than a permanent design doc.
    ///
    /// <para>Per-line hold time is computed from word count at
    /// <see cref="MoonlightTunables.DemoIntroWordsPerMinute"/>, clamped to
    /// [<see cref="MoonlightTunables.DemoIntroLineMinHold"/>,
    /// <see cref="MoonlightTunables.DemoIntroLineMaxHold"/>] — a five-word line and a thirty-word
    /// line both need to be readable, not the same fixed duration.</para>
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/UI/Demo Intro Subtitles (ELVTR demo only)")]
    public sealed class DemoIntroSubtitles : MonoBehaviour
    {
        [Tooltip("One line per subtitle. Assets/_Project/Data/Demo/DemoIntroLines.txt — blank lines are skipped.")]
        [SerializeField] private TextAsset introLines;

        [Tooltip("The bars this sequence runs inside. Shown instantly on Start, retracted once the last line fades out.")]
        [SerializeField] private LetterboxController letterbox;

        [Tooltip("Text shown one line at a time. Uses the Gabriel font (GabrieleBandAah SDF), white, per Carlos's ask.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Group the label fades on. Found on this GameObject if left empty.")]
        [SerializeField] private CanvasGroup group;

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            group.alpha = 0f;

            if (label != null)
            {
                label.fontSize = Tunables.I.DemoIntroFontSize;
                label.color = Tunables.I.DemoIntroTextColor;
                label.text = string.Empty;
            }
        }

        private void Start()
        {
            StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            if (introLines == null || letterbox == null || label == null)
            {
                Debug.LogError($"[{nameof(DemoIntroSubtitles)}] Missing a reference — intro sequence skipped.", this);
                yield break;
            }

            MoonlightTunables t = Tunables.I;

            letterbox.ShowInstant();
            yield return new WaitForSeconds(t.DemoIntroFirstLineDelay);

            string[] lines = introLines.text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim('\r', '\n', ' ');
                if (string.IsNullOrWhiteSpace(line)) continue;

                yield return ShowLine(line, t);
            }

            yield return letterbox.Retract();
        }

        private IEnumerator ShowLine(string line, MoonlightTunables t)
        {
            label.text = line;

            int wordCount = Mathf.Max(1, line.Split(' ').Length);
            float hold = Mathf.Clamp(wordCount / t.DemoIntroWordsPerMinute * 60f, t.DemoIntroLineMinHold, t.DemoIntroLineMaxHold);

            yield return Fade(1f, t.DemoIntroLineFadeDuration);
            yield return new WaitForSeconds(hold);
            yield return Fade(0f, t.DemoIntroLineFadeDuration);
            yield return new WaitForSeconds(t.DemoIntroLineGap);
        }

        private IEnumerator Fade(float target, float duration)
        {
            bool done = false;
            DOTween.To(() => group.alpha, a => group.alpha = a, target, duration)
                .SetLink(gameObject)
                .OnComplete(() => done = true);

            while (!done) yield return null;
        }
    }
}
