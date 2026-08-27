namespace MrMoonlight.Items
{
    /// <summary>
    /// Every item in MRM-41's demo catalogue, plus the non-consumable equipment the framework needs
    /// to account for even though most of that equipment's actual "use" behaviour belongs to other
    /// issues (boots, flashlight, etc.). One flat enum, not a class hierarchy - nine catalogue
    /// effects and nine equipment pieces isn't enough variety to earn polymorphism, per
    /// Docs/csharp-conventions.md's "no premature abstraction". Owner: MRM-41
    /// </summary>
    public enum ItemId
    {
        Crackers,
        Soda,
        Bandages,
        VodkaBottle,
        BeerCan,
        MarijuanaBlunt,
        MorphineVial,
        PistolAmmo,
        ShotgunShells,
        Canteen,
        WalkieTalkie,
        Matches,
        MapAndCompass,
        Flashlight,
        Boots,
        Backpack,
        Polaroid,
        TentKey
    }
}
