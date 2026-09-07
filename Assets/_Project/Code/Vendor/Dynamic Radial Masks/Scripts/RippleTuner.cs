using UnityEngine;
using AmazingAssets.DynamicRadialMasks;

// Live-tunable sliders for the rain ripple effect (MRM-18). Ported from the Playground
// project's RippleTuner, retargeted from DRMOnMouseRaycast to DRMOnParticleCollision to match
// how the rain ripple is actually triggered here (collision with the water, not a mouse click).
// Drag any slider in the Inspector, in Edit or Play mode, and the ripple updates immediately.
// Defaults below are the values already tuned and shipped (duration=0.45, maxRadius=2.25,
// startIntensity=2.0, ringFrequency=18, lineThinness=6, waveTravel=15).
[ExecuteAlways]
public class RippleTuner : MonoBehaviour
{
    [Tooltip("The object whose DRMLiveObject template this tunes.")]
    [SerializeField] private DRMOnParticleCollision target;

    [Header("Timing")]
    [Tooltip("How long one ripple lasts, in seconds. Lower = faster.")]
    [Range(0.02f, 2f)] public float duration = 0.45f;

    [Header("Shape")]
    [Tooltip("How far the ripple expands outward before fading out.")]
    [Range(0.2f, 6f)] public float maxRadius = 2.25f;

    [Tooltip("How bright the ripple is at the moment of impact. Fades to 0 over its lifetime.")]
    [Range(0f, 6f)] public float startIntensity = 2f;

    [Tooltip("How close together the ripple's rings are. Higher = tighter, more rings.")]
    [Range(1f, 40f)] public float ringFrequency = 18f;

    [Tooltip("How thin the ripple's rings are. Higher = thinner, crisper lines.")]
    [Range(0.5f, 12f)] public float lineThinness = 6f;

    [Tooltip("How far the wave visibly travels outward as it expands. 0 = static rings.")]
    [Range(0f, 40f)] public float waveTravel = 15f;

    private void OnValidate() => Apply();
    private void Awake() => Apply();

    private void Apply()
    {
        if (target == null) return;

        DRMLiveObject obj = target.DRMLiveObject;

        obj.lifeLength = new Vector2(duration, duration);

        obj.radius.evolutionType = DRMLiveProperty.Enum.AnimationType.LerpRange;
        obj.radius.startValue = new Vector2(0.1f, 0.1f);
        obj.radius.endValue = new Vector2(maxRadius, maxRadius);

        obj.intensity.evolutionType = DRMLiveProperty.Enum.AnimationType.LerpRange;
        obj.intensity.startValue = new Vector2(startIntensity, startIntensity);
        obj.intensity.endValue = new Vector2(0f, 0f);

        obj.frequency.evolutionType = DRMLiveProperty.Enum.AnimationType.Constant;
        obj.frequency.startValue = new Vector2(ringFrequency, ringFrequency);

        obj.smooth.evolutionType = DRMLiveProperty.Enum.AnimationType.Constant;
        obj.smooth.startValue = new Vector2(lineThinness, lineThinness);

        obj.phaseSpeed.evolutionType = DRMLiveProperty.Enum.AnimationType.LerpRange;
        obj.phaseSpeed.startValue = new Vector2(0f, 0f);
        obj.phaseSpeed.endValue = new Vector2(waveTravel, waveTravel);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(target);
#endif
    }
}
