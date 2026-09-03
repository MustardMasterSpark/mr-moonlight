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
    public class SubModuleDisableComponents : SubModuleBase
    {
        public override string ModuleName()
        {
            return "Disable Components";
        }

        public override string ModuleInfo()
        {
            return "Disables the specified components when executed.";
        }

        public override int imageIndex()
        {
            return 6;
        }

        public override void ModuleAdded(Type type)
        {
            base.ModuleAdded(type);
            components ??= new List<Component>();
#if UNITY_EDITOR
            if (type == typeof(GoreModuleRagdoll))
            {
                var animator = _goreSimulator.ragdollAnimator;
                if (animator == null) return;
                components.Add(animator);    
            }
#endif
        }

        /********************************************************************************************************************************/
        
        public bool disableBones = true;
        public List<Component> components = new();

        private readonly List<GameObject> disabledBones = new();
        
        /********************************************************************************************************************************/

        public override void ExecuteModuleCut(SubModuleClass subModuleClass)
        {
            if (subModuleClass.multiCut) return;
            SetComponents(false);

            if (subModuleClass.cuttedIndex < 1 && subModuleClass.centerBone != null)
            {
                disabledBones.Add(subModuleClass.centerBone.gameObject);
                subModuleClass.centerBone.gameObject.SetActive(false);
            }
        }

        public override void ExecuteModuleExplosion(SubModuleClass subModuleClass)
        {
            if (!Application.isPlaying) return;
            if (subModuleClass.multiCut || subModuleClass.subRagdoll) return;
            SetComponents(false);
        }

        public override void ExecuteModuleRagdoll(List<GoreBone> goreBones)
        {
            SetComponents(false);
        }
        
        public override void Reset()
        {
            base.Reset();
            SetComponents(true);
            
            for (int i = 0; i < disabledBones.Count; i++)
            {
                if(disabledBones[i] == null) continue;
                disabledBones[i].SetActive(true);
            }
            disabledBones.Clear();
        }

        /********************************************************************************************************************************/

        private void SetComponents(bool enabled)
        {
            foreach (var component in components)
            {
                switch (component)
                {
                    case Behaviour behaviour:
                        behaviour.enabled = enabled;
                        break;
                    case Renderer renderer: 
                        renderer.enabled = enabled;
                        break;
                }
            }
        }
    }
}
