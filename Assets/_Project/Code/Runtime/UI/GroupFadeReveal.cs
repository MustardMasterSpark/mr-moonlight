using System.Collections;
using DG.Tweening;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MrMoonlight.UI
{
    /// <summary>
    /// Fades this GameObject's CanvasGroup from its current alpha up to 1, using a duration and
    /// easing curve editable directly on this component - Carlos's ask (2026-09-08): a tool he
    /// can tune himself rather than a hardcoded lerp. Sits on Canvas/MainButtons/ButtonGroup
    /// specifically - Carlos's follow-up correction the same day: this should only apply to the
    /// button row, not the whole MainButtons block (title's own reveal, driven separately by
    /// <see cref="TitleLetterReveal"/>, is unaffected either way). This is the same CanvasGroup
    /// <see cref="MainMenuController"/> fades in on reveal; MainMenuController calls
    /// <see cref="FadeIn"/> instead of its own hand-rolled linear lerp.
    ///
    /// <see cref="fadeCurve"/> is a real DOTween custom ease (<c>Tween.SetEase(AnimationCurve)</c>,
    /// confirmed present in this project's DOTween build) - drag its keys/tangents in the
    /// Inspector to change how the fade accelerates/settles, independent of <see cref="duration"/>
    /// which controls how long it takes overall. The curve's X axis is normalized time (0-1), Y
    /// axis is normalized alpha (0-1) - a straight diagonal is linear, an S-curve eases both ends,
    /// overshooting above 1 briefly is valid (DOTween allows it) if that's ever wanted.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GroupFadeReveal : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("How long the fade-in takes, in seconds. Carlos: the previous fixed fade read as too fast - lower/raise this to taste.")]
        [SerializeField] private float duration = 1.5f;

        [Tooltip("Custom easing curve for the fade, evaluated over the duration above (X = normalized time 0-1, Y = normalized alpha 0-1). Edit the keys/tangents directly to change the speed and smoothness of the reveal.")]
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>Starts the fade to alpha 1 and returns a Coroutine a caller can yield on, matching this file's other fade coroutines (FadeOverlay, etc).</summary>
        public Coroutine FadeIn()
        {
            return StartCoroutine(FadeInRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            DOTween.Kill(this);

            bool done = false;
            DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, 1f, duration)
                .SetEase(fadeCurve)
                .SetId(this)
                .OnComplete(() => done = true);

            while (!done)
            {
                yield return null;
            }
        }

#if UNITY_EDITOR
        private double _previewStartTime;

        /// <summary>
        /// Edit-Mode preview - Carlos's ask (2026-09-08): watch the curve/duration above play
        /// without entering Play Mode or sitting through the whole menu intro. DOTween and
        /// coroutines don't tick outside Play Mode, so this drives canvasGroup.alpha directly off
        /// EditorApplication.update and fadeCurve.Evaluate() instead - same curve, same duration,
        /// same math as the real DOTween tween in <see cref="FadeInRoutine"/>, just ticked by the
        /// editor loop rather than DOTween's player-loop hook. In Play Mode this just calls the
        /// real <see cref="FadeIn"/> so the button always shows the actual behavior.
        ///
        /// Trigger it via the "Preview Fade In" button in the Inspector (see
        /// <see cref="GroupFadeRevealEditor"/> below), or the same-named entry in the component's
        /// right-click / kebab-menu context menu (the ContextMenu attribute below) - both call this.
        /// </summary>
        [ContextMenu("Preview Fade In")]
        public void PreviewFadeInEditor()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            if (Application.isPlaying)
            {
                FadeIn();
                return;
            }

            EditorApplication.update -= EditorPreviewTick;
            _previewStartTime = EditorApplication.timeSinceStartup;
            canvasGroup.alpha = 0f;
            EditorApplication.update += EditorPreviewTick;
        }

        private void EditorPreviewTick()
        {
            if (canvasGroup == null)
            {
                EditorApplication.update -= EditorPreviewTick;
                return;
            }

            float elapsed = (float)(EditorApplication.timeSinceStartup - _previewStartTime);
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = fadeCurve.Evaluate(t);

            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            if (t >= 1f)
            {
                EditorApplication.update -= EditorPreviewTick;
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorPreviewTick;
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>Adds a real "Preview Fade In" button below the default fields, so Carlos doesn't need to know the right-click context menu exists.</summary>
    [CustomEditor(typeof(GroupFadeReveal))]
    public sealed class GroupFadeRevealEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8);
            if (GUILayout.Button("Preview Fade In", GUILayout.Height(28)))
            {
                ((GroupFadeReveal)target).PreviewFadeInEditor();
            }
        }
    }
#endif
}
