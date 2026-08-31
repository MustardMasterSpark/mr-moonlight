using UnityEngine;

namespace Burntwax
{
    [RequireComponent(typeof(Rigidbody))]
    public class GunPickup : MonoBehaviour
    {

        [SerializeField] int ammoCount;

        [SerializeField] GunScriptableObject gun;
        private GunStateMachine gunSelector;


        void Awake()
        {
            // if (GameManager.Instance.IsLoadingFromSave)
            //     return;
            // if (ObjectManager.Instance == null)
            //     return;
            // ObjectManager.Instance.Register(gameObject);
        }
        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerHealth>() != false)
            {
                gunSelector = FindFirstObjectByType<GunStateMachine>();
                if (!gunSelector.CurrentGuns.Contains(gun))
                {
                    gunSelector.Pickup(gun);
                }
                else
                {
                    if (gun.AmmoConfig.currentAmmo < gun.AmmoConfig.maxAmmo)
                    {
                        if (gun.AmmoConfig.maxAmmo - gun.AmmoConfig.currentAmmo > ammoCount)
                        {
                            gun.AmmoConfig.currentAmmo += ammoCount;
                        }
                        else
                        {
                            gun.AmmoConfig.currentAmmo += gun.AmmoConfig.maxAmmo - gun.AmmoConfig.currentAmmo;
                        }

                    }
                }

                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

    }
}

