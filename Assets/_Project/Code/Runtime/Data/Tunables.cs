using UnityEngine;

namespace MrMoonlight.Data
{
    /// <summary>
    /// Single access point for <see cref="MoonlightTunables"/>, so scripts read one shared
    /// instance instead of each holding its own copy. Owner: MRM-7
    ///
    /// Resolves the asset named "MoonlightTunables" from a Resources folder. This is the one
    /// sanctioned use of Resources.Load in the project — it is baked into the build and
    /// resolved once. See Docs/unity-conventions.md. No other system should follow this
    /// pattern; this is also the project's only singleton, by design (see
    /// Docs/csharp-conventions.md).
    /// </summary>
    public static class Tunables
    {
        private static MoonlightTunables _instance;

        public static MoonlightTunables I =>
            _instance ??= Resources.Load<MoonlightTunables>("MoonlightTunables");
    }
}
