namespace MA.Flora
{
    internal static class ChangeVersionUtility
    {
        public static bool DidChange(uint changeVersion, uint currentVersion)
        {
            if (currentVersion == 0)
                return true;

            return (int)(changeVersion - currentVersion) > 0;
        }
    }
}
