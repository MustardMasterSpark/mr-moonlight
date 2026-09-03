// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;

namespace PampelGames.GoreSimulator
{
    public static class Enums
    {
        public enum ModuleType
        {
            Cut,
            Explosion,
            Ragdoll
        }
        public enum Collider
        {
            None,
            Box,
            Capsule,
            Mesh,
            Automatic
        }
        
        public enum FallbackCollider
        {
            Box,
            Capsule,
        }

        public enum Children
        {
            None,
            Rendered,
            All
        }

        public enum MultiCutStatus
        {
            Mesh,
            Ragdoll
        }

        public enum ParticlePositionExpl
        {
            Center,
            Method,
            Parts
        }

        public enum ParticleRotationExpl
        {
            Default,
            Method,
            Random,
            Forward
        }
        
        public enum ParticleRotationCut
        {
            CutDirection,
            Default,
            Method,
            Random
        }
        
        public enum ParticleSetParent
        {
            None,
            Both,
            Character,
            DetachedPart
        }
        
        public enum SpawnType
        {
            ParticleSystem,
            Gameobject
        }
        
    }
    
    /********************************************************************************************************************************/
    // Seperated for simpler user-access.
    public enum BoneTag
    {
        All,
        Head,
        Arm,
        Leg,
        Torso,
        Child,
        Other
    }
}
