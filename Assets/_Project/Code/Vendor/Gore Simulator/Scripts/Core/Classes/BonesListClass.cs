// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    /// <summary>
    ///     Used for the Editor Bones Tree View.
    /// </summary>
    [Serializable]
    public class BonesListClass
    {
        public Transform bone;

        /// <summary>
        ///     Unique GUID of this list item. Root bone is always '0'.
        /// </summary>
        public int guid;
        /// <summary>
        ///     Parent GUID of this list item.
        /// </summary>
        public int parentGuid;

        /// <summary>
        ///     Whether to send the OnDeath event when it is being cut.
        /// </summary>
        public bool sendOnDeath;

        /// <summary>
        ///     Custom bone tag which will be attached to cut parts via the <see cref="GoreTags"/> component.
        /// </summary>
        public BoneTag boneTag = BoneTag.Other;
    }
    
    public static class BonesListClassUtility
    {
        
        public static int GetNumberOfParents(BonesListClass startNode, List<BonesListClass> bonesListClasses)
        {
            int parentCount = 0;
            var currentNode = startNode;
            HashSet<BonesListClass> visitedParents = new HashSet<BonesListClass>();

            while (true)
            {
                var parentFound = bonesListClasses.Find(x => x.guid == currentNode.parentGuid);
                if (parentFound == null || !visitedParents.Add(parentFound)) break;
                parentCount++;
                currentNode = parentFound;
                if (parentCount > 20) break;
            }

            return parentCount;
        }
        
        public static List<BonesListClass> SortBonesList(List<BonesListClass> bonesListClasses)
        {
            var sortedList = new List<BonesListClass>();
            var visited = new HashSet<int>();
            AddChildren(0, bonesListClasses, sortedList, visited);
            return sortedList;
        }

        private static void AddChildren(int parentGuid, List<BonesListClass> bonesListClasses, List<BonesListClass> sortedList, HashSet<int> visited)
        {
            foreach (var bone in bonesListClasses.Where(b => b.parentGuid == parentGuid))
            {
                if (!visited.Add(bone.guid)) continue;
                sortedList.Add(bone);
                AddChildren(bone.guid, bonesListClasses, sortedList, visited);
            }
        }

        public static BonesListClass GetParentBone(Transform currentBone, List<BonesListClass> bonesListClasses)
        {
            Transform parentTransform = currentBone.parent;
            BonesListClass parentBone = null;
            while (parentTransform != null)
            {
                var foundBone = bonesListClasses.Find(b => b.bone != null && b.bone == parentTransform);
                if (foundBone != null)
                {
                    parentBone = foundBone;
                    break;
                }
                parentTransform = parentTransform.parent;
            }

            return parentBone;
        }   
        
    }
}
#endif
