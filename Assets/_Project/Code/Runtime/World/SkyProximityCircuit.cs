// Keeps the Draw Debug Tools calls in THIS file in every build, release included. DDT compiles its draw
// calls out of release players unless DDT_ENABLED is defined at the call site; defining it here instead of
// in Player Settings keeps every other DDT call in the project stripped as the package intends.
#define DDT_ENABLED

using DDT;
using MrMoonlight.Data;
using MrMoonlight.World.Weather;
using UnityEngine;

namespace MrMoonlight.World
{
    /// <summary>
    /// The Lighting Test Scene's weather circuit (MRM-86, Carlos's sketch 2026-09-29). One marker (a cylinder
    /// with no collider) stands on one corner of a square at a time, wired to one weather profile from the
    /// <see cref="WeatherSystem"/>'s library, in library order. Around it, two debug spheres drawn with Draw
    /// Debug Tools (AST-271):
    /// <list type="bullet">
    ///   <item><b>Blender radius</b> (outer, pink): walking inside it starts blending the sky, light and fog
    ///   toward the marker's weather; the closer to the marker, the stronger the blend.</item>
    ///   <item><b>Complete blend</b> (inner, green): reaching it locks that weather in at 100%, then the marker
    ///   jumps to the next corner, wired to the next weather.</item>
    /// </list>
    /// Corners are visited in <see cref="corners"/> order (clockwise), starting at <see cref="firstCorner"/>;
    /// the weather list loops forever.
    ///
    /// <para>Distance is flat (height ignored), so terrain slopes, jumping and crouching do not change the
    /// blend. Backing away before the lock lowers the blend again; after a lock the weather stays.</para>
    ///
    /// <para>Scene-agnostic: drop it on any object, give it corner transforms, a marker and a WeatherSystem. The
    /// player is found by the "Player" tag if not assigned.</para>
    ///
    /// Owner: MRM-86.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/World/Sky Proximity Circuit")]
    public sealed class SkyProximityCircuit : MonoBehaviour
    {
        private const string PlayerTag = "Player";

        [Tooltip("Applies and blends the weather profiles.")]
        [SerializeField] private WeatherSystem weather;

        [Tooltip("The marker (cylinder, no collider) moved from corner to corner.")]
        [SerializeField] private Transform marker;

        [Tooltip("Corner points in visiting order (clockwise). Corner 0 is where the player spawns.")]
        [SerializeField] private Transform[] corners;

        [Tooltip("Index in Corners of the first marker's corner.")]
        [SerializeField, Min(0)] private int firstCorner = 1;

        [Tooltip("What walks to the markers. Found by the 'Player' tag when empty.")]
        [SerializeField] private Transform player;

        [Header("Live tuning (Play Mode)")]
        [Tooltip("Tick this in Play Mode, then edit the values below. Un-ticked, they follow MoonlightTunables. "
                 + "The values are copied FROM the tunables every time the scene starts, and Play Mode edits are "
                 + "discarded on stop - tell Claude the values you like.")]
        [SerializeField] private bool liveTuning;

        [Tooltip("Outer 'Blender radius', metres: the blend starts here. Tunable: SkyBlendStartRadius.")]
        [SerializeField, Min(0.1f)] private float startRadius;

        [Tooltip("Inner 'Complete blend' radius, metres: the weather locks in here. Tunable: SkyBlendCompleteRadius.")]
        [SerializeField, Min(0f)] private float completeRadius;

        [Tooltip("Height of the spheres' centre above the marker's base. Tunable: SkyBlendSphereHeight.")]
        [SerializeField] private float sphereHeight;

        [Tooltip("Line segments per sphere ring. Tunable: SkyBlendSphereSegments.")]
        [SerializeField, Range(6, 64)] private int sphereSegments = 24;

        [Tooltip("Outer sphere colour. Tunable: SkyBlendStartColor.")]
        [SerializeField] private Color startColor = Color.magenta;

        [Tooltip("Inner sphere colour. Tunable: SkyBlendCompleteColor.")]
        [SerializeField] private Color completeColor = Color.green;

        [Header("Read-only, for tuning")]
        [SerializeField] private int nextWeather;
        [SerializeField] private int currentCorner;
        [SerializeField] private float flatDistance;
        [SerializeField] private int weathersLocked;

        private bool _armed;

        private void Start()
        {
            LoadFromTunables();

            if (weather == null || weather.Library == null || weather.Library.Count == 0
                || marker == null || corners == null || corners.Length == 0)
            {
                Debug.LogError("[MRM-86] SkyProximityCircuit: needs a WeatherSystem with profiles, a marker and corners.", this);
                enabled = false;
                return;
            }

            if (player == null)
            {
                // Several of the player's children carry the Player tag too (e.g. Body/Hitbox), so take the root.
                GameObject found = GameObject.FindGameObjectWithTag(PlayerTag);
                player = found != null ? found.transform.root : null;
            }

            if (player == null)
            {
                Debug.LogError("[MRM-86] SkyProximityCircuit: no player assigned and none tagged 'Player'.", this);
                enabled = false;
                return;
            }

            marker.gameObject.SetActive(false);
            nextWeather = 0;
            currentCorner = Mathf.Clamp(firstCorner, 0, corners.Length - 1);
        }

        private void Update()
        {
            if (!weather.IsReady) return;

            if (!_armed)
            {
                ArmMarker();
                _armed = true;
            }

            if (!liveTuning)
            {
                LoadFromTunables();
            }

            float inner = Mathf.Min(completeRadius, startRadius);
            Vector3 offset = player.position - marker.position;
            offset.y = 0f;
            flatDistance = offset.magnitude;

            // 0 at the outer sphere (and beyond), 1 at the inner sphere (and inside).
            weather.SetBlend(Mathf.InverseLerp(startRadius, inner, flatDistance));

            if (flatDistance <= inner)
            {
                LockAndAdvance();
            }

            DrawSpheres(inner);
        }

        /// <summary>Puts the marker on the current corner and wires it to the next weather.</summary>
        private void ArmMarker()
        {
            marker.position = corners[currentCorner].position;
            marker.gameObject.SetActive(true);
            weather.BeginTransition(nextWeather);
        }

        /// <summary>Locks the weather in fully, then moves on to the next corner and the next weather.</summary>
        private void LockAndAdvance()
        {
            weather.Lock();
            weathersLocked++;
            Debug.Log("[MRM-86] Weather locked: " + weather.CurrentName + " (" + (nextWeather + 1) + "/"
                      + weather.Library.Count + ") at corner " + currentCorner, this);

            nextWeather = (nextWeather + 1) % weather.Library.Count;
            currentCorner = (currentCorner + 1) % corners.Length;
            ArmMarker();
        }

        private void DrawSpheres(float inner)
        {
            Vector3 centre = marker.position + Vector3.up * sphereHeight;
            DrawDebugTools.DrawSphere(centre, startRadius, sphereSegments, startColor);
            DrawDebugTools.DrawSphere(centre, inner, sphereSegments, completeColor);
        }

        /// <summary>Re-reads the tunables, e.g. after editing the asset during Play Mode.</summary>
        [ContextMenu("Reload values from MoonlightTunables")]
        private void LoadFromTunables()
        {
            MoonlightTunables t = Tunables.I;
            startRadius = t.SkyBlendStartRadius;
            completeRadius = t.SkyBlendCompleteRadius;
            sphereHeight = t.SkyBlendSphereHeight;
            sphereSegments = t.SkyBlendSphereSegments;
            startColor = t.SkyBlendStartColor;
            completeColor = t.SkyBlendCompleteColor;
        }
    }
}
