// ----------------------------------------------------
// Spawn Machine
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
#if UNITY_EDITOR
using PampelGames.Shared.Editor;
#endif
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    public class SO_GlobalSettings : ScriptableObject
    {

#if UNITY_EDITOR
        public static SO_GlobalSettings Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadSingletonInstance()
        {
            Instance = PGAssetUtility.LoadAsset<SO_GlobalSettings>(Constants.GlobalSettings);
        }
#endif
  

        // Pooling
        public bool poolCutActive = true;
        public bool poolEffectsActive = true;
        public int cutPreload = 25;
        public int effectsPreload = 5;
        public bool hidePooledObjects = true;
        
        /********************************************************************************************************************************/

        public void ResetValues()
        {
            poolCutActive = true;
            poolEffectsActive = true;
            hidePooledObjects = true;
            cutPreload = 25;
            effectsPreload = 5;
        }

    }
}