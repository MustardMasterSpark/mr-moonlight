using MrMoonlight.Data;
using MrMoonlight.Player;
using MrMoonlight.UI;
using UnityEngine;

namespace MrMoonlight.Interaction
{
    /// <summary>
    /// Finds the single <see cref="Interactable"/> Tracey is closest to looking at - within
    /// <see cref="MoonlightTunables.InteractionNearbyDistance"/> and inside
    /// <see cref="MoonlightTunables.InteractionAngleTolerance"/> of screen centre, the smallest
    /// angle winning when more than one qualifies (the "two interactables close together resolve to
    /// the one actually being looked at" AC). Drives one shared 0-1 fade value for both
    /// <see cref="InteractionPromptUI"/> and the current target's highlight - "never a dry pop" per
    /// the issue - and triggers it on X (<see cref="InputSystem_Actions.Gameplay"/>'s Interact
    /// action). Reads <see cref="MoonlightPlayerRig.Input"/> rather than owning its own
    /// <see cref="MrMoonlight.Input.InputMapController"/>, so it doesn't bind a second, redundant
    /// instance to the same devices. Owner: MRM-16
    /// </summary>
    // MRM-9: no [RequireComponent(typeof(MoonlightPlayerRig))]. The rig lives on the player
    // ROOT, next to PolymindGames' character, while this component sits further down the
    // hierarchy - RequireComponent would force a second, non-functional rig onto whatever
    // GameObject this is on, which is exactly the duplication the swap was meant to remove.
    public sealed class InteractionDetector : MonoBehaviour
    {
        private const int MaxCandidates = 16;

        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InteractionPromptUI promptUI;

        private MoonlightPlayerRig _playerController;
        private readonly Collider[] _candidateBuffer = new Collider[MaxCandidates];

        private Interactable _currentTarget;
        private float _visibility;

        private void Awake()
        {
            _playerController = GetComponentInParent<MoonlightPlayerRig>();

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (playerCamera == null)
            {
                Debug.LogError($"[Interaction] {name} has no camera to aim from. See MRM-16.", this);
            }
        }

        private void Update()
        {
            Interactable best = FindBestCandidate();
            UpdateTarget(best);
            UpdateVisibility();

            if (_currentTarget != null && _playerController.Input.Actions.Gameplay.Interact.WasPerformedThisFrame())
            {
                _currentTarget.Interact(gameObject);
            }
        }

        private Interactable FindBestCandidate()
        {
            if (playerCamera == null)
            {
                return null;
            }

            Vector3 origin = playerCamera.transform.position;
            Vector3 forward = playerCamera.transform.forward;

            int count = Physics.OverlapSphereNonAlloc(origin, Tunables.I.InteractionNearbyDistance, _candidateBuffer, Tunables.I.InteractionLayerMask, QueryTriggerInteraction.Collide);

            Interactable best = null;
            float bestAngle = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider candidateCollider = _candidateBuffer[i];
                if (!candidateCollider.TryGetComponent(out Interactable candidate))
                {
                    candidate = candidateCollider.GetComponentInParent<Interactable>();
                }

                if (candidate == null || !candidate.IsInteractable)
                {
                    continue;
                }

                Vector3 toTarget = candidate.AimPoint.position - origin;
                float angle = Vector3.Angle(forward, toTarget);
                if (angle > Tunables.I.InteractionAngleTolerance)
                {
                    continue;
                }

                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = candidate;
                }
            }

            return best;
        }

        private void UpdateTarget(Interactable best)
        {
            if (best == _currentTarget)
            {
                return;
            }

            // Switching directly between two candidates without an intermediate null keeps
            // whatever fade level is already live instead of dropping to zero and re-fading in -
            // the disambiguation AC is about which object wins, not a fresh pop every swap.
            _currentTarget?.SetHighlight(0f);
            _currentTarget = best;
            promptUI?.SetContent(_currentTarget);
        }

        private void UpdateVisibility()
        {
            float target = _currentTarget != null ? 1f : 0f;
            float rate = target > _visibility
                ? 1f / Mathf.Max(Tunables.I.InteractionPromptFadeInDuration, 0.0001f)
                : 1f / Mathf.Max(Tunables.I.InteractionPromptFadeOutDuration, 0.0001f);

            _visibility = Mathf.MoveTowards(_visibility, target, rate * Time.deltaTime);

            promptUI?.SetVisibility(_visibility);
            _currentTarget?.SetHighlight(_visibility);
        }

        private void OnDrawGizmosSelected()
        {
            if (Tunables.I == null)
            {
                return;
            }

            Vector3 origin = playerCamera != null ? playerCamera.transform.position : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin, Tunables.I.InteractionNearbyDistance);
        }
    }
}
