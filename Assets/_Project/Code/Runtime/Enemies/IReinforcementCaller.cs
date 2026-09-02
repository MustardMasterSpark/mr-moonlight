namespace MrMoonlight.Enemies
{
    /// <summary>
    /// Something on an enemy that can summon more enemies. The Spotter has two independent ones —
    /// the flare (proactive, fires when isolated) and the panic call (reactive, fires when hurt) —
    /// and <see cref="EnemyReinforcementSpawner"/> needs to be able to silence all of them on a
    /// freshly spawned reinforcement without knowing which is which.
    /// Owner: MRM-34.
    /// </summary>
    public interface IReinforcementCaller
    {
        /// <summary>Permanently stop this caller from ever summoning anyone. Called on spawned reinforcements.</summary>
        void SuppressCall();
    }
}
