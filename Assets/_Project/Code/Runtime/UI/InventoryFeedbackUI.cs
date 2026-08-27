using System.Collections;
using MrMoonlight.Data;
using MrMoonlight.Items;
using TMPro;
using UnityEngine;

namespace MrMoonlight.UI
{
    /// <summary>
    /// The "clear feedback" MRM-41's AC demands when a pickup is refused because storage is full -
    /// a short on-screen message, unstyled per the project's established pattern (see
    /// <see cref="GameOverPanel"/>). Reuses <see cref="FadeOverlay"/> for the show/hold/hide beat
    /// rather than a second hand-rolled fade coroutine. Subscribes to the player's
    /// <see cref="Inventory.OnPickupRefused"/>; nothing else triggers it. Owner: MRM-41
    /// </summary>
    [RequireComponent(typeof(FadeOverlay))]
    public sealed class InventoryFeedbackUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Inventory inventory;
        [SerializeField] private FadeOverlay fadeOverlay;
        [SerializeField] private TMP_Text label;

        private Coroutine _routine;

        private void Awake()
        {
            if (fadeOverlay == null)
            {
                fadeOverlay = GetComponent<FadeOverlay>();
            }

            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>();
            }
        }

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.OnPickupRefused += HandleRefused;
            }
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.OnPickupRefused -= HandleRefused;
            }
        }

        private void HandleRefused(ItemDefinition definition)
        {
            if (label != null)
            {
                string storageName = inventory.HasBackpack ? "Backpack" : "Pocket";
                string itemName = !string.IsNullOrEmpty(definition.DisplayName) ? definition.DisplayName : definition.Id.ToString();
                label.SetText($"{storageName} is full - can't carry another {itemName}.");
            }

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(ShowAndHide());
        }

        private IEnumerator ShowAndHide()
        {
            yield return fadeOverlay.FadeToOpaque(Tunables.I.InventoryFullFeedbackFadeDuration);
            yield return new WaitForSeconds(Tunables.I.InventoryFullFeedbackHoldDuration);
            yield return fadeOverlay.FadeToClear(Tunables.I.InventoryFullFeedbackFadeDuration);
            _routine = null;
        }
    }
}
