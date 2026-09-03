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
    public static class Constants
    {
        public const string GlobalSettings = "GlobalSettings";
        public const string DefaultReferences = "DefaultReferences";
        public const string ColorKeywords = "ColorKeywords";

        public const float OversizedColliderMultiplier = 2f; // SubModulePhysics may create oversized colliders.

        
#if UNITY_EDITOR
        
        public const string DocumentationURL = "https://docs.google.com/document/d/1NqL4Zc172D0frM8DHTNtB8lufxdg9uqegMT6KVhmMHo/edit?usp=sharing";
        public const string ModuleStorage = "GoreSimulatorModuleStorage";
        
        public static readonly Color InspectorBackgroundHover = new Color32(100, 100, 100, 100);
        
        public static List<string> BonesSetupCenter()
        {
            var boneNames = new List<string>
            {
                "pelvis",
                "center",
                "hips"
            };
            return boneNames;
        }
        public static List<string> BonesSetupDuplicated()
        {
            var boneNames = new List<string>
            {
                "spine",
                "center",
            };
            return boneNames;
        }
        public static List<string> BonesSetupHumanoid()
        {
            var boneNames = new List<string>
            {
                "pelvis",
                "spine",
                "arm",
                "shoulder",
                "elbow",
                "head",
                "leg",
                "thigh",
                "calf",
            };
            return boneNames;
        }

        /********************************************************************************************************************************/
        public static List<string> HeadBones()
        {
            var boneNames = new List<string>
            {
                "head",
                "neck"
            };
            return boneNames;
        }
        public static List<string> CenterBones()
        {
            var boneNames = new List<string>
            {
                "pelvis",
                "spine",
                "center"
            };
            return boneNames;
        }
        
        /********************************************************************************************************************************/
        // Character Joint Setup Only

        public static List<string> ArmBones()
        {
            var boneNames = new List<string>
            {
                "arm",
                "branch",
                "limp",
                "clavicle",
                "hand",
                "shoulder",
                "elbow",
            };
            return boneNames;
        }
        
        public static List<string> UpperLegBones()
        {
            var boneNames = new List<string>
            {
                "upper",
                "thigh"
            };
            return boneNames;
        }
        
        public static List<string> LowerLegBones()
        {
            var boneNames = new List<string>
            {
                "lower",
                "calf"
            };
            return boneNames;
        }
        
        public static List<string> UnusedBones()
        {
            var boneNames = new List<string>
            {
                "twist",
                "eye",
                "jaw",
                "thumb",
                "finger",
                "pinky",
                "ring_",
                "ball",
                "ankle",
                "toes"
            };
            return boneNames;
        }

        public static bool ContainsName(string nameToCheck, List<string> names)
        {
            for (int i = 0; i < names.Count; i++)
            {
                var nameLowercase = names[i];
                var nameCapitalized = char.ToUpper(names[i][0]) + names[i].Substring(1);
        
                if (nameToCheck.Contains(nameLowercase) || nameToCheck.Contains(nameCapitalized)) 
                    return true;
            }
    
            return false;
        }
#endif
        
    }
}
