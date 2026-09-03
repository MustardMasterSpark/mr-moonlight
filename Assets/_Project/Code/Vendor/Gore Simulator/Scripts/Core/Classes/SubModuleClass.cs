// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace PampelGames.GoreSimulator
{
    public class SubModuleClass
    {
        public Vector3 cutPosition = Vector3.zero;
        public Vector3 cutDirection = Vector3.zero;
        public Vector3 position = Vector3.zero;
        public Vector3 force = Vector3.zero;
        public Vector3 centerPosition = Vector3.zero;

        public GameObject parent;

        /// <summary>
        ///     'nonBoneChildren' -> Child transforms that do not belong to a Bones Class (items etc.).
        /// </summary>
        public List<Transform> children = new();
        public Mesh centerMesh;
        
        public bool subRagdoll;
        public bool multiCut;
        public bool cachedPartsUsed;
        
        public readonly List<SubModuleObjectClass> subModuleObjectClasses = new();
        
        /// <summary>
        ///     Does only apply to Cut. -1 means it has not been cutted in between (so completely cutted).
        /// </summary>
        public int cuttedIndex = -1;
        
        internal Transform centerBone; // Does only apply to Cut, not explosion. 
    }

    public class SubModuleObjectClass
    {
        public List<Vector3> cutCenters = new();
        public Vector3 centerPosition = Vector3.zero;

        public GameObject obj;
        public Mesh mesh;
        public Renderer renderer;
        public BoneTag boneTag;
        
        public Vector3 force = Vector3.zero;
        public float mass = 1f;
        public Vector3 boundsSize = Vector3.zero;
    }
    
    internal static class ExecutionClassesUtility
    {
        internal static SubModuleClass CreateSubModuleClass()
        {
            var subModuleClass = new SubModuleClass();
            return subModuleClass;
        }
        
        internal static void ReleaseSubModuleClass(SubModuleClass subModuleClass)
        {
            subModuleClass.parent = null;
            if(Application.isPlaying) subModuleClass.children.Clear();
            subModuleClass.centerMesh = null;
            subModuleClass.centerBone = null;
            subModuleClass.subModuleObjectClasses.Clear();
        }
        
        internal static ExecutionCutClass CreateExecutionCutClass()
        {
            var executionCutClass = new ExecutionCutClass();
            return executionCutClass;
        }
        
        internal static void ReleaseExecutionCutClass(ExecutionCutClass executionCutClass)
        {
            executionCutClass.newIndexes.Clear();
            executionCutClass.cutIndexes.Clear();
            executionCutClass.sewIndexes.Clear();
            executionCutClass.sewTriangles.Clear();
        }
    }
    
}
