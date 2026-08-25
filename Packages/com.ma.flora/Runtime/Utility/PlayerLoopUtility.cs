// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEngine.LowLevel;

namespace MA.Flora
{
    internal static class PlayerLoopUtility
    {
        public enum AddMode { Beginning, End }

        public static bool TryAddToPlayerLoop(PlayerLoopSystem.UpdateFunction function, Type ownerType, Type playerLoopSystemType, AddMode addMode)
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            bool success = TryAddToPlayerLoop(function, ownerType, ref playerLoop, playerLoopSystemType, addMode);
            PlayerLoop.SetPlayerLoop(playerLoop);
            return success;
        }

        public static bool TryAddToPlayerLoop(PlayerLoopSystem.UpdateFunction function, Type ownerType, ref PlayerLoopSystem playerLoop, Type playerLoopSystemType, AddMode addMode)
        {
            if (playerLoop.type == playerLoopSystemType)
            {
                int oldListLength = playerLoop.subSystemList?.Length ?? 0;
                Array.Resize(ref playerLoop.subSystemList, oldListLength + 1);

                PlayerLoopSystem system = new PlayerLoopSystem {
                    type = ownerType,
                    updateDelegate = function
                };

                if (addMode == AddMode.Beginning)
                {
                    Array.Copy(playerLoop.subSystemList, 0, playerLoop.subSystemList, 1, playerLoop.subSystemList.Length - 1);
                    playerLoop.subSystemList[0] = system;
                }
                else if (addMode == AddMode.End)
                {
                    playerLoop.subSystemList[oldListLength] = system;
                }

                return true;
            }

            if (playerLoop.subSystemList != null)
            {
                for(int i = 0; i < playerLoop.subSystemList.Length; ++i)
                {
                    if (TryAddToPlayerLoop(function, ownerType, ref playerLoop.subSystemList[i], playerLoopSystemType, addMode))
                        return true;
                }
            }

            return false;
        }

        public static bool TryRemoveLoopSystem(Type childSystemType)
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            bool success = TryRemoveLoopSystem(ref playerLoop, childSystemType);
            PlayerLoop.SetPlayerLoop(playerLoop);
            return success;
        }

        public static bool TryRemoveLoopSystem(ref PlayerLoopSystem parentLoopSystem, Type childSystemType)
        {
            if (parentLoopSystem.subSystemList == null)
                return false;

            int systemPosition = FindSystemPosition(parentLoopSystem.subSystemList, childSystemType);
            if (systemPosition != -1)
            {
                RemoveSystemAt(ref parentLoopSystem, systemPosition);
                return true;
            }

            for (int i = 0; i < parentLoopSystem.subSystemList.Length; ++i)
            {
                if (TryRemoveLoopSystem(ref parentLoopSystem.subSystemList[i], childSystemType))
                    return true;
            }

            return false;
        }

        private static int FindSystemPosition(PlayerLoopSystem[] subSystemList, Type systemType)
        {
            for (int i = 0; i < subSystemList.Length; i++)
            {
                if (subSystemList[i].type == systemType)
                    return i;
            }

            return -1;
        }

        private static void RemoveSystemAt(ref PlayerLoopSystem parentLoopSystem, int systemPosition)
        {
            PlayerLoopSystem[] newSubsystemList = new PlayerLoopSystem[parentLoopSystem.subSystemList.Length - 1];

            if (systemPosition > 0)
                Array.Copy(parentLoopSystem.subSystemList, newSubsystemList, systemPosition);

            if (systemPosition < parentLoopSystem.subSystemList.Length - 1)
                Array.Copy(parentLoopSystem.subSystemList, systemPosition + 1, newSubsystemList, systemPosition, parentLoopSystem.subSystemList.Length - systemPosition - 1);

            parentLoopSystem.subSystemList = newSubsystemList;
        }
    }
}
