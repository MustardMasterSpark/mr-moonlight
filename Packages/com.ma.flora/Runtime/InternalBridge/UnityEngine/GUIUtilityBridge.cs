// Copyright © Magnetic Arcade. All Rights Reserved.

namespace MA.InternalBridge
{
    internal static class GUIUtilityBridge
    {
        public static int GetPermanentControlID()
        {
            return UnityEngine.GUIUtility.GetPermanentControlID();
        }
    }
}
