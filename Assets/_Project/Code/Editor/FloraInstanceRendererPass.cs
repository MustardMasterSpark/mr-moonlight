using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Adds MA.Flora.FloraInstanceRenderer to every GameObject Gaia spawned under
    /// "&lt;Terrain&gt;/Gaia Game Object Spawns/". Run AFTER a Gaia spawn pass, once density/spacing
    /// are tuned and the result looks right.
    ///
    /// WHY THIS EXISTS
    /// ----------------
    /// Gaia's GameObject-type spawn rules place real GameObjects (needed for real colliders — see
    /// MRM-70's biome doc §10.3, "terrain trees silently reject MeshColliders"). Real GameObjects
    /// render through Unity's normal per-object path, which is expensive at the counts a biome
    /// vegetation pass produces. Flora (com.ma.flora) can draw the same objects far more cheaply via
    /// its GPU-driven batched renderer, WITHOUT touching collision — but only for objects it knows
    /// about. FloraInstanceRenderer is the opt-in: add it to an object with a MeshRenderer/LODGroup
    /// and Flora takes over drawing that renderer (verified: it sets MeshRenderer.forceRenderingOff,
    /// not a second draw call) while the object's Transform/Collider/physics stay completely normal.
    ///
    /// This is a one-time editor-time pass, not a runtime cost — see the mrm70-vegetation-flora-pipeline
    /// memory for the full writeup. It does NOT fix an oversized instance count; that's a spacing/
    /// density problem to fix in the Gaia rules first. This only makes an already-reasonable count
    /// cheaper to draw.
    ///
    /// Prefab structure note: this project's vegetation prefabs keep the Collider on the root and the
    /// MeshRenderer on a child (typically named "Visual") — see prop-wizard pipeline. This pass
    /// checks the root first, then searches children, so it works either way.
    /// </summary>
    public static class FloraInstanceRendererPass
    {
        private const string SpawnContainerName = "Gaia Game Object Spawns";

        [MenuItem("Tools/MrMoonlight/Vegetation/Add Flora Instance Renderers to Spawned Vegetation")]
        public static void Run()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("FloraInstanceRendererPass: no Terrain found in the open scene.");
                return;
            }

            var container = terrain.transform.Find(SpawnContainerName);
            if (container == null)
            {
                Debug.LogWarning($"FloraInstanceRendererPass: no '{SpawnContainerName}' under {terrain.name} — nothing spawned yet.");
                return;
            }

            int added = 0, alreadyHad = 0, noRendererFound = 0;

            for (int i = 0; i < container.childCount; i++)
            {
                var speciesGroup = container.GetChild(i);
                for (int j = 0; j < speciesGroup.childCount; j++)
                {
                    var instance = speciesGroup.GetChild(j);

                    GameObject rendererGO = null;
                    if (instance.GetComponent<MeshRenderer>() != null || instance.GetComponent<LODGroup>() != null)
                    {
                        rendererGO = instance.gameObject;
                    }
                    else
                    {
                        var mr = instance.GetComponentInChildren<MeshRenderer>(true);
                        if (mr != null)
                        {
                            rendererGO = mr.gameObject;
                        }
                        else
                        {
                            var lod = instance.GetComponentInChildren<LODGroup>(true);
                            if (lod != null) rendererGO = lod.gameObject;
                        }
                    }

                    if (rendererGO == null)
                    {
                        noRendererFound++;
                        continue;
                    }

                    if (rendererGO.GetComponent<MA.Flora.FloraInstanceRenderer>() != null)
                    {
                        alreadyHad++;
                        continue;
                    }

                    Undo.AddComponent<MA.Flora.FloraInstanceRenderer>(rendererGO);
                    added++;
                }
            }

            Debug.Log($"FloraInstanceRendererPass: added {added}, already had it {alreadyHad}, no renderer found {noRendererFound}.");
        }
    }
}
