// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Editor;
using PampelGames.Shared.Utility;
using UnityEditor;
using UnityEngine;

namespace PampelGames.GoreSimulator.Editor
{
    public static class MeshEditorUtility
    {
        
        /// <summary>
        ///     Initial GoreSimulator component setup.
        /// </summary>
        public static void InitializeMeshSetup(GoreSimulator _goreSimulator)
        {
            
            _goreSimulator.bonesListClasses = new List<BonesListClass>();
            _goreSimulator.bonesListClasses.Add(new BonesListClass());
            var parentBoneListClass = _goreSimulator.bonesListClasses[0]; 
            parentBoneListClass.guid = 0;
            
            List<string> centerBoneNames = Constants.BonesSetupCenter();
            
            for (int i = 0; i < centerBoneNames.Count; i++)
            {
                var boneIndexes = FindBoneIndexesByName(_goreSimulator.smr, centerBoneNames[i]);
                if(boneIndexes.Count == 0) continue;
                parentBoneListClass.bone = _goreSimulator.smr.bones[boneIndexes[0]];
                parentBoneListClass.sendOnDeath = true;
                parentBoneListClass.boneTag = BoneTag.Torso;
                break;
            }
            
            EditorUtility.SetDirty(_goreSimulator);
        }

        private static List<int> FindBoneIndexesByName(SkinnedMeshRenderer smr, string boneName)
        {
            List<int> matchingIndexes = new List<int>();
            
            for (int i = 0; i < smr.bones.Length; i++)
                if (smr.bones[i].name.Contains(boneName))
                    matchingIndexes.Add(i);
            
            boneName = char.ToUpper(boneName[0]) + boneName.Substring(1);
            
            for (int i = 0; i < smr.bones.Length; i++)
                if (smr.bones[i].name.Contains(boneName))
                    matchingIndexes.Add(i);

            return matchingIndexes;
        }
        
        /********************************************************************************************************************************/

        public static void AutoSetupHumanoid(GoreSimulator _goreSimulator)
        {
            if (!InitializeBoneSetup(_goreSimulator)) return;
            var bonesListClasses = _goreSimulator.bonesListClasses;

            List<Transform> bones = new List<Transform>();
            
            List<string> boneNames = Constants.BonesSetupHumanoid();
            
            for (int i = 0; i < boneNames.Count; i++)
            {
                var boneIndexes = FindBoneIndexesByName(_goreSimulator.smr, boneNames[i]);
                for (int j = 0; j < boneIndexes.Count; j++)
                {
                    var bone = _goreSimulator.smr.bones[boneIndexes[j]];
                    if(!BoneWeightsUtility.DoesBoneHaveWeights(_goreSimulator.smr, bone.name)) continue;
                    if(!bones.Contains(bone)) bones.Add(bone);
                }
            }

            var removeDuplicated = Constants.BonesSetupDuplicated();
            for (int i = 0; i < removeDuplicated.Count; i++) RemoveDuplicated(bones, removeDuplicated[i]);
            
            var unusedBoneNames = Constants.UnusedBones();
            bones.RemoveAll(bone => 
                unusedBoneNames.Any(unusedName => 
                    bone.name.IndexOf(unusedName, StringComparison.OrdinalIgnoreCase) >= 0));
            
            
            // Parent bones first:
            var rootBones = _goreSimulator.smr.bones.Where(bone => bone.parent == null);
            var childBones = _goreSimulator.smr.bones.Except(rootBones);
            
            var orderedBones = rootBones.Concat(childBones.OrderBy(GetDepth));
            
            foreach (var bone in orderedBones) 
                AddBoneToClass(bone, bones, bonesListClasses);

        }
        
        public static void AutoSetupGeneric(GoreSimulator _goreSimulator)
        {
            if (!InitializeBoneSetup(_goreSimulator)) return;
            var bonesListClasses = _goreSimulator.bonesListClasses;
            
            List<Transform> bones = new List<Transform>();
            
            for (int i = 0; i < _goreSimulator.smr.bones.Length; i++)
            {
                var bone = _goreSimulator.smr.bones[i];
                if(!BoneWeightsUtility.DoesBoneHaveWeights(_goreSimulator.smr, bone.name)) continue;
                if(!bones.Contains(bone)) bones.Add(bone);
            }
            
            var unusedBoneNames = Constants.UnusedBones();
            bones.RemoveAll(bone => 
                unusedBoneNames.Any(unusedName => 
                    bone.name.IndexOf(unusedName, StringComparison.OrdinalIgnoreCase) >= 0));
            

            var removeDuplicated = Constants.BonesSetupDuplicated();
            for (int i = 0; i < removeDuplicated.Count; i++) RemoveDuplicated(bones, removeDuplicated[i]);   

            
            // Parent bones first:
            var rootBones = _goreSimulator.smr.bones.Where(bone => bone.parent == null);
            var childBones = _goreSimulator.smr.bones.Except(rootBones);

            var orderedBones = rootBones.Concat(childBones.OrderBy(bone => GetDepth(bone)));                

            foreach (var bone in orderedBones) 
                AddBoneToClass(bone, bones, bonesListClasses);
        }
        
