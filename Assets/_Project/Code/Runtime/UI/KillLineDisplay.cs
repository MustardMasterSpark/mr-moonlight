using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using MrMoonlight.Data;
using MrMoonlight.Enemies;
using TMPro;
using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// ELVTR-DEMO-ONLY. Flashes one edgy one-liner in red, shaking (Text Animator's &lt;shake&gt;
    /// tag), briefly after every enemy kill (<see cref="EnemyHealth.AnyDied"/>) — Carlos's ask
    /// (2026-09-10). Lines are drawn from a shuffled bag
    /// (Assets/_Project/Data/Demo/DemoKillLines.txt, see that folder's README): the whole file is
    /// shuffled once, lines are handed out from the back, and the bag is reshuffled from the full
    /// list the instant it runs dry — nothing repeats until every other line has shown at least
    /// once.
    ///
    /// <para>Sits a bit higher while <see cref="LetterboxController.IsActive"/> is still true (nested
    /// inside the bottom bar, above where the intro subtitles sit) and drops to its normal
    /// near-bottom spot once the bars have retracted — see
    /// <see cref="MoonlightTunables.KillLineAnchoredYWithLetterbox"/> vs
    /// <see cref="MoonlightTunables.KillLineAnchoredYWithoutLetterbox"/>.</para>
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/UI/Kill Line Display (ELVTR demo only)")]
    public sealed class KillLineDisplay : MonoBehaviour
    {
        [Tooltip("Pool source. Assets/_Project/Data/Demo/DemoKillLines.txt — blank lines are skipped.")]
        [SerializeField] private TextAsset killLines;

        [Tooltip("Text Animator component on the same label — drives the <shake> effect. Requires a TMP_Text on the same GameObject.")]
        [SerializeField] private TextAnimator_TMP animator;

        [Tooltip("The label itself. Uses the Gabriel font (GabrieleBandAah SDF), red, per Carlos's ask.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Group the line fades on. Found on this GameObject if left empty.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("Moved between the two Y positions in MoonlightTunables depending on whether the intro letterbox is still up. Found on this GameObject if left empty.")]
        [SerializeField] private RectTransform rect;

        private readonly List<string> _bag = new List<string>();
        private string[] _allLines = System.Array.Empty<string>();
        private Coroutine _routine;

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (rect == null) rect = GetComponent<RectTransform>();
            group.alpha = 0f;

            if (label != null)
            {
                label.fontSize = Tunables.I.KillLineFontSize;
                label.color = Tunables.I.KillLineColor;
                label.text = string.Empty;
            }

            if (killLines != null)
            {
                List<string> lines = new List<string>();
                string[] rawLines = killLines.text.Split('\n');
                for (int i = 0; i < rawLines.Length; i++)
                {
                    string line = rawLines[i].Trim('\r', '\n', ' ');
                    if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
                }
                _allLines = lines.ToArray();
            }
        }

        private void OnEnable() => EnemyHealth.AnyDied += HandleEnemyDied;

        private void OnDisable() => EnemyHealth.AnyDied -= HandleEnemyDied;

        private void HandleEnemyDied(EnemyHealth enemy)
        {
            if (label == null || _allLines.Length == 0) return;

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(NextLine()));
        }

        private string NextLine()
        {
            if (_bag.Count == 0)
            {
                _bag.AddRange(_allLines);

                for (int i = _bag.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    string swap = _bag[i];
                    _bag[i] = _bag[j];
                    _bag[j] = swap;
                }
            }

            string next = _bag[_bag.Count - 1];
            _bag.RemoveAt(_bag.Count - 1);
            return next;
        }

        private IEnumerator ShowRoutine(string line)
        {
            MoonlightTunables t = Tunables.I;

            bool nestedInLetterbox = LetterboxController.Active != null && LetterboxController.Active.IsActive;
            SetAnchoredY(nestedInLetterbox ? t.KillLineAnchoredYWithLetterbox : t.KillLineAnchoredYWithoutLetterbox);

            string tagged = "<shake>" + line + "</shake>";
            if (animator != null) animator.SetText(tagged);
            else label.text = tagged;

            yield return Fade(1f, t.KillLineFadeDuration);
            yield return new WaitForSeconds(t.KillLineHoldDuration);
            yield return Fade(0f, t.KillLineFadeDuration);

            _routine = null;
        }

        private void SetAnchoredY(float y)
        {
            if (rect == null) return;
            Vector2 pos = rect.anchoredPosition;
            pos.y = y;
            rect.anchoredPosition = pos;
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
