using System.Collections;
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

            [Tooltip("Falls straight down under gravity with no scatter impulse/torque (so it doesn't roll or go flying), then freezes solid — collider disabled, rigidbody removed — the instant it settles on the ground, instead of staying a live physics object forever. Added for the shotgun (2026-09-10, island demo wrap-up) — the scatter impulse plus a collider spawning inside the enemy/terrain was launching it across the map.")]
            public bool groundSnapNoPhysics;
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

            if (drop.groundSnapNoPhysics)
            {
                DropGroundSnapped(item, drop);
                return;
            }

            EnsureCollider(item, drop.fallbackColliderRadius);
            AssignDroppedPropLayer(item);

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

        /// <summary>Real gravity drop, no scatter impulse — it falls straight down and lands wherever
        /// the ground actually is, rather than trusting a fixed-distance raycast that goes wrong the
        /// moment a drop happens somewhere taller than expected (found live 2026-09-10: the previous
        /// raycast-snap version left the shotgun floating in mid-air when the guess missed). Needs a
        /// collider to land on something at all; <see cref="FreezeWhenSettled"/> strips the collider
        /// and rigidbody back off the instant it comes to rest, so it never slides afterward and
        /// costs nothing once frozen.</summary>
        private void DropGroundSnapped(Transform item, Drop drop)
        {
            EnsureCollider(item, drop.fallbackColliderRadius);
            AssignDroppedPropLayer(item);

            if (!item.TryGetComponent(out Rigidbody body))
            {
                body = item.gameObject.AddComponent<Rigidbody>();
            }

            body.mass = drop.mass > 0f ? drop.mass : 1f;
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // The fallback collider is a sphere, and a sphere has essentially zero rolling
            // resistance — on any slope (this island is mostly slopes and cliffs) it just rolls
            // downhill forever under gravity alone, no impulse required. That, not an impulse, is
            // what was actually launching the shotgun across the map. Freezing rotation stops it
            // from ever rolling at all: it can only translate straight down and settle exactly
            // under where it was dropped.
            body.constraints = RigidbodyConstraints.FreezeRotation;

            StartCoroutine(FreezeWhenSettled(item, body));
        }

        /// <summary>Waits for the rigidbody to fall asleep (Unity's own at-rest detection — the
        /// reliable signal that it actually landed, unlike a fixed timer) and freezes it there:
        /// collider off, rigidbody gone. A timeout guards the case where it never settles, e.g. it
        /// fell off the edge of the NavMesh into open space.</summary>
        private IEnumerator FreezeWhenSettled(Transform item, Rigidbody body)
        {
            float elapsed = 0f;
            float timeout = Tunables.I.EnemyDropGroundSnapTimeout;

            while (item != null && body != null && !body.IsSleeping() && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (item == null) yield break;

            foreach (var collider in item.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            if (body != null) Destroy(body);

            float lifetime = Tunables.I.EnemyDropLifetime;
            if (lifetime > 0f) Destroy(item.gameObject, lifetime);
        }

        /// <summary>Puts the drop on the dedicated DroppedProp layer, which the project's physics
        /// matrix (Edit &gt; Project Settings &gt; Physics, set 2026-09-10) is configured to never
        /// collide with Enemy or Ragdoll. Fixes the actual cause of the shotgun launching across the
        /// map (found live 2026-09-10): its new collider was spawning inside the dying enemy's own
        /// capsule collider, and once Blaze's ragdoll takes over a frame later the flailing bone
        /// colliders would have kept hitting it too — a per-collider <c>Physics.IgnoreCollision</c>
        /// call here can't reach colliders that don't exist yet, but a layer-level ignore covers them
        /// automatically the moment they spawn. Recurses because these prefabs are multi-part meshes,
        /// not a single collider on the root.</summary>
        private static void AssignDroppedPropLayer(Transform item)
        {
            int layer = LayerMask.NameToLayer("DroppedProp");
            if (layer < 0) return;

            SetLayerRecursively(item.gameObject, layer);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;

            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void EnsureCollider(Transform item, float fallbackRadius)
        {
            if (item.GetComponentInChildren<Collider>(true) != null) return;

            var sphere = item.gameObject.AddComponent<SphereCollider>();
            sphere.radius = fallbackRadius > 0f ? fallbackRadius : 0.12f;
        }
    }
}