        /********************************************************************************************************************************/
        
        
        private static int GetDepth(Transform transform)
        {
            int depth = 0;
            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }
            return depth;
        }
        
        private static bool InitializeBoneSetup(GoreSimulator _goreSimulator)
        {
            var centerBonesListClass = _goreSimulator.bonesListClasses[0];
            if (centerBonesListClass.bone == null)
            {
                EditorUtility.DisplayDialog("Missing Bone", "Please assign the center bone first.", "Ok");
                return false;
            }
            var bonesListClasses = _goreSimulator.bonesListClasses;
            bonesListClasses.Clear();
            bonesListClasses.Add(centerBonesListClass);
            return true;
        }

        private static void RemoveDuplicated(List<Transform> bones, string boneName)
        {
            List<Transform> spineBones = bones.FindAll(b => b.name.Contains(boneName));

            if(spineBones.Count > 1)
            {
                int middleIndex = spineBones.Count / 2;
                Transform middleBone = spineBones[middleIndex];

                foreach(Transform bone in spineBones)
                    if (bone != middleBone) bones.Remove(bone);
            }
            
            boneName = char.ToUpper(boneName[0]) + boneName.Substring(1);
            
            spineBones = bones.FindAll(b => b.name.Contains(boneName));

            if(spineBones.Count > 1)
            {
                int middleIndex = spineBones.Count / 2;
                Transform middleBone = spineBones[middleIndex];

                foreach(Transform bone in spineBones)
                    if (bone != middleBone) bones.Remove(bone);
            }
        }
        
                
        private static void AddBoneToClass(Transform currentBone, List<Transform> bones, List<BonesListClass> bonesListClasses)
        {
            if (!bones.Contains(currentBone) || bonesListClasses.Any(b => b.bone == currentBone)) return;
            
            var guid = MathUtility.UniqueGUID();
            
            
            BonesListClass parentBone = BonesListClassUtility.GetParentBone(currentBone, bonesListClasses);
            if (parentBone == null) return;
            

            var headBones = Constants.HeadBones();
            var centerBones = Constants.CenterBones();
            var armBones = Constants.ArmBones();
            var upperLegBones = Constants.UpperLegBones();
            var lowerLegBones = Constants.LowerLegBones();

            var sendOnDeath = false;
            headBones.AddRange(centerBones);
            for (int i = 0; i < headBones.Count; i++)
            {
                if (currentBone.name.IndexOf(headBones[i], StringComparison.OrdinalIgnoreCase) >= 0) sendOnDeath = true;
            }

            BoneTag boneTag = BoneTag.Other;
            for (int i = 0; i < headBones.Count; i++)
                if (currentBone.name.IndexOf(headBones[i], StringComparison.OrdinalIgnoreCase) >= 0) boneTag = BoneTag.Head;
            for (int i = 0; i < centerBones.Count; i++)
                if (currentBone.name.IndexOf(centerBones[i], StringComparison.OrdinalIgnoreCase) >= 0) boneTag = BoneTag.Torso;
            for (int i = 0; i < armBones.Count; i++)
                if (currentBone.name.IndexOf(armBones[i], StringComparison.OrdinalIgnoreCase) >= 0) boneTag = BoneTag.Arm;
            if(boneTag == BoneTag.Other)
            {
                for (int i = 0; i < upperLegBones.Count; i++)
                    if (currentBone.name.IndexOf(upperLegBones[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        boneTag = BoneTag.Leg;
                for (int i = 0; i < lowerLegBones.Count; i++)
                    if (currentBone.name.IndexOf(lowerLegBones[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        boneTag = BoneTag.Leg;
            }
            

            bonesListClasses.Add(new BonesListClass
            {
                guid = guid,
                parentGuid = parentBone.guid,
                bone = currentBone,
                sendOnDeath = sendOnDeath,
                boneTag = boneTag
            });
            
        }


        
        /********************************************************************************************************************************/
        
        
        /// <summary>
        ///     Returns the list of vertices that are influenced by the specified bone.
        /// </summary>
        public static List<int> GetBoneIndexes(SkinnedMeshRenderer smr, Transform bone, float weightThreshold)
        {
            var bones = smr.bones;
            var boneIndex = Array.IndexOf(bones, bone);
            if (boneIndex == -1) return new List<int>();
            var boneWeights = smr.sharedMesh.boneWeights;
            var resultList = new List<int>();
            for (var i = 0; i < boneWeights.Length; i++)
                if ((boneWeights[i].boneIndex0 == boneIndex && boneWeights[i].weight0 > weightThreshold) ||
                    (boneWeights[i].boneIndex1 == boneIndex && boneWeights[i].weight1 > weightThreshold) ||
                    (boneWeights[i].boneIndex2 == boneIndex && boneWeights[i].weight2 > weightThreshold) ||
                    (boneWeights[i].boneIndex3 == boneIndex && boneWeights[i].weight3 > weightThreshold)
                   )
                {
                    resultList.Add(i);
                }
            
            return resultList;
        }
        
    }
}