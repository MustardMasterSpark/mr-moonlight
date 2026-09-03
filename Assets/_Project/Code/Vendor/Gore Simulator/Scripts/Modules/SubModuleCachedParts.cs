// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    [Serializable]
    public class SubModuleCachedParts : SubModuleBase
    {
        public override string ModuleName()
        {
            return "Cached Parts";
        }

        public override string ModuleInfo()
        {
            return "Use non-animated existing parts instead of creating animated detached parts on execution.\n" +
                   "This is helpful if you are seeking maximum performance.\n\n" +
                   "Mesh Parts can be created in the initialization window." +
                   "Note that other modules (Physics etc.) are only applied when the parts are created in the editor, not on execution.";
        }

        public override int imageIndex()
        {
            return 10;
        }


        public override bool CompatibleCut()
        {
            return false;
        }

        public override bool CompatibleRagdoll()
        {
            return false;
        }

        /********************************************************************************************************************************/

        public MeshParts meshPartsParent;

        /********************************************************************************************************************************/
        public override void ExecuteModuleExplosion(SubModuleClass subModuleClass)
        {
        }

        public override void ExecuteModuleCut(SubModuleClass subModuleClass)
        {
        }

        public override void ExecuteModuleRagdoll(List<GoreBone> goreBones)
        {
        }

        /********************************************************************************************************************************/
    }
}