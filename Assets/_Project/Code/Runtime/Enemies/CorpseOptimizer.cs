using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MrMoonlight.Data;
using UnityEngine;

namespace MrMoonlight.Enemies
{
    /// <summary>
    /// Raised when a corpse has settled and is worth culling. The AST-145 bridge (a git-ignored
    /// assembly that lives next to the asset in Assets/ThirdParty) subscribes; without it nothing
    /// happens, so a clone that does not have the asset still builds and plays.
    /// </summary>
    public static class CorpseCullingHooks
    {
        /// <summary>Corpse root, the body renderers to switch off while hidden, and their world bounds.</summary>
        public static event Action<GameObject, Renderer[], Bounds> CorpseSettled;

        /// <summary>Corpses currently handed to the culler (written by the bridge, read by the session log).</summary>
        public static int Registered;

        /// <summary>Corpses whose renderer is switched off right now (written by the bridge).</summary>
        public static int CulledNow;

        /// <summary>Raised just before a dissolved corpse is destroyed, so the culler can drop its bookkeeping.</summary>
        public static event Action<GameObject> CorpseRemoving;

        internal static void Raise(GameObject corpse, Renderer[] renderers, Bounds bounds)
            => CorpseSettled?.Invoke(corpse, renderers, bounds);

        internal static void RaiseRemoving(GameObject corpse) => CorpseRemoving?.Invoke(corpse);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // The subscriber list is NOT cleared: the bridge subscribes in the same startup phase, in
            // no guaranteed order, and clearing here could silently drop it.
            Registered = 0;
            CulledNow = 0;
        }
    }

    /// <summary>
    /// The "settle" pass for a dead enemy, run by <see cref="EnemyCorpseCleanup"/> after the AI strip and
    /// <see cref="MoonlightTunables.CorpseSettleDelay"/>. The AI side was already off; this removes what
    /// the render and physics side was still doing on a body that does not move (MRM-85, corpses were
    /// the biggest cost left: frame ~9.7 ms at 0 corpses to 24-29 ms at 60-100, SetPass ~600 to ~2,000).
    /// Every step is a tunable (see "Corpse optimization" in MoonlightTunables): Legion mode wants them
    /// all on; story mode wants gore and physics back (you can shoot and dismember a corpse there).
    /// Nothing is ever removed from the world: corpses stay looking like corpses.
    /// Owner: MRM-85, 2026-09-30.
    /// </summary>
    public static class CorpseOptimizer
    {
        private static readonly Regex LodSuffix = new Regex(@"_LOD(\d+)$", RegexOptions.Compiled);

        /// <summary>Corpses that finished the settle pass this session. Shown in the session log.</summary>
        public static int SettledCount { get; private set; }

        /// <summary>Corpses fully dissolved and destroyed this session. Shown in the session log.</summary>
        public static int DissolvedCount { get; private set; }

        internal static void NoteDissolved() => DissolvedCount++;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SettledCount = 0;
            DissolvedCount = 0;
        }

        public static IEnumerator Settle(GameObject corpse)
        {
            MoonlightTunables t = Tunables.I;
            if (corpse == null || !t.CorpseOptimizeEnabled) yield break;

            // Steps 1 and 6 on the body and on whatever it dropped (lamp, shotgun).
            if (t.CorpseShadowsOff)
            {
                foreach (Renderer r in corpse.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if (corpse.TryGetComponent(out EnemyDeathDrop drops))
            {
                foreach (Transform item in drops.DroppedItems)
                {
                    if (item == null) continue;
                    if (t.CorpseDropKeepLodIndex >= 0) KeepOnlyLod(item, t.CorpseDropKeepLodIndex);
                    if (!t.CorpseShadowsOff) continue;
                    foreach (Renderer r in item.GetComponentsInChildren<Renderer>(true))
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            // Step 3: gore and blood. Story mode keeps these (dismembering corpses).
            if (t.CorpseDisableGore)
            {
                if (corpse.TryGetComponent(out PampelGames.GoreSimulator.GoreSimulator gore)) gore.enabled = false;
                foreach (ParticleSystem ps in corpse.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // Bounds for the culling proxy, taken before anything below can change them.
            Renderer[] body = VisibleBodyRenderers(corpse, out Bounds bounds);

            // Step 2: freeze the pose. A disabled Animator leaves every bone where it is.
            if (t.CorpseDisableAnimator && corpse.TryGetComponent(out Animator anim)) anim.enabled = false;

            // Step 5: the ragdoll. Joints first, one frame later the rigidbodies they depend on.
            if (t.CorpseStripPhysics)
            {
                foreach (Joint j in corpse.GetComponentsInChildren<Joint>(true)) UnityEngine.Object.Destroy(j);
                yield return null;
                if (corpse == null) yield break;
                foreach (Rigidbody rb in corpse.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.Destroy(rb);
            }

            // Step 7: hand the body to the dynamic culler (no-op without the bridge or a controller).
            if (t.CorpseCullingEnabled && body.Length > 0) CorpseCullingHooks.Raise(corpse, body, bounds);

            SettledCount++;
        }

        private static Renderer[] VisibleBodyRenderers(GameObject corpse, out Bounds bounds)
        {
            var list = new List<Renderer>();
            bounds = new Bounds(corpse.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in corpse.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                list.Add(r);
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            return list.ToArray();
        }

        /// <summary>
        /// A prop built from separate LOD meshes with no LODGroup (the shotgun has _LOD0, _LOD1 and _LOD2
        /// children; only LOD0, about 5 renderers, is switched on) is set to show exactly one: the requested
        /// index, or the nearest existing one. LOD1 is a single renderer.
        /// </summary>
        private static void KeepOnlyLod(Transform item, int keepIndex)
        {
            var lods = new List<KeyValuePair<Transform, int>>();
            foreach (Transform child in item)
            {
                Match m = LodSuffix.Match(child.name);
                if (m.Success) lods.Add(new KeyValuePair<Transform, int>(child, int.Parse(m.Groups[1].Value)));
            }
            if (lods.Count < 2) return;

            int chosen = lods[0].Value;
            foreach (var kv in lods)
                if (Mathf.Abs(kv.Value - keepIndex) < Mathf.Abs(chosen - keepIndex)) chosen = kv.Value;

            // Activate the chosen one too: the shotgun's LOD children start with only LOD0 active.
            foreach (var kv in lods)
                kv.Key.gameObject.SetActive(kv.Value == chosen);
        }
    }
}
