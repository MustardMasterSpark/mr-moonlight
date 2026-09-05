using System.Collections.Generic;
using UnityEngine;

namespace MrMoonlight.EditorTools.Migration
{
    /// <summary>
    /// The one description of Mr. Moonlight's weapon set (MRM-25).
    ///
    /// <para>Every tool that needs to know "which weapons, in what category, with what damage" reads
    /// this rather than carrying its own copy — <see cref="MoonlightWeaponSetBuild"/> builds the
    /// prefab variants from it and <see cref="PolymindPlayerBuild"/> assembles the player from it.
    /// Two lists that have to agree is how a weapon ends up in the loadout but missing from its
    /// number key, so there is only one list.</para>
    ///
    /// <para>Damage numbers are <b>not</b> here — they live in <c>MoonlightTunables</c>, per the
    /// project's no-hardcoded-values rule. This holds identity and structure only.</para>
    /// </summary>
    public static class MoonlightWeaponSet
    {
        /// <summary>Where the tracked, Mr. Moonlight-owned weapon prefabs live.</summary>
        public const string VariantRoot = "Assets/_Project/Prefabs/Weapons";

        /// <summary>Where the raw vendor wieldables live (git-ignored, per the ThirdParty policy).</summary>
        public const string VendorRoot = "Assets/ThirdParty/PolymindGames/HQFPS/Prefabs/Wieldables";

        /// <summary>Which number key a weapon answers to. Matches
        /// <c>MoonlightWeaponCategorySwitcher.CategoryKey</c>, and doubles as the folder name under
        /// <see cref="VariantRoot"/>.</summary>
        public enum Category
        {
            Melee,      // key 1
            Pistol,     // key 2
            Shotgun,    // key 3
            Rifle,      // key 4
            Precision,  // key 5
            Throwable,  // key 7 / G
            Item,       // no number key - the Syringe, reached with the Heal key
            Hands,      // not a weapon and not an inventory item; always present
        }

        public sealed class Entry
        {
            /// <summary>Vendor prefab name, without the path or extension.</summary>
            public string VendorName;

            /// <summary>Item definition name, exactly as it appears in
            /// Resources/Definitions/Item. This is the key everything else resolves by.</summary>
            public string ItemName;

            /// <summary>Short, human name used for the variant asset file.</summary>
            public string ShortName;

            public Category Group;

            /// <summary>Order within the category — the order repeated key presses walk through.</summary>
            public int Order;
        }

        /// <summary>
        /// The thirteen-weapon testing arsenal plus the Syringe and the unarmed hands.
        ///
        /// <para>Carlos's category assignment, verbatim from 2026-09-04. The Club is the BaseballBat
        /// asset — <c>Docs/glossary.md</c>, ruled 2026-08-28, which also records that "Trench Club"
        /// is a superseded name for the same thing.</para>
        /// </summary>
        public static readonly List<Entry> All = new List<Entry>
        {
            new Entry { VendorName = "HQFPS_Wieldable_Arms",            ItemName = null,                   ShortName = "Hands",        Group = Category.Hands,     Order = 0 },

            new Entry { VendorName = "HQFPS_Wieldable_CombatKnife",     ItemName = "Combat Knife",         ShortName = "CombatKnife",  Group = Category.Melee,     Order = 0 },
            new Entry { VendorName = "HQFPS_Wieldable_FireAxe",         ItemName = "Fire Axe",             ShortName = "FireAxe",      Group = Category.Melee,     Order = 1 },
            new Entry { VendorName = "HQFPS_Wieldable_BaseballBat",     ItemName = "Baseball Bat",         ShortName = "Club",         Group = Category.Melee,     Order = 2 },

            new Entry { VendorName = "HQFPS_Wieldable_M1911",           ItemName = "M1911",                ShortName = "M1911",        Group = Category.Pistol,    Order = 0 },
            new Entry { VendorName = "HQFPS_Wieldable_Revolver",        ItemName = "Revolver",             ShortName = "Revolver",     Group = Category.Pistol,    Order = 1 },

            new Entry { VendorName = "HQFPS_Wieldable_R870",            ItemName = "R870",                 ShortName = "R870",         Group = Category.Shotgun,   Order = 0 },
            new Entry { VendorName = "HQFPS_Wieldable_DBShotgun",       ItemName = "Double Barrel Shotgun", ShortName = "DBShotgun",   Group = Category.Shotgun,   Order = 1 },

            new Entry { VendorName = "HQFPS_Wieldable_M1A",             ItemName = "M1A",                  ShortName = "M1A",          Group = Category.Rifle,     Order = 0 },
            new Entry { VendorName = "HQFPS_Wieldable_AKM",             ItemName = "AKM",                  ShortName = "AKM",          Group = Category.Rifle,     Order = 1 },

            new Entry { VendorName = "HQFPS_Wieldable_Crossbow",        ItemName = "Crossbow",             ShortName = "Crossbow",     Group = Category.Precision, Order = 0 },
            new Entry { VendorName = "HQFPS_Wieldable_HuntingRifle",    ItemName = "Hunting Rifle",        ShortName = "HuntingRifle", Group = Category.Precision, Order = 1 },

            new Entry { VendorName = "HQFPS_Wieldable_FragGrenade",     ItemName = "Frag Grenade",         ShortName = "FragGrenade",  Group = Category.Throwable, Order = 0 },
            new Entry { VendorName = "HQFPS_Wieldable_MolotovCocktail", ItemName = "Molotov Cocktail",     ShortName = "Molotov",      Group = Category.Throwable, Order = 1 },

            new Entry { VendorName = "HQFPS_Wieldable_Syringe",         ItemName = "Syringe",              ShortName = "Syringe",      Group = Category.Item,      Order = 0 },
        };

        public static string VendorPath(Entry e) => VendorRoot + "/" + e.VendorName + ".prefab";

        public static string VariantPath(Entry e) =>
            VariantRoot + "/" + e.Group + "/MRM_" + (e.Group == Category.Item ? "Item_" : "Weapon_") + e.ShortName + ".prefab";

        /// <summary>Per-projectile firearm damage, keyed by short name. Read live from
        /// <c>MoonlightTunables</c> so the tuning surface stays in one place.</summary>
        public static Dictionary<string, float> FirearmDamage()
        {
            var t = MrMoonlight.Data.Tunables.I;
            if (t == null)
            {
                return new Dictionary<string, float>();
            }

            return new Dictionary<string, float>
            {
                { "M1911", t.WeaponDamageM1911 },
                { "Revolver", t.WeaponDamageRevolver },
                { "R870", t.WeaponDamageR870 },
                { "DBShotgun", t.WeaponDamageDBShotgun },
                { "M1A", t.WeaponDamageM1A },
                { "AKM", t.WeaponDamageAKM },
                { "Crossbow", t.WeaponDamageCrossbow },
                { "HuntingRifle", t.WeaponDamageHuntingRifle },
            };
        }

        /// <summary>Per-swing melee damage range (min, max), keyed by short name.</summary>
        public static Dictionary<string, Vector2> MeleeDamage()
        {
            var t = MrMoonlight.Data.Tunables.I;
            if (t == null)
            {
                return new Dictionary<string, Vector2>();
            }

            return new Dictionary<string, Vector2>
            {
                { "CombatKnife", new Vector2(t.WeaponDamageCombatKnifeMin, t.WeaponDamageCombatKnifeMax) },
                { "FireAxe", new Vector2(t.WeaponDamageFireAxeMin, t.WeaponDamageFireAxeMax) },
                { "Club", new Vector2(t.WeaponDamageClubMin, t.WeaponDamageClubMax) },
            };
        }
    }
}
