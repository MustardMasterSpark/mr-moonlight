namespace MrMoonlight.Enemies
{
    /// <summary>
    /// Which enemy this is. Exists so behaviours can ask "is there another one of *me* nearby?"
    /// without a name comparison or a per-enemy component check — the Spotter's alone-check
    /// (MRM-34) is the first user, the Wolf's pack check (MRM-33) is the next.
    /// Owner: MRM-34.
    /// </summary>
    public enum EnemyKind
    {
        Spotter,
        Zealot,
        Wolf,
        Furman,
        Wendigo
    }
}
