using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools.Migration
{
    /// <summary>
    /// One-shot migration for MRM-9's HQ FPS swap.
    ///
    /// PolymindGames' FPSCore hardcodes layer indices 6-17 in <c>LayerConstants</c>, and 6-13 of
    /// those collided with Mr. Moonlight's own gameplay layers (Enemy, Destructible, Ground,
    /// DroppedProp, Health, Consumable, Weapon, NPC), which predate the framework and are load
    /// bearing for Blaze AI, the NavMesh and MRM-34's enemy hitboxes. Rather than renumber ours,
    /// FPSCore's eight were moved to the free slots 18-25.
    ///
    /// Assets copied in from the Weapons project still carry the *original* numbers, both on
    /// GameObjects and inside serialized LayerMask fields, so every one of them has to be
    /// rewritten once. Kept in the repo rather than run and deleted so the same pass can be
    /// re-run if more HQ FPS content is migrated later.
    /// </summary>
    public static class PolymindLayerRemap
    {
        private static readonly Dictionary<int, int> Map = new Dictionary<int, int>
        {
            { 6, 18 },  // Debris
            { 7, 19 },  // Effect
            { 8, 20 },  // TriggerZone
            { 9, 21 },  // Interactable
            { 10, 22 }, // ViewModel
            { 11, 23 }, // PostProcessing
            { 12, 24 }, // Hitbox
            { 13, 25 }, // Character
        };

        private static readonly string[] Roots =
        {
            "Assets/ThirdParty/PolymindGames",
            "Assets/_Project/Data/PolymindGames",
        };

        [MenuItem("Tools/MrMoonlight/Migration/Remap Polymind Layers (MRM-9)")]
        public static void Run()
        {
            int assets = 0, layers = 0, masks = 0;
            var log = new StringBuilder();

            string[] guids = AssetDatabase.FindAssets("t:Prefab", Roots);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // LoadMainAssetAtPath + SetDirty does NOT persist edits to a prefab asset's own
                // hierarchy - only to nested prefab instances, which save through their own asset.
                // The first run of this pass hit exactly that: every wieldable was remapped but
                // FPS_Player's own Systems/Body/Head/Hitbox kept the vendor numbers. Editing a
                // prefab asset has to go through a prefab-contents scope.
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    continue;
                }

                bool dirty = false;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    int layer = t.gameObject.layer;
                    int moved;
                    if (Map.TryGetValue(layer, out moved))
                    {
                        t.gameObject.layer = moved;
                        layers++;
                        dirty = true;
                    }
                }

                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (RemapMasks(c, ref masks))
                    {
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    assets++;
                    log.AppendLine("  " + path);
                }

                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", Roots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                {
                    continue;
                }

                if (RemapMasks(asset, ref masks))
                {
                    EditorUtility.SetDirty(asset);
                    assets++;
                    log.AppendLine("  " + path);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[MRM-9] Polymind layer remap: " + assets + " assets, " + layers
                      + " GameObject layers, " + masks + " LayerMask fields.\n" + log);
        }

        /// <summary>Rewrites every serialized LayerMask on <paramref name="target"/>. "Everything"
        /// (-1) and "Nothing" (0) are left alone - they mean the same thing after the move.</summary>
        private static bool RemapMasks(Object target, ref int masks)
        {
            if (target == null)
            {
                return false;
            }

            var so = new SerializedObject(target);
            var it = so.GetIterator();
            bool changed = false;
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.LayerMask)
                {
                    continue;
                }

                int remapped = RemapMask(it.intValue);
                if (remapped != it.intValue)
                {
                    it.intValue = remapped;
                    masks++;
                    changed = true;
                }
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }

        private static int RemapMask(int mask)
        {
            if (mask == -1 || mask == 0)
            {
                return mask;
            }

            int result = mask;
            foreach (var pair in Map)
            {
                result &= ~(1 << pair.Key);
            }

            foreach (var pair in Map)
            {
                if ((mask & (1 << pair.Key)) != 0)
                {
                    result |= 1 << pair.Value;
                }
            }

            return result;
        }
    }
}
