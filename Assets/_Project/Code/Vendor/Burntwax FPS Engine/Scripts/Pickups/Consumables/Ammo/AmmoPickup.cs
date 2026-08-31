using UnityEngine;

namespace Burntwax
{
    [RequireComponent(typeof(Rigidbody))]
    public class AmmoPickup : MonoBehaviour
    {

        public int ammoCount;


        [SerializeField] GunScriptableObject gun;

        private GunStateMachine playerGun;

        void Awake()
        {
            GetComponent<Rigidbody>().transform.localScale = new Vector3(0.2f, 0.6f, 0.2f);
        }


        // void Start()
        // {
        //     if (GameManager.Instance.IsLoadingFromSave)
        //         return;
        //     ObjectManager.Instance.Register(gameObject);
        // }


        //Collect ammo
        void OnTriggerEnter(Collider other)
        {

            if (other.GetComponentInParent<PlayerHealth>())
            {
                playerGun = FindFirstObjectByType<GunStateMachine>();

                if (playerGun.CurrentGuns.Contains(gun))
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
                        gameObject.SetActive(false);
                        Destroy(gameObject);
                    }
                }
            }
        }
    }
}