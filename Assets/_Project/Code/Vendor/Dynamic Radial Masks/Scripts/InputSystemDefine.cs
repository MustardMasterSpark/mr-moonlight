// Dynamic Radial Masks <https://u3d.as/1w0H>
// Copyright (c) Amazing Assets <https://amazingassets.world>

#if UNITY_EDITOR
using System;

using UnityEditor;
using UnityEditor.Build;


namespace AmazingAssets.DynamicRadialMasks
{
    [InitializeOnLoad]
    public class InputSystemDefine
    {
        static InputSystemDefine()
        {

#if UNITY_6000_0_OR_NEWER
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            if (Type.GetType("UnityEngine.InputSystem.InputAction, Unity.InputSystem") != null)
            {
                if (!defines.Contains("USE_INPUT_SYSTEM"))
                {
                    defines += ";USE_INPUT_SYSTEM";
                    PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, defines);
                }
            }
            else
            {
                if (defines.Contains("USE_INPUT_SYSTEM"))
                {
                    defines = defines.Replace("USE_INPUT_SYSTEM", "");
                    PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, defines);
                }
            }
#else
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
            if (Type.GetType("UnityEngine.InputSystem.InputAction, Unity.InputSystem") != null)
            {
                if (!defines.Contains("USE_INPUT_SYSTEM"))
                {
                    defines += ";USE_INPUT_SYSTEM";
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defines);
                }
            }
            else
            {
                if (defines.Contains("USE_INPUT_SYSTEM"))
                {
                    defines = defines.Replace("USE_INPUT_SYSTEM", "");
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defines);
                }
            }
#endif
        }
    }
}
#endif