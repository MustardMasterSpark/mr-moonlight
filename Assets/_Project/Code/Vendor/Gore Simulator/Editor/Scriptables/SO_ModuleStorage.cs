// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PampelGames.GoreSimulator.Editor
{
    public class SO_ModuleStorage : ScriptableObject
    {
        [SerializeReference] public List<GoreModuleBase> goreModules = new();
        [FormerlySerializedAs("cutModules")] [SerializeReference] public List<SubModuleBase> subModules = new();
        [SerializeReference] public List<SubModuleBase> explosionModules = new();
        [SerializeReference] public List<SubModuleBase> ragdollModules = new();
    }
}