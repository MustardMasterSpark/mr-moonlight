using MrMoonlight.Data;
using MrMoonlight.Player;

namespace MrMoonlight.Items
{
    /// <summary>
    /// Applies one item's effect to the player's stats. A single switch over <see cref="ItemId"/>,
    /// not a virtual method per item or a lookup-table strategy pattern - nine catalogue effects
    /// isn't enough to earn an abstraction, per Docs/csharp-conventions.md. Every value read here is
    /// a named field on <see cref="MoonlightTunables"/>, never a literal. Owner: MRM-41
    /// </summary>
    public static class ItemEffectApplier
    {
        /// <summary>
        /// Returns true if this item actually did something to <paramref name="stats"/>. Equipment
        /// and ammo return false here - their effects (equip behaviour, weapon reserve refills)
        /// belong to other issues that don't exist yet - which <see cref="Inventory"/> uses to
        /// decide whether a "used" item should be consumed from the stack.
        /// </summary>
        public static bool Apply(ItemId id, PlayerStats stats)
        {
            switch (id)
            {
                case ItemId.Crackers:
                    stats.Health.Restore(Tunables.I.CrackersHealAmount);
                    stats.Stamina.Restore(Tunables.I.CrackersStaminaAmount);
                    return true;

                case ItemId.Soda:
                    stats.Health.Restore(Tunables.I.SodaHealAmount);
                    stats.Stamina.Restore(Tunables.I.SodaStaminaAmount);
                    return true;

                case ItemId.Bandages:
                    stats.Health.Restore(Tunables.I.BandagesHealAmount);
                    return true;

                case ItemId.VodkaBottle:
                    stats.Drunkenness.Restore(Tunables.I.VodkaDrunkennessAmount);
                    return true;

                case ItemId.BeerCan:
                    stats.Drunkenness.Restore(Tunables.I.BeerDrunkennessAmount);
                    return true;

                case ItemId.MarijuanaBlunt:
                    stats.WeedHigh.Restore(Tunables.I.WeedHighAmount);
                    return true;

                case ItemId.MorphineVial:
                    stats.MorphineHigh.Restore(Tunables.I.MorphineHighAmount);
                    return true;

                // Ammo refills a weapon's reserve, not a player stat - no weapon system exists yet
                // to call into (Pistol/Shotgun are both still Backlog). Still counted and stacked
                // by Inventory like any other item; wiring the actual refill is that issue's job.
                case ItemId.PistolAmmo:
                case ItemId.ShotgunShells:
                    // TODO(MRM-22 or whichever issue builds the Pistol/Shotgun): refill the matching weapon's reserve.
                    return false;

                // Equipment (canteen, walkie-talkie, matches, map+compass, flashlight, boots,
                // backpack, Polaroid, tent key) has no stat effect here - equipping/using each piece
                // is its own issue's scope. The framework accounts for them (pick up, stack, store
                // like any item) without guessing behaviour that isn't MRM-41's to build.
                default:
                    return false;
            }
        }
    }
}
