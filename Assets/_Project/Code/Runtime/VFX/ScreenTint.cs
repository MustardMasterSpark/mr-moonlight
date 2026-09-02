using System.Collections.Generic;

namespace MrMoonlight.VFX
{
    /// <summary>
    /// Shared registry for the screen's additive red tint. Any system that wants to redden the
    /// screen - the death sequence today, and eventually MRM-53's health damage tint - registers
    /// its own named contribution here instead of drawing its own overlay. This is what B.7.6's
    /// "overlapping HUD effects add, they do not overwrite" and the "one volume, many weighted
    /// overrides" pattern from Docs/webgl-constraints.md §4 actually look like in code.
    /// <see cref="ScreenTintRenderer"/> is the only thing that reads this and draws it, so a new
    /// contributor never touches rendering code - it only ever calls <see cref="SetRed"/> and
    /// <see cref="ClearRed"/>. Built agnostic on purpose: this class has no idea the death
    /// sequence exists, and won't need to know about MRM-53's health tint either. Owner: MRM-17
    ///
    /// Deliberately not a MonoBehaviour or a singleton - the project's only sanctioned singleton
    /// is Tunables (Docs/csharp-conventions.md). A static dictionary is enough, same shape as
    /// the project's GameEvents pattern.
    /// </summary>
    public static class ScreenTint
    {
        private static readonly Dictionary<string, float> _redContributions = new Dictionary<string, float>();

        /// <summary>Every currently-registered red contribution, by source name. Read by <see cref="ScreenTintRenderer"/>; nothing else should need this.</summary>
        public static IReadOnlyDictionary<string, float> RedContributions => _redContributions;

        /// <summary>Sets (or replaces) this source's red contribution, clamped 0-1. Call every frame the effect is active - the death sequence updates its own entry every tick as its curve rises.</summary>
        public static void SetRed(string source, float value01)
        {
            _redContributions[source] = value01 < 0f ? 0f : (value01 > 1f ? 1f : value01);
        }

        /// <summary>Removes a source's contribution entirely, so it stops adding to the tint. Call when the effect ends.</summary>
        public static void ClearRed(string source)
        {
            _redContributions.Remove(source);
        }

        /// <summary>
        /// Removes every contribution. Being a static dictionary, this registry survives a scene
        /// reload untouched — a contributor that ramps to red and gets destroyed without calling
        /// <see cref="ClearRed"/> (e.g. <c>DeathSequence</c> on death) leaves the tint stuck on the
        /// next scene. Call this before loading a new scene from a menu/restart flow.
        /// </summary>
        public static void ClearAll()
        {
            _redContributions.Clear();
        }
    }
}
