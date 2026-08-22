using System;

namespace MrMoonlight.UI
{
    /// <summary>
    /// Fired when every open HUD element (inventory, map) needs to force-close instantly, no
    /// animation - currently only the death sequence raises this (MRM-17). No subscribers exist
    /// yet: the inventory (MRM-42) and map (MRM-16) are both still Backlog. When either lands, it
    /// subscribes here and snaps its own UI shut; there is nothing else to build on this end
    /// until then - see the note left on those issues. A single narrow event, not a general
    /// message bus, per Docs/csharp-conventions.md. Owner: MRM-17
    /// </summary>
    public static class HudCloseRequest
    {
        public static event Action OnForceCloseAll;

        public static void RaiseForceCloseAll() => OnForceCloseAll?.Invoke();
    }
}
