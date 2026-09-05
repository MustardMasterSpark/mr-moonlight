using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.EditorTools.Migration
{
    /// <summary>
    /// Builds Mr. Moonlight's weapon library and upgrades <c>Player_Tracey</c> to carry it (MRM-25).
    ///
    /// <para>Two menu items, run in order:</para>
    /// <list type="number">
    /// <item><b>Build Weapon Library</b> — makes one <b>prefab variant</b> per weapon under
    /// <c>Assets/_Project/Prefabs/Weapons/&lt;Category&gt;/</c>, with Mr. Moonlight's own damage
    /// numbers and infinite reserve ammo applied as variant overrides.</item>
    /// <item><b>Upgrade Player_Tracey</b> — points the player's wieldable list at those variants,
    /// restores body lean, adds MRM-25's components and grows the holster.</item>
    /// </list>
    ///
    /// <para><b>Why variants, and why in <c>_Project</c>.</b> Carlos, 2026-09-04: <i>"create prefabs
    /// for these weapons and store everything needed ... in an orderly fashion within the project as
    /// best as you see fit."</i> The vendor's twenty-one wieldables sit in <c>Assets/ThirdParty/</c>,
    /// which is <b>entirely git-ignored</b> by project policy — so before this, every weapon override
    /// the project made was untracked. A variant per weapon puts our changes (damage, infinite ammo,
    /// the crossbow's descope) in version control, keeps them visibly separate from vendor data, and
    /// still inherits vendor fixes automatically. Raw meshes, textures and audio deliberately stay in
    /// ThirdParty — that is where <c>Docs/unity-conventions.md</c> says binaries go, and moving them
    /// would break the GUID links that make the whole vendor prefab set resolve.</para>
    ///
    /// <para><b>Why this exists instead of just re-running <see cref="PolymindPlayerBuild"/>.</b>
    /// That tool deletes and recreates the prefab from the vendor source, which breaks the link from
    /// the scene instance. As of 2026-09-04 the <c>Island.unity</c> instance carries <b>nine added
    /// components and two added GameObjects</b> that exist nowhere in the prefab — DeathSequence with
    /// its death-scream pool, black-screen image and game-over panel; four debug overlays with their
    /// font; PauseController; the damage-numbers overlay camera. Rebuilding would drop all of it, and
    /// the resulting nulls fail silently at runtime. That is the exact failure mode
    /// <c>Docs/mrm9-hqfps-integration.md</c> §9b records from the last swap. So this upgrades the
    /// existing prefab in place and never touches the instance.</para>
    /// </summary>
    public static class MoonlightWeaponSetBuild
    {
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Player/Player_Tracey.prefab";
        private const string OurActions = "Assets/InputSystem_Actions.inputactions";

        /// <summary>Holster slots. Fourteen items plus headroom — see
        /// <see cref="PolymindPlayerBuild"/> for why this is deliberately generous.</summary>
        private const int HolsterSlots = 16;

        #region Weapon library

        [MenuItem("Tools/MrMoonlight/Weapons/1. Build Weapon Library (MRM-25)")]
        public static void BuildLibrary()
        {
            var log = new StringBuilder();
            EnsureFolder(MoonlightWeaponSet.VariantRoot);

            var firearmDamage = MoonlightWeaponSet.FirearmDamage();
            var meleeDamage = MoonlightWeaponSet.MeleeDamage();
            if (firearmDamage.Count == 0)
            {
                Debug.LogError("[MRM-25] MoonlightTunables not found — aborting rather than baking vendor damage in silently.");
                return;
            }

            int built = 0;
            foreach (MoonlightWeaponSet.Entry entry in MoonlightWeaponSet.All)
            {
                if (entry.Group == MoonlightWeaponSet.Category.Hands)
                {
                    // The arms are not a weapon and carry no Mr. Moonlight overrides — the player
                    // keeps instancing the vendor prefab directly. A variant would be pure noise.
                    continue;
                }

                var source = AssetDatabase.LoadAssetAtPath<GameObject>(MoonlightWeaponSet.VendorPath(entry));
                if (source == null)
                {
                    log.AppendLine("  MISSING vendor prefab: " + MoonlightWeaponSet.VendorPath(entry));
                    continue;
                }

                EnsureFolder(MoonlightWeaponSet.VariantRoot + "/" + entry.Group);
                string path = MoonlightWeaponSet.VariantPath(entry);

                // Instantiating the base and saving the instance is what makes Unity author a
                // VARIANT rather than a flat copy — a copy would fork from the vendor permanently.
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
                    ApplyOverrides(instance, entry, firearmDamage, meleeDamage, log);
                    PrefabUtility.SaveAsPrefabAsset(instance, path);
                    built++;
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }

                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (saved != null && PrefabUtility.GetPrefabAssetType(saved) != PrefabAssetType.Variant)
                {
                    log.AppendLine("  WARNING: " + path + " saved as a "
                                   + PrefabUtility.GetPrefabAssetType(saved)
                                   + ", not a Variant — it will not inherit vendor fixes.");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MRM-25] Weapon library built: " + built + " prefab variants under "
                      + MoonlightWeaponSet.VariantRoot + "\n" + log);
        }

        /// <summary>
        /// Applies everything Mr. Moonlight changes about a vendor weapon, as variant overrides.
        /// </summary>
        private static void ApplyOverrides(
            GameObject instance,
            MoonlightWeaponSet.Entry entry,
            Dictionary<string, float> firearmDamage,
            Dictionary<string, Vector2> meleeDamage,
            StringBuilder log)
        {
            log.AppendLine("[" + entry.ShortName + "]");

            // --- damage ---------------------------------------------------------------
            float damage;
            if (firearmDamage.TryGetValue(entry.ShortName, out damage))
            {
                int n = SetOnAll(instance, "FirearmStandardImpactEffect", "_damage",
                    p => p.floatValue = damage);
                log.AppendLine("  damage " + damage + " on " + n + " impact effect(s)");
            }

            Vector2 range;
            if (meleeDamage.TryGetValue(entry.ShortName, out range))
            {
                int n = SetOnAll(instance, "BasicMeleeAttack", "_hitDamageRange",
                    p => p.vector2Value = range);
                log.AppendLine("  damage " + range.x + ".." + range.y + " on " + n + " swing(s)");
            }

            // --- infinite reserve ammo ------------------------------------------------
            // Carlos: "infinite ammo, not infinite chamber. We would still be in need of reloading."
            // The MAGAZINE is untouched, so reloads happen normally and the magazine size still
            // limits a burst; only the reserve the reload draws from becomes bottomless.
            var providers = new List<GameObject>();
            foreach (var p in instance.GetComponentsInChildren<PolymindGames.WieldableSystem.FirearmInventoryAmmoProvider>(true))
            {
                providers.Add(p.gameObject);
            }

            foreach (GameObject go in providers)
            {
                var old = go.GetComponent<PolymindGames.WieldableSystem.FirearmInventoryAmmoProvider>();
                if (old != null)
                {
                    Object.DestroyImmediate(old, true);
                }

                go.AddComponent<PolymindGames.WieldableSystem.FirearmInfiniteAmmoProvider>();
                log.AppendLine("  infinite reserve ammo");
            }

            // --- the crossbow keeps no scope -----------------------------------------
            // Carlos ruled "the crossbow without scope" on MRM-9. A prefab VARIANT cannot delete a
            // GameObject it inherits, only deactivate it — so this is a behaviour change from
            // MRM-9's outright deletion, with the same visible result. Nothing is lost: the
            // attachment system already picks the iron sight, because the Crossbow item's "Sight
            // Attachment" property is 0. The scope mesh ships regardless now, for the Hunting Rifle.
            if (entry.ShortName == "Crossbow")
            {
                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Contains("SniperScope"))
                    {
                        t.gameObject.SetActive(false);
                        log.AppendLine("  scope deactivated (Carlos: crossbow without scope)");
                        break;
                    }
                }
            }
        }

        private static int SetOnAll(GameObject go, string componentType, string field, System.Action<SerializedProperty> set)
        {
            int count = 0;
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.GetType().Name != componentType)
                {
                    continue;
                }

                var so = new SerializedObject(c);
                SerializedProperty p = so.FindProperty(field);
                if (p == null)
                {
                    continue;
                }

                set(p);
                so.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }

            return count;
        }

        #endregion

        #region Player upgrade

        [MenuItem("Tools/MrMoonlight/Weapons/2. Upgrade Player_Tracey to the weapon library (MRM-25)")]
        public static void UpgradePlayer()
        {
            var log = new StringBuilder();
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            if (root == null)
            {
                Debug.LogError("[MRM-25] Could not open " + PlayerPrefab);
                return;
            }

            try
            {
                Transform holder = FindDeep(root.transform, "Root");
                if (holder == null)
                {
                    Debug.LogError("[MRM-25] Wieldable holder 'Root' not found on the player.");
                    return;
                }

                int swapped = SwapInVariants(holder, log);
                int lean = RestoreBodyLean(root, log);
                int rebound = RebindNewActions(root, log);
                int slots = ResizeHolster(root, log);
                RestoreHealingHandler(root, log);
                AddScopeOverlayUI(root, log);
                AddMoonlightComponents(root, log);
                ConfigureLoadout(root, log);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                Debug.Log("[MRM-25] Player_Tracey upgraded: " + swapped + " wieldables now on library "
                          + "variants, " + lean + " lean components restored, " + rebound
                          + " input references rebound, holster at " + slots + " slots.\n" + log);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Replaces the player's wieldable children with instances of the library variants, adding
        /// the ones that were never there.
        /// </summary>
        private static int SwapInVariants(Transform holder, StringBuilder log)
        {
            // Index what is already present, by GameObject name.
            var existing = new Dictionary<string, Transform>();
            for (int i = holder.childCount - 1; i >= 0; i--)
            {
                existing[holder.GetChild(i).name] = holder.GetChild(i);
            }

            int count = 0;
            foreach (MoonlightWeaponSet.Entry entry in MoonlightWeaponSet.All)
            {
                if (entry.Group == MoonlightWeaponSet.Category.Hands)
                {
                    continue;   // vendor prefab, already present, nothing to swap
                }

                string variantPath = MoonlightWeaponSet.VariantPath(entry);
                var variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
                if (variant == null)
                {
                    log.AppendLine("  MISSING variant, run step 1 first: " + variantPath);
                    continue;
                }

                string variantName = System.IO.Path.GetFileNameWithoutExtension(variantPath);

                // Drop the old vendor instance if one is there. Nothing is lost: a check of the
                // 2026-09-04 prefab found zero property overrides on any wieldable child, and every
                // Mr. Moonlight change now lives in the variant instead.
                Transform old;
                if (existing.TryGetValue(entry.VendorName, out old))
                {
                    Object.DestroyImmediate(old.gameObject);
                    log.AppendLine("  replaced vendor instance: " + entry.VendorName);
                }

                if (existing.ContainsKey(variantName))
                {
                    continue;   // already on the variant
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(variant, holder);
                instance.name = variantName;
                instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                count++;
                log.AppendLine("  + " + variantName + "  (" + entry.Group + ")");
            }

            return count;
        }

        /// <summary>
        /// Puts body lean back on the player (MRM-25).
        ///
        /// <para>MRM-9 deleted <c>BodyLeanHandler</c> and <c>FPSBodyLeanInput</c> because lean was
        /// not a demo mechanic. Carlos made it one: <i>"now we will use the keys Q and E ... bring
        /// that behavior and function to MrMoonlight. This behavior will now be part of the default
        /// player controller."</i></para>
        ///
        /// <para>Copied from the vendor <c>FPS_Player</c> with
        /// <c>ComponentUtility.CopyComponent</c>/<c>PasteComponentAsNew</c> rather than
        /// <c>AddComponent</c>, because <c>BodyLeanHandler</c> has two <c>[NotNull]</c>
        /// <c>LeanMotion</c> references plus an obstruction mask, cooldown and audio clip that
        /// AddComponent would leave at defaults. The pasted references then have to be re-pointed at
        /// <i>this</i> player's LeanMotions — a paste carries the source prefab's objects, which
        /// would silently lean the wrong hierarchy.</para>
        /// </summary>
        private static int RestoreBodyLean(GameObject root, StringBuilder log)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ThirdParty/PolymindGames/FPSCore/Prefabs/Core/FPS_Player.prefab");
            if (source == null)
            {
                log.AppendLine("  WARNING: FPS_Player not found, lean not restored");
                return 0;
            }

            int restored = 0;

            var sourceHandler = source.GetComponentInChildren<PolymindGames.BodyLeanHandler>(true);
            if (sourceHandler != null && root.GetComponentInChildren<PolymindGames.BodyLeanHandler>(true) == null)
            {
                Transform target = FindDeep(root.transform, sourceHandler.gameObject.name);
                if (target == null)
                {
                    log.AppendLine("  WARNING: no '" + sourceHandler.gameObject.name + "' on the player, lean handler not restored");
                }
                else
                {
                    UnityEditorInternal.ComponentUtility.CopyComponent(sourceHandler);
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(target.gameObject);
                    var pasted = target.GetComponent<PolymindGames.BodyLeanHandler>();
                    RepointLeanMotions(root, pasted, log);
                    restored++;
                    log.AppendLine("  restored BodyLeanHandler on " + target.name);
                }
            }

            var sourceInput = source.GetComponentInChildren<PolymindGames.InputSystem.Behaviours.FPSBodyLeanInput>(true);
            if (sourceInput != null && root.GetComponentInChildren<PolymindGames.InputSystem.Behaviours.FPSBodyLeanInput>(true) == null)
            {
                Transform target = FindDeep(root.transform, sourceInput.gameObject.name);
                if (target == null)
                {
                    log.AppendLine("  WARNING: no '" + sourceInput.gameObject.name + "' on the player, lean input not restored");
                }
                else
                {
                    UnityEditorInternal.ComponentUtility.CopyComponent(sourceInput);
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(target.gameObject);
                    restored++;
                    log.AppendLine("  restored FPSBodyLeanInput on " + target.name);
                }
            }

            return restored;
        }

        /// <summary>
        /// Points a freshly pasted <c>BodyLeanHandler</c> at this player's own LeanMotion
        /// components. Matches by GameObject name, which is what the vendor hierarchy uses:
        /// <c>Body/Head</c> leans the body, <c>Body/Head/Camera/Wieldables</c> leans the weapon.
        /// </summary>
        private static void RepointLeanMotions(GameObject root, PolymindGames.BodyLeanHandler handler, StringBuilder log)
        {
            if (handler == null)
            {
                return;
            }

            var so = new SerializedObject(handler);
            string[] fields = { "_bodyLeanMotion", "_wieldableLeanMotion" };
            foreach (string field in fields)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null || p.objectReferenceValue == null)
                {
                    log.AppendLine("  WARNING: " + field + " is null on the pasted lean handler");
                    continue;
                }

                string wanted = p.objectReferenceValue.name;
                Transform t = FindDeep(root.transform, wanted);
                var motion = t != null ? t.GetComponent<PolymindGames.ProceduralMotion.LeanMotion>() : null;
                if (motion == null)
                {
                    log.AppendLine("  WARNING: no LeanMotion named '" + wanted + "' on the player for " + field);
                    continue;
                }

                p.objectReferenceValue = motion;
                log.AppendLine("  " + field + " -> " + wanted);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Points the Lean and Heal input behaviours at our own actions.
        ///
        /// <para>MRM-9 sank every vendor action it had no equivalent for into a bindingless
        /// <c>Unbound</c> action. Both of these now have real bindings — Lean on Q/E and Heal on H —
        /// so they get repointed. Everything else stays sunk.</para>
        /// </summary>
        private static int RebindNewActions(GameObject root, StringBuilder log)
        {
            var ours = new Dictionary<string, InputActionReference>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(OurActions))
            {
                var r = o as InputActionReference;
                if (r == null || r.action == null || r.action.actionMap == null)
                {
                    continue;
                }

                if (r.action.actionMap.name == "Gameplay" && !ours.ContainsKey(r.action.name))
                {
                    ours.Add(r.action.name, r);
                }
            }

            // Every InputActionReference sub-asset of OUR asset, by identity. Matching on the
            // action's NAME is not enough and was an actual bug here: the vendor's own asset also
            // contains an action called "Lean", so a freshly pasted FPSBodyLeanInput looks correct
            // by name while still pointing at the vendor asset — which MRM-9 went to some trouble to
            // leave completely unreferenced.
            var oursByIdentity = new HashSet<Object>();
            foreach (InputActionReference r in ours.Values)
            {
                oursByIdentity.Add(r);
            }

            string[] wanted = { "Lean", "Heal" };
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
                    if (current == null)
                    {
                        continue;
                    }

                    // Which of our actions does this field want? The field name is the only signal
                    // left: a component MRM-9 sank into Unbound has lost the vendor action's
                    // identity entirely.
                    string target = null;
                    foreach (string name in wanted)
                    {
                        if (it.propertyPath.ToLower().Contains(name.ToLower()))
                        {
                            target = name;
                            break;
                        }
                    }

                    if (target == null)
                    {
                        continue;
                    }

                    InputActionReference replacement;
                    if (!ours.TryGetValue(target, out replacement))
                    {
                        log.AppendLine("  MISSING our action " + target);
                        continue;
                    }

                    if (current == replacement)
                    {
                        continue;   // already correct
                    }

                    string was = oursByIdentity.Contains(current)
                        ? "ours/" + (current.action != null ? current.action.name : "?")
                        : "VENDOR ASSET";
                    it.objectReferenceValue = replacement;
                    changed = true;
                    count++;
                    log.AppendLine("  " + c.GetType().Name + "." + it.propertyPath
                                   + ": " + was + " -> ours/" + target);
                }

                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            return count;
        }

        private static int ResizeHolster(GameObject root, StringBuilder log)
        {
            var inventory = root.GetComponentInChildren<PolymindGames.InventorySystem.Inventory>(true);
            if (inventory == null)
            {
                log.AppendLine("  WARNING: no Inventory component, holster not resized");
                return 0;
            }

            var so = new SerializedObject(inventory);
            SerializedProperty containers = so.FindProperty("_defaultContainers");
            if (containers == null)
            {
                return 0;
            }

            int applied = 0;
            for (int i = 0; i < containers.arraySize; i++)
            {
                SerializedProperty container = containers.GetArrayElementAtIndex(i);
                SerializedProperty name = container.FindPropertyRelative("Name");
                if (name == null || name.stringValue != "Holster")
                {
                    continue;
                }

                SerializedProperty count = container.FindPropertyRelative("MaxSlotCount");
                if (count == null)
                {
                    continue;
                }

                if (count.intValue < HolsterSlots)
                {
                    log.AppendLine("  holster slots " + count.intValue + " -> " + HolsterSlots);
                    count.intValue = HolsterSlots;
                }

                applied = count.intValue;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return applied;
        }

        /// <summary>
        /// Adds the component that actually makes the Syringe do something (MRM-25).
        ///
        /// <para>Carlos asked what the Syringe is and how to use it. It is a fully functional
        /// <c>HealingWieldable</c>, not a prop — but it has no input of its own. Its
        /// <c>Use()</c> only <i>cancels</i> a heal in progress; the single call site that ever
        /// <i>starts</i> one is <c>WieldableHealingHandler.TryHeal()</c>, which
        /// <c>FPSWieldablesInput</c> raises from the Heal action.</para>
        ///
        /// <para>That handler is <b>not on the vendor's own FPS_Player either</b>, which is why the
        /// Syringe appears inert in the Weapons project too — this is not something MRM-9 dropped.
        /// So it is added here rather than copied.</para>
        ///
        /// <para><c>_containerTags</c> is set to the <b>Wieldable</b> tag: a container's tags come
        /// from its <c>TagContainerRestriction</c>, and the Holster's
        /// <c>FPS_Restriction_Wieldable</c> is what carries that tag. This is the same route
        /// <c>WieldableInventory</c> uses to find the holster, so the two agree by construction.</para>
        ///
        /// <para>Note the handler equips the Syringe itself and holsters it again when the heal
        /// finishes, so H works from any weapon — the player never selects the Syringe manually,
        /// which is why it is on no number key.</para>
        /// </summary>
        private static void RestoreHealingHandler(GameObject root, StringBuilder log)
        {
            if (root.GetComponentInChildren<PolymindGames.WieldableSystem.WieldableHealingHandler>(true) != null)
            {
                return;
            }

            // Must sit with the wieldable inventory/controller it declares as required components.
            var inventory = root.GetComponentInChildren<PolymindGames.WieldableSystem.WieldableInventory>(true);
            if (inventory == null)
            {
                log.AppendLine("  WARNING: no WieldableInventory, healing handler not added (Heal key will do nothing)");
                return;
            }

            var handler = inventory.gameObject.AddComponent<PolymindGames.WieldableSystem.WieldableHealingHandler>();

            const string WieldableTagName = "Wieldable";
            var tags = Resources.LoadAll<PolymindGames.InventorySystem.ItemTagDefinition>("Definitions/ItemTag");
            int tagId = 0;
            foreach (var tag in tags)
            {
                if (tag != null && tag.Name == WieldableTagName)
                {
                    tagId = tag.Id;
                    break;
                }
            }

            if (tagId == 0)
            {
                log.AppendLine("  WARNING: no '" + WieldableTagName + "' item tag found; healing handler will search no containers");
            }

            var so = new SerializedObject(handler);
            SerializedProperty tagsProp = so.FindProperty("_containerTags");
            if (tagsProp != null)
            {
                tagsProp.arraySize = 1;
                SerializedProperty entry = tagsProp.GetArrayElementAtIndex(0);
                SerializedProperty value = entry.FindPropertyRelative("_value");
                if (value != null)
                {
                    value.intValue = tagId;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("  added WieldableHealingHandler on " + inventory.gameObject.name
                           + " (container tag '" + WieldableTagName + "' = " + tagId + ")");
        }

        /// <summary>
        /// Writes the arsenal into <c>MoonlightStartingLoadout</c>'s serialized lists, and the
        /// category map into <c>MoonlightWeaponCategorySwitcher</c>'s.
        ///
        /// <para><b>This is not redundant with the C# field initializers, and finding that out cost
        /// a play-mode run.</b> A field initializer only supplies the value for a <i>newly created</i>
        /// component. <c>MoonlightStartingLoadout</c> has been on this prefab since MRM-9 with the
        /// old four-weapon list serialized into it, and editing the initializer left that stored
        /// list completely untouched — the player still spawned with a shotgun, pistol, crossbow and
        /// bat. Anything that must actually change on an existing prefab has to be written through
        /// <see cref="SerializedObject"/>, which is what this does.</para>
        ///
        /// <para>Both components are driven from <see cref="MoonlightWeaponSet"/>, so the loadout
        /// and the number keys cannot disagree about what exists.</para>
        /// </summary>
        private static void ConfigureLoadout(GameObject root, StringBuilder log)
        {
            var loadout = root.GetComponent<MrMoonlight.Player.MoonlightStartingLoadout>();
            if (loadout == null)
            {
                log.AppendLine("  WARNING: no MoonlightStartingLoadout, arsenal not written");
                return;
            }

            // Holster order: grouped by category so the gamepad's linear cycle also walks category
            // by category. Slot 0 is the Combat Knife — Carlos: "From the start the player will
            // always spawn with the combat knife."
            var ordered = new List<MoonlightWeaponSet.Entry>();
            foreach (MoonlightWeaponSet.Category group in System.Enum.GetValues(typeof(MoonlightWeaponSet.Category)))
            {
                if (group == MoonlightWeaponSet.Category.Hands)
                {
                    continue;   // not an inventory item
                }

                var inGroup = new List<MoonlightWeaponSet.Entry>();
                foreach (MoonlightWeaponSet.Entry e in MoonlightWeaponSet.All)
                {
                    if (e.Group == group && !string.IsNullOrEmpty(e.ItemName))
                    {
                        inGroup.Add(e);
                    }
                }

                inGroup.Sort((a, b) => a.Order.CompareTo(b.Order));
                ordered.AddRange(inGroup);
            }

            var so = new SerializedObject(loadout);
            WriteStringList(so, "weapons", ordered.ConvertAll(e => e.ItemName));

            // Throwables are consumed per throw and need a stack to draw from; the Syringe is a
            // consumable too. See MoonlightInfiniteThrowables for why the stack is kept high.
            int stock = MrMoonlight.Data.Tunables.I != null ? MrMoonlight.Data.Tunables.I.WeaponThrowableTestStock : 99;
            SerializedProperty stacked = so.FindProperty("stackedItems");
            if (stacked != null)
            {
                stacked.arraySize = 3;
                SetStacked(stacked, 0, "Frag Grenade", stock);
                SetStacked(stacked, 1, "Molotov Cocktail", stock);
                SetStacked(stacked, 2, "Syringe", 5);
            }

            SerializedProperty startingSlot = so.FindProperty("startingSlot");
            if (startingSlot != null)
            {
                startingSlot.intValue = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("  loadout written: " + ordered.Count + " items, starting slot 0 ("
                           + (ordered.Count > 0 ? ordered[0].ItemName : "?") + ")");

            // --- the category map -------------------------------------------------------
            var switcher = root.GetComponent<MrMoonlight.Player.MoonlightWeaponCategorySwitcher>();
            if (switcher == null)
            {
                return;
            }

            var switcherSo = new SerializedObject(switcher);
            SerializedProperty categories = switcherSo.FindProperty("categories");
            if (categories == null)
            {
                return;
            }

            var keyed = new List<MoonlightWeaponSet.Category>
            {
                MoonlightWeaponSet.Category.Melee,
                MoonlightWeaponSet.Category.Pistol,
                MoonlightWeaponSet.Category.Shotgun,
                MoonlightWeaponSet.Category.Rifle,
                MoonlightWeaponSet.Category.Precision,
                MoonlightWeaponSet.Category.Throwable,
            };

            categories.arraySize = keyed.Count;
            for (int i = 0; i < keyed.Count; i++)
            {
                SerializedProperty entry = categories.GetArrayElementAtIndex(i);
                SerializedProperty key = entry.FindPropertyRelative("Key");
                if (key != null)
                {
                    key.enumValueIndex = (int)keyed[i];
                }

                var names = new List<string>();
                foreach (MoonlightWeaponSet.Entry e in ordered)
                {
                    if (e.Group == keyed[i])
                    {
                        names.Add(e.ItemName);
                    }
                }

                SerializedProperty list = entry.FindPropertyRelative("Weapons");
                if (list != null)
                {
                    list.arraySize = names.Count;
                    for (int j = 0; j < names.Count; j++)
                    {
                        list.GetArrayElementAtIndex(j).stringValue = names[j];
                    }
                }

                log.AppendLine("  key " + keyed[i] + ": " + string.Join(", ", names.ToArray()));
            }

            switcherSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStacked(SerializedProperty array, int index, string name, int count)
        {
            SerializedProperty e = array.GetArrayElementAtIndex(index);
            SerializedProperty n = e.FindPropertyRelative("Name");
            SerializedProperty c = e.FindPropertyRelative("Count");
            if (n != null)
            {
                n.stringValue = name;
            }

            if (c != null)
            {
                c.intValue = count;
            }
        }

        private static void WriteStringList(SerializedObject so, string field, List<string> values)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                return;
            }

            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                p.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        /// <summary>
        /// Gives the player the scoped-aim overlay — the lens reticle and the black surround that
        /// masks everything outside it (MRM-25).
        ///
        /// <para>Carlos, 2026-09-04: <i>"When I aim to use the scope on the Hunting Rifle I didn't
        /// see the image of the scope lines, nor the black effect that only leaves us with a circle
        /// in the middle."</i> The scope was working — <c>FirearmScopedAimHandler</c> was zooming
        /// correctly — but the overlay is a <b>UI</b> element, and MRM-9 migrated none of the vendor
        /// UI. The whole thing lives in <c>FPS_UI_Wieldables.prefab</c>, which was never put in the
        /// scene.</para>
        ///
        /// <para><b>Parented to the player, not to the HUD Canvas.</b> <c>ScopeControllerUI</c> is a
        /// <c>CharacterUIBehaviour</c>: its <c>Awake</c> looks for an <c>ICharacterUI</c> <i>in a
        /// parent</i> and logs an error and gives up if there is none. Hanging the canvas off the
        /// player and setting <c>CharacterUI</c> to attach to the parent character makes that
        /// resolve with no scene wiring at all — nothing to re-point if the player is ever
        /// re-instanced, which is exactly the class of silent breakage
        /// <c>Docs/mrm9-hqfps-integration.md</c> §9b is a catalogue of.</para>
        ///
        /// <para>Sorting order is <b>-1</b>, deliberately below the HUD Canvas at 0: the scope
        /// surround is full-screen black, and at a higher order it would hide the damage tint,
        /// system messages and the game-over panel.</para>
        ///
        /// <para>Only the Scope branch is taken. The same vendor prefab also carries an ammo
        /// counter, fire-mode indicator, crosshair set, hitmarker and reload bar — all still
        /// unmigrated (<c>Docs/mrm9-hqfps-integration.md</c> §10 item 4). They are one edit away
        /// from here if wanted, but they are not what was asked for.</para>
        /// </summary>
        private static void AddScopeOverlayUI(GameObject root, StringBuilder log)
        {
            const string HolderName = "MRM_UI_Scope";
            if (FindDeep(root.transform, HolderName) != null)
            {
                return;
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ThirdParty/PolymindGames/FPSCore/Prefabs/UI/Wieldables/FPS_UI_Wieldables.prefab");
            if (source == null)
            {
                log.AppendLine("  WARNING: FPS_UI_Wieldables not found, scope overlay not added");
                return;
            }

            Transform sourceScope = FindDeep(source.transform, "Scope");
            if (sourceScope == null)
            {
                log.AppendLine("  WARNING: no 'Scope' branch in FPS_UI_Wieldables, overlay not added");
                return;
            }

            var holder = new GameObject(HolderName, typeof(RectTransform));
            holder.transform.SetParent(root.transform, false);
            holder.layer = LayerMask.NameToLayer("UI");

            var canvas = holder.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1;

            var scaler = holder.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // The project's display target, per CLAUDE.md.
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var characterUI = holder.AddComponent<PolymindGames.UserInterface.CharacterUI>();
            var so = new SerializedObject(characterUI);
            SerializedProperty mode = so.FindProperty("_attachMode");
            if (mode != null)
            {
                // 1 = ToParentCharacter. The enum is private, so this is set by index; if the vendor
                // ever reorders it the scope will silently stop attaching, hence the readback below.
                mode.enumValueIndex = 1;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject scope = Object.Instantiate(sourceScope.gameObject, holder.transform, false);
            scope.name = "Scope";

            log.AppendLine("  added scope overlay UI under " + HolderName
                           + " (canvas order -1, attach mode "
                           + (mode != null ? mode.enumNames[mode.enumValueIndex] : "?") + ")");
        }

        private static void AddMoonlightComponents(GameObject root, StringBuilder log)
        {
            if (root.GetComponent<MrMoonlight.Player.MoonlightWeaponCategorySwitcher>() == null)
            {
                root.AddComponent<MrMoonlight.Player.MoonlightWeaponCategorySwitcher>();
                log.AppendLine("  added MoonlightWeaponCategorySwitcher");
            }

            if (root.GetComponent<MrMoonlight.Player.MoonlightInfiniteThrowables>() == null)
            {
                root.AddComponent<MrMoonlight.Player.MoonlightInfiniteThrowables>();
                log.AppendLine("  added MoonlightInfiniteThrowables");
            }
        }

        #endregion

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int cut = path.LastIndexOf('/');
            string parent = path.Substring(0, cut);
            string leaf = path.Substring(cut + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
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
