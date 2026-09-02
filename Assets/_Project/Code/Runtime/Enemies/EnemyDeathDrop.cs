using System.Collections.Generic;
using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// Detaches carried props when the enemy dies, so they fall and roll instead of vanishing with
    /// the body or staying welded to a ragdolling hand. MRM-34 asks for exactly this for the
    /// Spotter's lamp and shotgun.
    ///
    /// The lamp matters most: its <c>Light</c> keeps burning on the ground, which is both the
    /// acceptance criterion and a genuinely good horror beat — a dropped lamp still lighting the
    /// clearing where the body is. Nothing here touches the Light; detaching it and leaving it
    /// alone is what makes that work.
    ///
    /// Pickup behaviour is deliberately <b>not</b> here. MRM-26 owns the universal pickup rule and
    /// is still Backlog; Carlos's call for this pass was detach only. When MRM-26 lands it adds its
    /// own component to the dropped prefab and nothing in this file changes. Owner: MRM-34.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/Enemies/Enemy Death Drop")]
    public sealed class EnemyDeathDrop : MonoBehaviour
    {
        [System.Serializable]
        public struct Drop
        {
            [Tooltip("The prop to detach. Usually the weapon or lamp instance parented under a hand socket.")]
            public Transform item;

            [Tooltip("Mass of the dropped rigidbody, in kilograms.")]
            public float mass;

            [Tooltip("Collider radius used when the prop has no collider of its own, in metres.")]
            public float fallbackColliderRadius;
        }

        [Tooltip("Everything this enemy is carrying that should hit the ground when it dies.")]
        [SerializeField] private Drop[] drops = new Drop[0];

        [Tooltip("Where dropped props are re-parented. Leave empty to drop them at scene root, which is what keeps them alive after the body is destroyed.")]
        [SerializeField] private Transform dropParent;

        private readonly List<Rigidbody> _dropped = new List<Rigidbody>();

        /// <summary>Wired to <see cref="EnemyHealth.Died"/>. Safe to call twice.</summary>
        public void DropAll()
        {
            for (int i = 0; i < drops.Length; i++)
            {
                DropOne(drops[i]);
            }
        }

        private void DropOne(Drop drop)
        {
            if (drop.item == null || drop.item.parent == dropParent) return;

            Transform item = drop.item;
            item.SetParent(dropParent, worldPositionStays: true);
            item.gameObject.SetActive(true);

            EnsureCollider(item, drop.fallbackColliderRadius);

            if (!item.TryGetComponent(out Rigidbody body))
            {
                body = item.gameObject.AddComponent<Rigidbody>();
            }

            body.mass = drop.mass > 0f ? drop.mass : 1f;
            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // A small random impulse, not a straight drop — MRM-34 asks for "falls and rolls", and
            // a prop released with zero velocity just lands flat under the hand it left.
            Vector3 impulse = (Random.insideUnitSphere + Vector3.up).normalized * Tunables.I.EnemyDropScatterImpulse;
            body.AddForce(impulse, ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * Tunables.I.EnemyDropScatterImpulse, ForceMode.Impulse);

            _dropped.Add(body);

            float lifetime = Tunables.I.EnemyDropLifetime;
            if (lifetime > 0f) Destroy(item.gameObject, lifetime);
        }

        private static void EnsureCollider(Transform item, float fallbackRadius)
        {
            if (item.GetComponentInChildren<Collider>(true) != null) return;

            var sphere = item.gameObject.AddComponent<SphereCollider>();
            sphere.radius = fallbackRadius > 0f ? fallbackRadius : 0.12f;
        }
    }
}
