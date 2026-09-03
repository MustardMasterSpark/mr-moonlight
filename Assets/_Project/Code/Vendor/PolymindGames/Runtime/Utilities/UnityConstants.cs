namespace PolymindGames
{
    /// <summary>
    /// Provides constants representing layer indices for commonly used layers in Unity.
    /// </summary>
    public static partial class LayerConstants
    {
        // Layer indices
        public const int Default = 0;
        public const int TransparentFX = 1;
        public const int IgnoreRaycast = 2;
        public const int Water = 4;
        public const int UI = 5;
        public const int Debris = 18;
        public const int Effect = 19;
        public const int TriggerZone = 20;
        public const int Interactable = 21;
        public const int ViewModel = 22;
        public const int PostProcessing = 23;
        public const int Hitbox = 24;
        public const int Character = 25;
        public const int StaticObject = 14;
        public const int DynamicObject = 15;
        public const int Building = 16;
        public const int InteractableNoCollision = 17;

        // --- Mr. Moonlight layers (MRM-9 HQ FPS swap) ---
        // Layers 6-13 belong to Mr. Moonlight and predate this framework, so the eight
        // FPSCore layers that used to sit there were remapped to 18-25 instead. See
        // Docs/mrm9-hqfps-integration.md for the full before/after table.
        public const int MoonlightPlayer = 3;
        public const int MoonlightEnemy = 6;
        public const int MoonlightDestructible = 7;
        public const int MoonlightGround = 8;
        public const int MoonlightDroppedProp = 9;
        public const int MoonlightHealth = 10;
        public const int MoonlightConsumable = 11;
        public const int MoonlightWeapon = 12;
        public const int MoonlightNPC = 13;

        
        // Layer masks
        public const int CharacterMask = (1 << Character);
        public const int BuildingMask = (1 << Building);
        // Mr. Moonlight enemies carry their damage hitboxes on Health and breakables on
        // Destructible, so weapon rays have to see those too - MRM-9.
        //
        // Enemy (6) is deliberately NOT in here. That layer holds the enemy's root Blaze AI
        // movement capsule, which encloses all fifteen precise hitboxes but has no IDamageHandler
        // of its own. With Enemy in the mask, every shot stopped on that capsule, spawned an impact
        // decal and dealt no damage at all - the hitboxes behind it were never reached. Leaving it
        // out lets rays pass through the movement capsule and land on the real hitboxes, which is
        // also what preserves MRM-34's per-limb damage multipliers.
        public const int MoonlightDamageableMask = (1 << MoonlightHealth) | (1 << MoonlightDestructible);
        public const int DamageableMask = (1 << Hitbox) | (1 << Interactable) | (1 << DynamicObject) | MoonlightDamageableMask;
        public const int InteractableMask = (1 << Interactable) | (1 << InteractableNoCollision) | (1 << Building);
        public const int SimpleSolidObjectsMask = (1 << Default) | (1 << StaticObject) | (1 << DynamicObject) | (1 << MoonlightGround);
        public const int SolidObjectsMask = SimpleSolidObjectsMask | DamageableMask | BuildingMask;
    }

    /// <summary>
    /// Provides constants representing commonly used tags in Unity.
    /// </summary>
    public static partial class TagConstants
    {
        // Tag names
        public const string MainCamera = "MainCamera";
        public const string Player = "Player";
        public const string GameController = "GameController";
    }

    /// <summary>
    /// Provides constants representing execution order values for script execution in Unity.
    /// </summary>
    public static partial class ExecutionOrderConstants
    {
        // Execution order values
        public const int Manager = -100000;
        public const int MonoSingleton = -10000;
        public const int BeforeDefault3 = -1000;
        public const int BeforeDefault2 = -100;
        public const int BeforeDefault1 = -10;
        public const int AfterDefault1 = 10;
        public const int AfterDefault2 = 100;
    }
}