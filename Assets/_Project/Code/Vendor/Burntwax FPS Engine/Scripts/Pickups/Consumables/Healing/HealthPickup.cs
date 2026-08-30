using UnityEngine;

namespace Burntwax
{

    [RequireComponent(typeof(Rigidbody))]
    public class HealthPickup : MonoBehaviour
    {

        public int heal;


        private PlayerHealth playerHealth;

        void Awake()
        {
            GetComponent<Rigidbody>().transform.localScale = new Vector3(0.4f, 0.2f, 0.4f);
        }

        //Collect health
        void OnTriggerEnter(Collider other)
        {

            if (other.GetComponentInParent<PlayerHealth>() != false)
            {
                playerHealth = FindFirstObjectByType<PlayerHealth>();
                if (playerHealth.CurrentHealth() < playerHealth.maxHealth)
                {
                    playerHealth.RestoreHealth(heal);
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                }

            }
        }

    }

}

