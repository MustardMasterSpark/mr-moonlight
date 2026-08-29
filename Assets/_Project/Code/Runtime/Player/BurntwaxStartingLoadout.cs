using System.Collections.Generic;
using Burntwax;
using UnityEngine;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Equips the player's starting weapons on spawn.
    ///
    /// <para><b>Why this is needed.</b> The Burntwax <see cref="GunStateMachine"/> only spawns a
    /// weapon model through <see cref="GunStateMachine.Pickup"/> — assigning
    /// <c>ActiveGunScriptable</c> in the inspector sets the data but never runs the equip path, so
    /// the player would start holding nothing while the HUD claimed otherwise. Their demo scene
    /// sidestepped this by starting the player unarmed and relying on world pickups.</para>
    ///
    /// <para>Leaving this list empty reproduces that unarmed start, which is the intended flow for
    /// the demo: find the pistol in the world. Populate it when a scene needs the player armed
    /// from the first frame — a test scene, or a later chapter that starts mid-story.</para>
    ///
    /// <para>When the inventory issue (MRM-41/42) takes over persistent loadout, this is the hook
    /// it should replace: one place that decides what the player spawns holding.</para>
    ///
    /// Owner: MRM-9.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class BurntwaxStartingLoadout : MonoBehaviour
    {
        [Tooltip("Weapons the player spawns with, equipped in order. Empty means start unarmed and pick weapons up in the world.")]
        [SerializeField] private List<GunScriptableObject> startingGuns = new List<GunScriptableObject>();

        [Tooltip("Gun state machine to equip into. Found in children if left unassigned.")]
        [SerializeField] private GunStateMachine gunStateMachine;

        private void Start()
        {
            if (gunStateMachine == null)
            {
                // From the prefab root - the Arms live under the camera rig, not below us. (MRM-9)
                gunStateMachine = transform.root.GetComponentInChildren<GunStateMachine>(true);
            }

            if (gunStateMachine == null)
            {
                Debug.LogError($"{nameof(BurntwaxStartingLoadout)} on '{name}' found no {nameof(GunStateMachine)}.", this);
                return;
            }

            foreach (GunScriptableObject gun in startingGuns)
            {
                if (gun == null) continue;

                // Pickup() is a no-op if the gun is already held, so this is safe to call blindly.
                gunStateMachine.Pickup(gun);
            }
        }
    }
}
