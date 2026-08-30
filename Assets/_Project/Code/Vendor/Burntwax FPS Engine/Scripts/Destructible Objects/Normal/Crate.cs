using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Burntwax
{

    [RequireComponent(typeof(Rigidbody))]
    public class Crate : MonoBehaviour, Destructible
    {

        [Header("Health")]
        [SerializeField] public float currentHealth;


        [Header("Customizables")]
        [SerializeField] bool destroy;
        [SerializeField] private int itemsToSpawn = 3;
        public List<GameObject> resources_prefabs;
        public float CurrentHealth { get => currentHealth; set => currentHealth = value; }

        public void TakeDamage(float damage)
        {
            float damageTaken = Mathf.Clamp(damage, 0, currentHealth);
            currentHealth -= damageTaken;
            if (currentHealth == 0 && damageTaken != 0)
            {
                Destruct(transform.position);

            }
        }

        private void SpawnResources(Vector3 pos)
        {
            for (int i = 0; i < itemsToSpawn; i++)
            {
                Instantiate(resources_prefabs[Random.Range(0, resources_prefabs.Count)], pos, Quaternion.identity);
            }

        }

        void Awake()
        {
            GetComponent<Rigidbody>().transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        }

        public void Destruct(Vector3 pos)
        {
            SpawnResources(pos);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

}