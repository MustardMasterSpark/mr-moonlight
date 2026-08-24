using System.Collections;
using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// Owns the Directional Light standing in for the sun. Applies a <see cref="SunState"/>
    /// instantly — <see cref="TimeManager"/> is responsible for any smooth transition between
    /// states; this component is a pure mechanism with no preset knowledge of its own.
    /// Attach to the scene's single Directional Light, never a second one — see
    /// Docs/webgl-constraints.md §5 on real-time lighting cost.
    ///
    /// Also owns the cabin's fast indoor dim (<see cref="SetIndoorDim"/>) — a separate,
    /// shorter transition from the story-beat dimming <see cref="TimeManager"/> drives, per
    /// MRM-47. <see cref="dimmedForIndoors"/> is an inspector placeholder for the real trigger
    /// volume the mine/cabin staging issues will add later.
    /// Owner: MRM-47
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class SunController : MonoBehaviour
    {
        [Header("Indoor Dim Override — placeholder trigger, MRM-47")]

        [Tooltip("Placeholder for the cabin's fast interior dim, until a real trigger volume exists (staging work). Toggle in Play Mode to dim the sun toward near-zero intensity, or restore it.")]
        [SerializeField] private bool dimmedForIndoors;

        [SerializeField] private bool overrideDimTransitionSeconds;
        [SerializeField] private float dimTransitionSecondsOverride;

        private Light _light;
        private bool _lastDimmedForIndoors;
        private float _preDimIntensity = -1f;
        private Coroutine _dimCoroutine;

        /// <summary>Seconds the indoor dim/restore transition takes — the default is shared via MoonlightTunables; override per-instance if this Sun ever needs a different feel. Owner: MRM-47</summary>
        private float DimTransitionSeconds =>
            overrideDimTransitionSeconds ? dimTransitionSecondsOverride : Tunables.I.SunIndoorDimTransitionSeconds;

        private void Awake() => _light = GetComponent<Light>();

        private void Update()
        {
            if (dimmedForIndoors == _lastDimmedForIndoors) return;
            _lastDimmedForIndoors = dimmedForIndoors;
            SetIndoorDim(dimmedForIndoors);
        }

        /// <summary>Applies every field of the given state immediately, with no transition.</summary>
        public void ApplyState(SunState state)
        {
            if (_light == null) _light = GetComponent<Light>();

            transform.rotation = Quaternion.Euler(state.Elevation, state.Azimuth, 0f);
            _light.useColorTemperature = state.UseColorTemperature;
            if (state.UseColorTemperature)
                _light.colorTemperature = state.ColorTemperature;
            else
                _light.color = state.Color;
            _light.intensity = state.Intensity;
        }

        /// <summary>Reads the light's current values back into a state, so a caller (TimeManager) can lerp FROM the live state without tracking it separately.</summary>
        public SunState GetState()
        {
            if (_light == null) _light = GetComponent<Light>();
            Vector3 euler = transform.eulerAngles;
            return new SunState
            {
                Elevation = euler.x,
                Azimuth = euler.y,
                Color = _light.color,
                Intensity = _light.intensity,
                UseColorTemperature = _light.useColorTemperature,
                ColorTemperature = _light.colorTemperature
            };
        }

        /// <summary>Smoothly dims toward near-zero intensity (true) or restores the pre-dim intensity (false), over <see cref="DimTransitionSeconds"/>. The cabin's "step indoors" beat — separate from and faster than TimeManager's per-story-beat dimming. Owner: MRM-47</summary>
        public void SetIndoorDim(bool dimOut)
        {
            if (_light == null) _light = GetComponent<Light>();
            if (dimOut && _preDimIntensity < 0f) _preDimIntensity = _light.intensity;

            float target = dimOut ? 0f : (_preDimIntensity < 0f ? _light.intensity : _preDimIntensity);
            if (_dimCoroutine != null) StopCoroutine(_dimCoroutine);
            _dimCoroutine = gameObject.activeInHierarchy ? StartCoroutine(LerpIntensity(target, DimTransitionSeconds)) : null;
            if (_dimCoroutine == null) _light.intensity = target;
        }

        private IEnumerator LerpIntensity(float target, float duration)
        {
            float start = _light.intensity;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _light.intensity = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            _light.intensity = target;
            _dimCoroutine = null;
        }
    }
}
