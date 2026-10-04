using HeurekaGames.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeurekaGames.SmartBuilder
{
    [UnityEditor.InitializeOnLoad]
    public static class SB_Define
    {
        private static readonly string[] compileSymbols = { "HEUREKAGAMES_SMARTBUILDER" };
        static SB_Define()
        {
            Heureka_AddDefineSymbols.AddDefineSymbols(compileSymbols);
        }
    }
}
