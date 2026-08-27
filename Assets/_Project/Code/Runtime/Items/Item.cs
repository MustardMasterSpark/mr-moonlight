using MrMoonlight.Interaction;
using UnityEngine;

namespace MrMoonlight.Items
{
    /// <summary>
    /// A pickup in the world: an <see cref="Interaction.Interactable"/> plus an
    /// <see cref="ItemDefinition"/>. Subscribes to the Interactable's own
    /// <see cref="Interaction.Interactable.OnInteracted"/> hook (MRM-16) rather than the other way
    /// around - MRM-16 doesn't know MRM-41 exists. On pickup: added to the interactor's
    /// <see cref="Inventory"/> (found via GetComponent on the interactor the event passes through,
    /// not a scene Find); on success the prop is destroyed - "prop removed from the world" is the
    /// universal pickup rule per the issue, not conditional on item type. On refusal (storage full)
    /// the prop stays exactly where it is - <see cref="Inventory.OnPickupRefused"/> has already
    /// fired for the feedback UI to react to. Owner: MRM-41
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class Item : MonoBehaviour
    {
        [Header("Item — MRM-41")]
        [SerializeField] private ItemDefinition definition;

        private Interactable _interactable;

        public ItemDefinition Definition => definition;

        private void Awake()
        {
            _interactable = GetComponent<Interactable>();

            if (definition == null)
            {
                Debug.LogError($"[Item] {name} has no ItemDefinition assigned. See MRM-41.", this);
            }
        }

        private void OnEnable() => _interactable.OnInteracted += HandleInteracted;

        private void OnDisable() => _interactable.OnInteracted -= HandleInteracted;

        private void HandleInteracted(Interactable source, GameObject interactor)
        {
            if (definition == null)
            {
                return;
            }

            if (!interactor.TryGetComponent(out Inventory inventory))
            {
                Debug.LogError($"[Item] {interactor.name} interacted with {name} but has no Inventory. See MRM-41.", this);
                return;
            }

            if (inventory.TryAddItem(definition))
            {
                Destroy(gameObject);
            }
        }
    }
}
