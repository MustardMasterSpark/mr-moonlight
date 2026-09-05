using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools.Migration
{
    /// <summary>
    /// Builds the <c>AudioRandomContainer</c> assets for MRM-25's new weapons and wires them onto
    /// the weapon library variants.
    ///
    /// <para><b>Why this is needed at all.</b> MRM-9 retyped <c>_equipAudio</c>,
    /// <c>_holsterAudio</c>, <c>_reloadAudio</c>, <c>_emptyReloadAudio</c> and <c>_ejectAudio</c>
    /// from the vendor's <c>AudioSequence</c> to <c>AudioData</c> so they could hold an
    /// <c>AudioRandomContainer</c> (see Docs/weapon-audio-system.md §3). Retyping a serialized field
    /// discards whatever the old one held, so every weapon that was not hand-re-authored at the time
    /// came across mute in those slots. Worse, <c>FirearmBasicBarrelEffect._fireAudio</c> is empty on
    /// five of the new firearms — the Revolver, Hunting Rifle, AKM, M1A and R870 make no sound at all
    /// when fired until this runs.</para>
    ///
    /// <para><b>The standing rule</b> (Carlos, Docs/weapon-audio-system.md): every weapon sound slot
    /// uses Unity's native <c>AudioRandomContainer</c>, never the vendor's <c>AudioSequence</c>.
    /// Containers are created with the same randomisation the existing ones use — ±75 cents pitch,
    /// -5..0 dB volume, Shuffle playback — so a repeated shot never sounds identical twice.</para>
    ///
    /// <para><b>The creation trap</b>, already paid for once and recorded here so it is not
    /// rediscovered: <c>AudioRandomContainer</c>'s and <c>AudioContainerElement</c>'s constructors
    /// are <c>internal</c>. <c>ScriptableObject.CreateInstance</c> and
    /// <c>ObjectFactory.CreateInstance</c> both fail on them, and a raw
    /// <c>Activator.CreateInstance</c> produces an asset that throws <i>"missing the class attribute
    /// 'ExtensionOfNativeClass'"</i> on load. The only path that yields a valid asset is Unity's own
    /// creation flow — <c>DoCreateAudioRandomContainer.CreateAudioRandomContainerFromSelectedClips</c>,
    /// which is what "select clips → Create → Audio Random Container" runs. This reaches it by
    /// reflection.</para>
    ///
    /// <para>Re-runnable. An existing container is left alone rather than rebuilt, so clips Carlos
    /// adds by hand later survive.</para>
    /// </summary>
    public static class MoonlightWeaponAudioBuild
    {
        private const string ContainerFolder = "Assets/_Project/Data/PolymindGames/Audio/Wieldables";
        private const string ClipFolder = "Assets/ThirdParty/PolymindGames/HQFPS/Audio/SFX/Wieldables";

        /// <summary>Matches every container built in the 2026-09-04 session.</summary>
        private static readonly Vector2 PitchRange = new Vector2(-75f, 75f);
        private static readonly Vector2 VolumeRange = new Vector2(-5f, 0f);

        /// <summary>
        /// One sound slot: which container to build, from which clips, and where it gets assigned.
        /// </summary>
        private sealed class Slot
        {
            public string Container;             // asset name, without AudioRC_ or .asset
            public string[] Clips;               // clip asset names, without .wav
            public string WeaponVariant;         // MRM_Weapon_* / MRM_Item_* to wire it onto
            public string ComponentType;         // component holding the field
            public string Field;                 // AudioData field, e.g. "_fireAudio"
        }

        /// <summary>
        /// Shared handling foley, reused across weapons exactly as the existing four already do —
        /// <c>AudioRC_Equip_Foley5</c> and <c>AudioRC_Holster_Foley4</c> are shared by every weapon
        /// that has no distinctive sound of its own.
        /// </summary>
        private const string SharedEquip = "Equip_Foley5";
        private const string SharedHolster = "Holster_Foley4";

        private static List<Slot> BuildSlotTable()
        {
            var slots = new List<Slot>();

            // ---- Fire. The five genuinely silent guns. -------------------------------------
            Fire(slots, "Revolver", "MRM_Weapon_Revolver", "FirearmBasicBarrelEffect",
                "Revolvers/HQFPS_Revolver_Shoot1", "Revolvers/HQFPS_Revolver_Shoot2");
            Fire(slots, "HuntingRifle", "MRM_Weapon_HuntingRifle", "FirearmBasicBarrelEffect",
                "HuntingRifle/HQFPS_HuntingRifle_Shoot1", "HuntingRifle/HQFPS_HuntingRifle_Shoot2",
                "HuntingRifle/HQFPS_HuntingRifle_Shoot3", "HuntingRifle/HQFPS_HuntingRifle_Shoot4");
            Fire(slots, "AKM", "MRM_Weapon_AKM", "FirearmBasicBarrelEffect",
                "AKM/HQFPS_AKM_Shoot1", "AKM/HQFPS_AKM_Shoot2");
            Fire(slots, "M1A", "MRM_Weapon_M1A", "FirearmBasicBarrelEffect",
                "M1A/HQFPS_M1A_Shoot1");
            Fire(slots, "R870", "MRM_Weapon_R870", "FirearmBasicBarrelEffect",
                "R870/HQFPS_R870_Shoot1", "R870/HQFPS_R870_Shoot2");

            // The double barrel has been firing a FLARE GUN sound since 2026-09-04 because HQ FPS
            // ships no double-barrel fire clip (Docs/weapon-audio-system.md §5 left this open).
            // The R870 arriving with this issue is the first real shotgun report the project owns,
            // so the stand-in is replaced. Same 12-gauge, different action - a far closer match than
            // a flare gun.
            slots.Add(new Slot
            {
                Container = "Fire_DBShotgun",
                Clips = new[] { "R870/HQFPS_R870_Shoot1", "R870/HQFPS_R870_Shoot2" },
                WeaponVariant = "MRM_Weapon_DBShotgun",
                ComponentType = "FirearmBasicBarrelEffect",
                Field = "_fireAudio",
            });

            // ---- Fire tail. A second, longer report layered after the crack. ---------------
            Tail(slots, "AKM", "MRM_Weapon_AKM", "AKM/HQFPS_AKM_ShootTail1", "AKM/HQFPS_AKM_ShootTail2");
            Tail(slots, "R870", "MRM_Weapon_R870", "R870/HQFPS_R870_ShootTail1");

            // ---- Reload --------------------------------------------------------------------
            Reload(slots, "Revolver", "MRM_Weapon_Revolver", "FirearmBasicReloadableMagazine",
                "_reloadAudio", "Revolvers/HQFPS_Revolver_Reload");
            Reload(slots, "AKM", "MRM_Weapon_AKM", "FirearmAdvancedReloadableMagazine",
                "_reloadAudio", "AKM/HQFPS_AKM_TacticalReload");
            Reload(slots, "M1A", "MRM_Weapon_M1A", "FirearmAdvancedReloadableMagazine",
                "_reloadAudio", "M1A/HQFPS_M1A_TacticalReload");

            // ---- Empty reload --------------------------------------------------------------
            Reload(slots, "EmptyReload_AKM", "MRM_Weapon_AKM", "FirearmAdvancedReloadableMagazine",
                "_emptyReloadAudio", "AKM/HQFPS_AKM_EmptyReload");
            Reload(slots, "EmptyReload_M1A", "MRM_Weapon_M1A", "FirearmAdvancedReloadableMagazine",
                "_emptyReloadAudio", "M1A/HQFPS_M1A_EmptyReload");
            Reload(slots, "EmptyReload_R870", "MRM_Weapon_R870", "FirearmProgressiveReloadableMagazine",
                "_emptyReloadAudio", "R870/HQFPS_R870_EmptyReload");
            Reload(slots, "EmptyReload_HuntingRifle", "MRM_Weapon_HuntingRifle",
                "FirearmAdvancedProgressiveReloadableMagazine",
                "_emptyReloadAudio", "HuntingRifle/HQFPS_HuntingRifle_EmptyReload");

            // HQ FPS ships no empty-reload take for the Revolver - only the one reload clip. A
            // revolver being loaded from empty and being topped up are the same physical action, so
            // reusing it is honest rather than a placeholder. (The Crossbow is left genuinely silent
            // here: it has no empty-reload STATE at all, so the slot never fires.)
            Reload(slots, "EmptyReload_Revolver", "MRM_Weapon_Revolver", "FirearmBasicReloadableMagazine",
                "_emptyReloadAudio", "Revolvers/HQFPS_Revolver_Reload");

            // ---- Cylinder swap / cartridge -------------------------------------------------
            slots.Add(new Slot
            {
                Container = "SwapCartridge_Revolver",
                Clips = new[] { "Revolvers/HQFPS_Revolver_CyllinderSpin" },
                WeaponVariant = "MRM_Weapon_Revolver",
                ComponentType = "FirearmCartridgeSwapEjector",
                Field = "_swapCartridgeAudio",
            });

            // ---- Throwables ----------------------------------------------------------------
            slots.Add(new Slot
            {
                Container = "Throw_Molotov",
                Clips = new[] { "Throwables/HQFPS_Molotov_Throw" },
                WeaponVariant = "MRM_Weapon_Molotov",
                ComponentType = "MeleeThrowAttack",
                Field = "_throwAudio",
            });

            // ---- Equip / holster foley -----------------------------------------------------
            // The Combat Knife is the only new weapon with a dedicated equip clip; everything else
            // shares the generic handling foley, which is what the existing four already do.
            slots.Add(new Slot
            {
                Container = "Equip_CombatKnife",
                Clips = new[] { "CombatKnife/HQFPS_CombatKnife_Equip" },
                WeaponVariant = "MRM_Weapon_CombatKnife",
                ComponentType = "MeleeWeapon",
                Field = "_equipAudio",
            });

            Foley(slots, "MRM_Weapon_CombatKnife", "MeleeWeapon", holsterOnly: true);
            Foley(slots, "MRM_Weapon_FireAxe", "MeleeWeapon", holsterOnly: false);
            Foley(slots, "MRM_Weapon_Revolver", "Firearm", holsterOnly: false);
            Foley(slots, "MRM_Weapon_R870", "Firearm", holsterOnly: false);
            Foley(slots, "MRM_Weapon_M1A", "Firearm", holsterOnly: false);
            Foley(slots, "MRM_Weapon_AKM", "Firearm", holsterOnly: false);
            Foley(slots, "MRM_Weapon_HuntingRifle", "Firearm", holsterOnly: false);
            Foley(slots, "MRM_Weapon_FragGrenade", "Throwable", holsterOnly: false);
            Foley(slots, "MRM_Weapon_Molotov", "Throwable", holsterOnly: false);
            Foley(slots, "MRM_Item_Syringe", "HealingWieldable", holsterOnly: false);

            return slots;
        }

        private static void Fire(List<Slot> slots, string name, string variant, string component, params string[] clips)
        {
            slots.Add(new Slot { Container = "Fire_" + name, Clips = clips, WeaponVariant = variant, ComponentType = component, Field = "_fireAudio" });
        }

        private static void Tail(List<Slot> slots, string name, string variant, params string[] clips)
        {
            slots.Add(new Slot { Container = "FireTail_" + name, Clips = clips, WeaponVariant = variant, ComponentType = "FirearmBasicBarrelEffect", Field = "_fireTailAudio" });
        }

        private static void Reload(List<Slot> slots, string name, string variant, string component, string field, params string[] clips)
        {
            string container = name.StartsWith("EmptyReload_") ? name : "Reload_" + name;
            slots.Add(new Slot { Container = container, Clips = clips, WeaponVariant = variant, ComponentType = component, Field = field });
        }

        private static void Foley(List<Slot> slots, string variant, string component, bool holsterOnly)
        {
            if (!holsterOnly)
            {
                slots.Add(new Slot { Container = SharedEquip, Clips = null, WeaponVariant = variant, ComponentType = component, Field = "_equipAudio" });
            }

            slots.Add(new Slot { Container = SharedHolster, Clips = null, WeaponVariant = variant, ComponentType = component, Field = "_holsterAudio" });
        }

        [MenuItem("Tools/MrMoonlight/Weapons/3. Build + wire weapon audio (MRM-25)")]
        public static void Run()
        {
            var log = new StringBuilder();
            List<Slot> slots = BuildSlotTable();

            int created = 0;
            int wired = 0;
            int missing = 0;

            foreach (Slot slot in slots)
            {
                string containerPath = ContainerFolder + "/AudioRC_" + slot.Container + ".asset";
                var container = AssetDatabase.LoadMainAssetAtPath(containerPath);

                if (container == null)
                {
                    if (slot.Clips == null)
                    {
                        log.AppendLine("  MISSING shared container (expected to exist already): " + containerPath);
                        missing++;
                        continue;
                    }

                    container = CreateContainer(containerPath, slot.Clips, log);
                    if (container == null)
                    {
                        missing++;
                        continue;
                    }

                    created++;
                }

                if (WireOntoVariant(slot, container, log))
                {
                    wired++;
                }
                else
                {
                    missing++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MRM-25] Weapon audio: " + created + " containers created, " + wired
                      + " slots wired, " + missing + " problem(s).\n" + log);
        }

        /// <summary>
        /// Creates one AudioRandomContainer through Unity's own creation flow. See the class comment
        /// for why every more obvious route fails.
        /// </summary>
        private static UnityEngine.Object CreateContainer(string path, string[] clipNames, StringBuilder log)
        {
            var clips = new List<AudioClip>();
            foreach (string name in clipNames)
            {
                string clipPath = ClipFolder + "/" + name + ".wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip == null)
                {
                    log.AppendLine("  MISSING clip: " + clipPath);
                    continue;
                }

                clips.Add(clip);
            }

            if (clips.Count == 0)
            {
                log.AppendLine("  no clips resolved for " + path + ", container not created");
                return null;
            }

            Type doCreate = null;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                doCreate = a.GetType("UnityEditor.ProjectWindowCallback.DoCreateAudioRandomContainer");
                if (doCreate != null)
                {
                    break;
                }
            }

            if (doCreate == null)
            {
                log.AppendLine("  CANNOT CREATE: UnityEditor.ProjectWindowCallback.DoCreateAudioRandomContainer not found in this Unity version");
                return null;
            }

            object action = ScriptableObject.CreateInstance(doCreate);
            FieldInfo selection = doCreate.GetField("selection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo create = doCreate.GetMethod("CreateAudioRandomContainerFromSelectedClips",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (selection == null || create == null)
            {
                log.AppendLine("  CANNOT CREATE: DoCreateAudioRandomContainer's API changed shape (selection="
                               + (selection != null) + " method=" + (create != null) + ")");
                return null;
            }

            selection.SetValue(action, clips.ToArray());
            create.Invoke(action, new object[] { path });
            UnityEngine.Object.DestroyImmediate((ScriptableObject)action);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var created = AssetDatabase.LoadMainAssetAtPath(path);
            if (created == null)
            {
                log.AppendLine("  CREATE FAILED: " + path);
                return null;
            }

            ApplyRandomisation(created, log);
            log.AppendLine("  created " + path.Substring(path.LastIndexOf('/') + 1)
                           + " (" + clips.Count + " clip(s))");
            return created;
        }

        /// <summary>
        /// Turns on the pitch/volume randomisation and shuffle playback that make a repeated shot
        /// stop sounding like a copy of itself. Set by serialized property because the container's
        /// managed API is internal too.
        /// </summary>
        private static void ApplyRandomisation(UnityEngine.Object container, StringBuilder log)
        {
            var so = new SerializedObject(container);
            SetVector2(so, "m_PitchRandomizationRange", PitchRange);
            SetVector2(so, "m_VolumeRandomizationRange", VolumeRange);
            SetBool(so, "m_PitchRandomizationEnabled", true);
            SetBool(so, "m_VolumeRandomizationEnabled", true);
            // 1 = Shuffle. Stops the same take playing twice in a row.
            SetInt(so, "m_PlaybackMode", 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(container);
        }

        private static void SetVector2(SerializedObject so, string name, Vector2 value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null)
            {
                p.vector2Value = value;
            }
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null)
            {
                p.boolValue = value;
            }
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null)
            {
                p.intValue = value;
            }
        }

        /// <summary>
        /// Assigns a container into one <c>AudioData</c> field on a weapon variant.
        ///
        /// <para><c>AudioData</c> serializes its container under a nested <c>Clip</c> property —
        /// which is why the audit output reads <c>_fireAudio.Clip</c> rather than <c>_fireAudio</c>.
        /// </para>
        /// </summary>
        private static bool WireOntoVariant(Slot slot, UnityEngine.Object container, StringBuilder log)
        {
            string variantPath = FindVariantPath(slot.WeaponVariant);
            if (variantPath == null)
            {
                log.AppendLine("  MISSING variant: " + slot.WeaponVariant);
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(variantPath);
            try
            {
                bool done = false;
                foreach (Component c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c.GetType().Name != slot.ComponentType)
                    {
                        continue;
                    }

                    var so = new SerializedObject(c);
                    SerializedProperty p = so.FindProperty(slot.Field);
                    if (p == null)
                    {
                        continue;
                    }

                    SerializedProperty clip = p.FindPropertyRelative("Clip");
                    if (clip == null)
                    {
                        continue;
                    }

                    clip.objectReferenceValue = container;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    done = true;
                }

                if (done)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, variantPath);
                    log.AppendLine("  " + slot.WeaponVariant + "." + slot.ComponentType + slot.Field
                                   + " -> AudioRC_" + slot.Container);
                    return true;
                }

                log.AppendLine("  NOT FOUND on " + slot.WeaponVariant + ": "
                               + slot.ComponentType + "." + slot.Field);
                return false;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string FindVariantPath(string variantName)
        {
            string[] guids = AssetDatabase.FindAssets(variantName + " t:Prefab",
                new[] { MoonlightWeaponSet.VariantRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == variantName)
                {
                    return path;
                }
            }

            return null;
        }
    }
}
