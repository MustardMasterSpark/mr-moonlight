using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.EditorTools.Migration
{
    /// <summary>
    /// MRM-9: builds Mr. Moonlight's player prefab out of PolymindGames' <c>FPS_Player</c>.
    ///
    /// The HQ FPS demo player carries all nineteen wieldables and is wired to the vendor's own
    /// keyboard-only input asset. Mr. Moonlight ships four weapons plus unarmed hands, and MRM-8's
    /// action asset is the single source of input truth - it already has both a Keyboard and Mouse
    /// scheme and a Gamepad scheme, which the vendor asset does not. So this pass strips the
    /// wieldable list down and repoints every <see cref="InputActionReference"/> at our actions,
    /// leaving one input path rather than two.
    ///
    /// Re-runnable: it rebuilds the prefab from the vendor source each time.
    ///
    /// <para><b>⚠ Re-running deletes and recreates the prefab asset, which breaks the link from any
    /// existing scene instance.</b> `Island.unity` holds one. If this is re-run, the Player in the
    /// scene has to be replaced with a fresh instance and the Event Director re-pointed at it - see
    /// Docs/mrm9-hqfps-integration.md §9.</para>
    /// </summary>
    public static class PolymindPlayerBuild
    {
        private const string Source = "Assets/ThirdParty/PolymindGames/FPSCore/Prefabs/Core/FPS_Player.prefab";
        private const string Target = "Assets/_Project/Prefabs/Player/Player_Tracey.prefab";
        private const string OurActions = "Assets/InputSystem_Actions.inputactions";

        /// <summary>The wieldables Tracey actually carries. Everything else in the vendor demo
        /// player is deleted - see Docs/mrm9-hqfps-integration.md for why these four.</summary>
        private static readonly HashSet<string> KeepWieldables = new HashSet<string>
        {
            "HQFPS_Wieldable_Arms",          // unarmed hands, incl. the Unarmed_Run_ animation
            "HQFPS_Wieldable_M1911",
            "HQFPS_Wieldable_Crossbow",
            "HQFPS_Wieldable_BaseballBat",
            "HQFPS_Wieldable_DBShotgun",
        };

        /// <summary>Vendor action name to Mr. Moonlight action name, both in the Gameplay map.
        /// Anything not listed is deliberately unmapped: the fist melee, lean, item wheel, book,
        /// throwables and drop are all out of scope for the demo.</summary>
        private static readonly Dictionary<string, string> ActionMap = new Dictionary<string, string>
        {
            { "Move", "Move" },
            { "Look", "Look" },
            { "Jump", "Jump" },
            { "Crouch", "Crouch" },
            { "Run", "Sprint" },
            { "Use", "Fire" },
            { "Aim", "AimDownSights" },
            { "Reload", "Reload" },
            { "Interact", "Interact" },
            { "Cycle", "SwitchWeapon" },
            { "Escape", "Pause" },
        };

        /// <summary>Vendor actions Mr. Moonlight has no use for - Drop, Select, Holster, Heal,
        /// Throw, FireMode, Scroll, Lean, Arms. They can't just be nulled: the vendor's own
        /// <c>InputExtensions.EnsureActionIsNotNull</c> only logs and then dereferences anyway, so a
        /// null reference is a NullReferenceException on enable. Leaving them pointed at the vendor
        /// asset is worse - its Drop / Holster / Select bindings would fight our weapon cycling. So
        /// they get routed to a real action in our own asset that has no bindings at all, which
        /// means it can never fire and the vendor asset ends up completely unreferenced.</summary>
        private const string UnboundAction = "Unbound";

        /// <summary>Components that exist only to serve features we aren't shipping. Removed
        /// outright rather than left listening to a dead action.</summary>
        private static readonly HashSet<string> DropComponents = new HashSet<string>
        {
            "FPSBodyLeanInput",     // peek/lean - not a demo mechanic
            "BodyLeanHandler",
            "FPSArmsChangeInput",   // cycles the arm-mesh variant at runtime; we pick one and keep it
        };

        [MenuItem("Tools/MrMoonlight/Migration/Build Player_Tracey from FPS_Player (MRM-9)")]
        public static void Run()
        {
            var log = new StringBuilder();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(Source) == null)
            {
                Debug.LogError("[MRM-9] Source prefab missing: " + Source);
                return;
            }

            const string folder = "Assets/_Project/Prefabs/Player";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Player");
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(Target) != null)
            {
                AssetDatabase.DeleteAsset(Target);
            }

            if (!AssetDatabase.CopyAsset(Source, Target))
            {
                Debug.LogError("[MRM-9] CopyAsset failed: " + Source + " -> " + Target);
                return;
            }

            AssetDatabase.ImportAsset(Target, ImportAssetOptions.ForceUpdate);

            GameObject root = PrefabUtility.LoadPrefabContents(Target);
            int removed = StripWieldables(root, log);
            int scopes = RemoveCrossbowScopes(root, log);
            int comps = RemoveOutOfScopeComponents(root, log);
            int rebound = RebindInput(root, log);
            int ammo = MakeReserveAmmoInfinite(root, log);
            WireMoonlightSystems(root, log);

            PrefabUtility.SaveAsPrefabAsset(root, Target);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();

            Debug.Log("[MRM-9] Player_Tracey built: removed " + removed + " wieldables, "
                      + scopes + " crossbow scope attachments, " + comps
                      + " out-of-scope components, rebound " + rebound
                      + " input references, " + ammo + " firearms on infinite reserve ammo.\n" + log);
        }

        private static int StripWieldables(GameObject root, StringBuilder log)
        {
            Transform holder = FindDeep(root.transform, "Root");
            if (holder == null)
            {
                log.AppendLine("  WARNING: wieldable holder 'Root' not found, nothing stripped");
                return 0;
            }

            var doomed = new List<GameObject>();
            for (int i = 0; i < holder.childCount; i++)
            {
                GameObject child = holder.GetChild(i).gameObject;
                if (!KeepWieldables.Contains(child.name))
                {
                    doomed.Add(child);
                }
            }

            foreach (GameObject go in doomed)
            {
                log.AppendLine("  removed wieldable: " + go.name);
                Object.DestroyImmediate(go);
            }

            return doomed.Count;
        }

        /// <summary>Carlos asked for the crossbow without a scope, so the 5x scope attachment is
        /// deleted outright rather than left switched off - an unused attachment still drags its
        /// mesh and material into the build.</summary>
        private static int RemoveCrossbowScopes(GameObject root, StringBuilder log)
        {
            var doomed = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("SniperScope"))
                {
                    doomed.Add(t.gameObject);
                }
            }

            int count = 0;
            foreach (GameObject go in doomed)
            {
                // Deleting a parent first leaves its children in the list already destroyed.
                if (go == null)
                {
                    continue;
                }

                log.AppendLine("  removed scope: " + go.name);
                Object.DestroyImmediate(go);
                count++;
            }

            return count;
        }

        /// <summary>
        /// Swaps each firearm's reserve-ammo source to the infinite provider.
        ///
        /// <para>Carlos's testing rule: "we would give infinite ammo, not infinite chamber. We would
        /// still be in need of reloading when we run out of bullets... but we would be able to
        /// reload with an infinite pool." That is exactly the split PolymindGames already draws.
        /// The <i>magazine</i> component holds the chamber and is left untouched, so reloads still
        /// happen normally; only the <i>provider</i> it draws from is swapped from
        /// <c>FirearmInventoryAmmoProvider</c> (which pulls rounds out of the item inventory) to
        /// <c>FirearmInfiniteAmmoProvider</c>. No ammo pickups are needed to play.</para>
        ///
        /// <para>This is a testing convenience, not a shipping decision - reverting it is a one-line
        /// change back to the inventory provider once MRM-26's pickups are live.</para>
        /// </summary>
        private static int MakeReserveAmmoInfinite(GameObject root, StringBuilder log)
        {
            var swapped = new List<GameObject>();
            foreach (var provider in root.GetComponentsInChildren<PolymindGames.WieldableSystem.FirearmInventoryAmmoProvider>(true))
            {
                swapped.Add(provider.gameObject);
            }

            foreach (GameObject go in swapped)
            {
                var old = go.GetComponent<PolymindGames.WieldableSystem.FirearmInventoryAmmoProvider>();
                if (old != null)
                {
                    Object.DestroyImmediate(old, true);
                }

                go.AddComponent<PolymindGames.WieldableSystem.FirearmInfiniteAmmoProvider>();
                log.AppendLine("  infinite reserve ammo: " + go.name);
            }

            return swapped.Count;
        }

        /// <summary>
        /// Adds Mr. Moonlight's own player systems. Everything that used to hang off the Burntwax
        /// bridges now hangs off <c>MoonlightPlayerRig</c>, which is the single seam between our code
        /// and the PolymindGames character.
        /// </summary>
        /// <summary>
        /// Gives the copied prefab its own save-system identity.
        ///
        /// <para><c>AssetDatabase.CopyAsset</c> duplicates the <c>SaveableObject.PrefabGuid</c> along
        /// with everything else, so Player_Tracey shipped with FPS_Player's GUID. PolymindGames keys
        /// its saveable-prefab lookup by that value, so two prefabs sharing one threw
        /// <c>ArgumentException: An item with the same key has already been added</c> on entering
        /// play mode. Every other saveable prefab in the project has PrefabGuid == its own asset
        /// GUID, so that is the convention followed here.</para>
        /// </summary>
        private static void FixSaveableGuid(GameObject root, StringBuilder log)
        {
            var saveable = root.GetComponent<PolymindGames.SaveSystem.SaveableObject>();
            if (saveable == null)
            {
                return;
            }

            string assetGuid = AssetDatabase.AssetPathToGUID(Target);
            if (string.IsNullOrEmpty(assetGuid))
            {
                return;
            }

            saveable.PrefabGuid = new System.Guid(assetGuid);
            log.AppendLine("  save-system PrefabGuid set to the prefab's own asset GUID");
        }

        private static void WireMoonlightSystems(GameObject root, StringBuilder log)
        {
            FixSaveableGuid(root, log);

            // The Player tag has to be on the object carrying the collider, not a parent: Blaze AI's
            // vision reads the collider's own tag, which silently broke every Spotter once before
            // (see Docs/mrm34-spotter-ai-build.md).
            root.tag = "Player";
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Hitbox")
                {
                    t.gameObject.tag = "Player";
                    log.AppendLine("  tagged Player: " + t.name);
                }
            }

            AddIfMissing<MrMoonlight.Player.MoonlightPlayerRig>(root, log);
            AddIfMissing<MrMoonlight.Player.MoonlightWeaponCycler>(root, log);
            AddIfMissing<MrMoonlight.Player.MoonlightStartingLoadout>(root, log);

            Transform systems = FindDeep(root.transform, "MrMoonlight Systems");
            if (systems == null)
            {
                var go = new GameObject("MrMoonlight Systems");
                go.transform.SetParent(root.transform, false);
                systems = go.transform;
                log.AppendLine("  created MrMoonlight Systems");
            }

            AddIfMissing<MrMoonlight.Player.PlayerStats>(systems.gameObject, log);
            AddIfMissing<MrMoonlight.Items.Inventory>(systems.gameObject, log);
        }

        private static void AddIfMissing<T>(GameObject go, StringBuilder log) where T : Component
        {
            if (go.GetComponent<T>() != null)
            {
                return;
            }

            go.AddComponent<T>();
            log.AppendLine("  added " + typeof(T).Name + " to " + go.name);
        }

        private static int RemoveOutOfScopeComponents(GameObject root, StringBuilder log)
        {
            var doomed = new List<Component>();
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c != null && DropComponents.Contains(c.GetType().Name))
                {
                    doomed.Add(c);
                }
            }

            foreach (Component c in doomed)
            {
                if (c == null)
                {
                    continue;
                }

                log.AppendLine("  removed component: " + c.GetType().Name + " on " + c.gameObject.name);
                Object.DestroyImmediate(c, true);
            }

            return doomed.Count;
        }

        private static int RebindInput(GameObject root, StringBuilder log)
        {
            Dictionary<string, InputActionReference> ours = LoadOurReferences();
            if (ours.Count == 0)
            {
                log.AppendLine("  WARNING: no InputActionReferences found in " + OurActions);
                return 0;
            }

            int count = 0;
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null)
                {
                    continue;
                }

                var so = new SerializedObject(c);
                SerializedProperty it = so.GetIterator();
                bool changed = false;
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    var current = it.objectReferenceValue as InputActionReference;
                    if (current == null || current.action == null)
                    {
                        continue;
                    }

                    string vendorName = current.action.name;
                    string mine;
                    if (!ActionMap.TryGetValue(vendorName, out mine))
                    {
                        mine = UnboundAction;
                        log.AppendLine("  sank unused vendor action " + vendorName + " on "
                                       + c.GetType().Name + " (" + c.gameObject.name + ")");
                    }

                    InputActionReference replacement;
                    if (!ours.TryGetValue(mine, out replacement))
                    {
                        log.AppendLine("  MISSING our action " + mine);
                        continue;
                    }

                    it.objectReferenceValue = replacement;
                    changed = true;
                    count++;
                }

                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            return count;
        }

        /// <summary>Our action asset carries duplicate InputActionReference sub-assets - Unity
        /// leaves orphans behind when actions are renamed. Keyed by action name, first wins:
        /// they all resolve to the same action, so any of them binds correctly.</summary>
        private static Dictionary<string, InputActionReference> LoadOurReferences()
        {
            var map = new Dictionary<string, InputActionReference>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(OurActions))
            {
                var r = o as InputActionReference;
                if (r == null || r.action == null || r.action.actionMap == null)
                {
                    continue;
                }

                if (r.action.actionMap.name != "Gameplay")
                {
                    continue;
                }

                if (!map.ContainsKey(r.action.name))
                {
                    map.Add(r.action.name, r);
                }
            }

            return map;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
