using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Burntwax
{

    [RequireComponent(typeof(Rigidbody))]
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private Rigidbody rb;

        public Vector3 spawnPoint;

        [SerializeField] private float disableTime = 2f;

        public delegate void CollisionEvent(Bullet bullet, Collision collision);
        public event CollisionEvent OnCollision;


        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }


        public void Spawn(Vector3 spawnForce)
        {
            spawnPoint = transform.position;
            transform.forward = spawnForce.normalized;
            rb.AddForce(spawnForce);
            StartCoroutine(DelayedDisable(disableTime));
        }

        private IEnumerator DelayedDisable(float time)
        {
            yield return new WaitForSeconds(time);
            OnCollisionEnter(null);
        }

        private void OnCollisionEnter(Collision collision)
        {
            OnCollision?.Invoke(this, collision);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            OnCollision = null;
        }
    }

}