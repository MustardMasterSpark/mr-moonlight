// Copyright © Magnetic Arcade. All Rights Reserved.

namespace MA.Flora
{
    internal static class CullingConstants
    {
        public const int MaxVisibleInstancesPerDrawList = 0x00FFFFFF;
        public const int ReservedLodFadeMask = unchecked((int)0xFF000000);
        public const int MaxShadowSplitCount = 4;
        public const int MaxXrSubviewCount = 2;
        public const int MaxLodCount = 8;
    }
}
