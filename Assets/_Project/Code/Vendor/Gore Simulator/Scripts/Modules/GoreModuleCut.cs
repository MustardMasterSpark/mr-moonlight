// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using PampelGames.Shared.Utility;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    [Serializable]
    public class GoreModuleCut : GoreModuleBase
    {
        public override string ModuleName()
        {
            return "Cut";
        }

        public override string ModuleInfo()
        {
            return "Cut Module: Enables mesh cutting.\n" + "\n" +
                   "The suggested way to execute is via the IGoreObject interface attached to the bones, using IGoreObject.ExecuteCut().";
        }

        public override int imageIndex()
        {
            return 1;
        }

        public override void ClearSubmodules()
        {
            _goreSimulator.cutModules.Clear();
        }

        private readonly List<GameObject> currentPoolableObjects = new();
        private readonly List<GameObject> currentDestroyableObjects = new();

        /********************************************************************************************************************************/

        public override void Reset(List<BonesClass> bonesClasses)
        {
            if (!_goreSimulator.meshCutInitialized) return;
            base.Reset(bonesClasses);
            for (var i = 0; i < _goreSimulator.bonesClasses.Count; i++) bonesClasses[i].ResetCutted();
            for (var i = 0; i < _goreSimulator.cutModules.Count; i++) _goreSimulator.cutModules[i].Reset();
        }

        public override void FinalizeExecution()
        {
            base.FinalizeExecution();

            for (var k = 0; k < _goreSimulator.cutModules.Count; k++)
            {
                if (!_goreSimulator.cutModules[k].moduleActive) continue;
                _goreSimulator.cutModules[k].FinalizeExecution(
                    new List<GameObject>(currentPoolableObjects), new List<GameObject>(currentDestroyableObjects));
            }
        }

        /********************************************************************************************************************************/


        public string ExecuteCut(string boneName, Vector3 position, Vector3 force, out GameObject detachedObject)
        {
            detachedObject = null;
            currentPoolableObjects.Clear();
            currentDestroyableObjects.Clear();

            if (boneName == _goreSimulator.center.name)
            {
                var nearestChildIndex = CutUtility.FindNearestTransformIndex(_goreSimulator.centerBonesClass.firstChildren, position);
                boneName = _goreSimulator.centerBonesClass.firstChildren[nearestChildIndex].name;
            }

            if (!_goreSimulator.bonesDict.TryGetValue(boneName, out var bonesClassTuple)) return string.Empty;

            var currentMesh = _goreSimulator.smr.sharedMesh;
            _goreSimulator.smr.sharedMesh = _goreSimulator.originalMesh;
            _goreSimulator.smr.BakeMesh(_goreSimulator.bakedMesh);
            _goreSimulator.smr.sharedMesh = currentMesh;

            var bakedVertices = PGMeshUtility.CreateVertexList(_goreSimulator.bakedMesh);
            var cutIndex = CutUtility.GetCutIndex(_goreSimulator.smr.transform, bonesClassTuple.Item2, bakedVertices, position);
            
            var bakedNormals = new List<Vector3>(bakedVertices.Count);
            _goreSimulator.bakedMesh.GetNormals(bakedNormals);

            var bonesClass = bonesClassTuple.Item1;
            var bonesStorageClass = bonesClassTuple.Item2;
            var cutIndexClass = bonesStorageClass.cutIndexClasses[cutIndex];

            var executionCutClass = ExecutionClassesUtility.CreateExecutionCutClass();

            /********************************************************************************************************************************/
            // Logic if already cutted bones exist.

            var innerChunkClasses = CutUtility.AddBoneHierarchy(_goreSimulator, bonesClassTuple, executionCutClass, cutIndex,
                out var boneAmount);

            if (innerChunkClasses.Count == 0) return string.Empty;

            CutUtility.RemoveUsedChildren(_goreSimulator.usedBonesClasses, _goreSimulator.centerExecutionCutClass, bonesClass);

            bonesClass.cutted = true;
            bonesClass.cuttedIndex = cutIndexClass.cutIndex;
            for (var i = 0; i < bonesClass.boneChildrenSel.Count; i++)
            {
                if (!_goreSimulator.bonesDict.TryGetValue(bonesClass.boneChildrenSel[i].name, out var childTuple)) continue;
                childTuple.Item1.cutted = true;
                childTuple.Item1.cuttedIndex = -1;
            }

            if (cutIndexClass.cutIndex == 0 && bonesClass.parentExists)
                if (_goreSimulator.bonesDict.TryGetValue(bonesClass.firstParent.name, out var parentTuple))
                    parentTuple.Item1.firstChildCutted = true;

            /********************************************************************************************************************************/

            _goreSimulator.AddCutMaterial();
            
            _goreSimulator.centerExecutionCutClass.newIndexes.ExceptWith(cutIndexClass.oppositeParentIndexes);
            _goreSimulator.usedBonesClasses.Add(new UsedBonesClass());
            _goreSimulator.usedBonesClasses[^1].AddItems(bonesClass.bone, cutIndexClass.cutIndex);
            
            executionCutClass.AddExecutionIndexes(cutIndexClass.cutIndexesParentSide, cutIndexClass.sewIndexesParentSide,
                cutIndexClass.sewTrianglesParentSide);

            _goreSimulator.centerExecutionCutClass.AddExecutionIndexes(cutIndexClass.cutIndexesParentSide, cutIndexClass.sewIndexesForParent,
                cutIndexClass.sewTrianglesForParent);

            var validateContinue = MeshCutJobs.IndexesSnapshotCut(_goreSimulator.meshNativeDataClass, _goreSimulator.originalMesh,
                _goreSimulator.centerExecutionCutClass, _goreSimulator.centerMesh);
            if (!validateContinue) return string.Empty;

            GoreMultiCut goreMultiCut;

            var children = BonesUtility.GetChildren(_goreSimulator.smrBones, bonesClass.bone, _goreSimulator.childrenEnum, _goreSimulator.fixedChildren);
            foreach (var child in children) child.parent = null;


            var subModuleClass = ExecutionClassesUtility.CreateSubModuleClass();
            subModuleClass.children = children;
            subModuleClass.centerMesh = _goreSimulator.centerMesh;
            subModuleClass.force = force;
            subModuleClass.cuttedIndex = bonesClass.cuttedIndex;

            var moduleCutRagdoll = _goreSimulator.GetSubModuleCut<SubModuleCutRagdoll>(); 
            var isSubRagdoll = moduleCutRagdoll != null && boneAmount >= moduleCutRagdoll.minimumBoneAmount; 
            
            // Skinned Children
            var detachedSkinnedChildren = SkinnedChildren.CreateSkinnedChildrenForBone(_goreSimulator, bonesClass.bone);
            subModuleClass.children.AddRange(detachedSkinnedChildren);

            var multiCutChildClasses = new List<MultiCutChildClass>();

            /********************************************************************************************************************************/
            // Sub Ragdoll
            if (isSubRagdoll)
            {
                validateContinue = RagdollUtility.ExecuteRagdollCut(_goreSimulator, moduleCutRagdoll, bonesClass, cutIndexClass, executionCutClass,
                    subModuleClass, out goreMultiCut);
                if (!validateContinue) return string.Empty;

                detachedObject = goreMultiCut.gameObject;
                var detachedGoreBones = detachedObject.GetComponentsInChildren<GoreBone>();
                for (var i = 0; i < detachedGoreBones.Length; i++)
                {
                    detachedGoreBones[i]._goreMultiCut = goreMultiCut;
                    detachedGoreBones[i].multiCut = true;
                }

                for (var i = 0; i < innerChunkClasses.Count; i++)
                    multiCutChildClasses.Add(new MultiCutChildClass
                    {
                        chunkClass = innerChunkClasses[i],
                        childObject = subModuleClass.subModuleObjectClasses[0].obj
                    });
            }
            /********************************************************************************************************************************/
            else
            {
                detachedObject = new GameObject(_goreSimulator.gameObject.name + " - " + bonesClass.bone.name + " - " + cutIndexClass.cutIndex);
                detachedObject.transform.SetPositionAndRotation(bonesClass.bone.position, _goreSimulator.smr.transform.rotation);
                goreMultiCut = detachedObject.AddComponent<GoreMultiCut>();
                goreMultiCut._goreSimulator = _goreSimulator;
                goreMultiCut.bakedMesh = _goreSimulator.bakedMesh;

                var mass = 1f;
                if (bonesClass.goreBone._rigidbody != null) mass = bonesClass.goreBone._rigidbody.mass;

                var instancedMaterials = _goreSimulator.GetInstancedMaterialsStatic();

                for (var i = 0; i < innerChunkClasses.Count; i++)
                {
                    var subModuleObjectClass = CutUtility.CreateSubModuleObjectClass(goreMultiCut, innerChunkClasses[i], _goreSimulator.smr.transform,
                        bakedVertices, bakedNormals, bonesClass.goreBone.boneTag);
                    currentPoolableObjects.Add(subModuleObjectClass.obj);
                    subModuleObjectClass.obj.transform.SetParent(detachedObject.transform);
                    subModuleObjectClass.renderer.materials = instancedMaterials;
                    subModuleObjectClass.mass = mass;
                    subModuleClass.subModuleObjectClasses.Add(subModuleObjectClass);
                }

                for (var i = 0; i < innerChunkClasses.Count; i++)
                    multiCutChildClasses.Add(new MultiCutChildClass
                    {
                        chunkClass = innerChunkClasses[i],
                        childObject = subModuleClass.subModuleObjectClasses[i].obj
                    });

                subModuleClass.cutPosition = subModuleClass.subModuleObjectClasses[0].cutCenters[0];

                subModuleClass.cutDirection = CutUtility.GetCutDirection(subModuleClass.subModuleObjectClasses[0].obj.transform,
                    innerChunkClasses[0].mesh.bounds.center, subModuleClass.cutPosition);
                
                var fixedChildren = SkinnedChildren.CreateFixedChildrenForBone(_goreSimulator, bonesClass.bone);
                for (int i = 0; i < fixedChildren.Count; i++) fixedChildren[i].parent = detachedObject.transform;
            }

            /********************************************************************************************************************************/
            
            currentDestroyableObjects.Add(detachedObject);
            _goreSimulator.AddDestroyableObject(detachedObject);

            subModuleClass.parent = detachedObject;
            subModuleClass.centerBone = bonesClass.bone;

            goreMultiCut.bonesClass = bonesClass;
            goreMultiCut.CreateMultiCutChildClassDict(multiCutChildClasses);

            for (var k = 0; k < _goreSimulator.cutModules.Count; k++)
            {
                if (!_goreSimulator.cutModules[k].moduleActive) continue;
                _goreSimulator.cutModules[k].ExecuteModuleCut(subModuleClass);
            }

            for (var i = 0; i < bonesClass.directBoneChildren.Count; i++) bonesClass.directBoneChildren[i].gameObject.SetActive(false);

            _goreSimulator.smr.sharedMesh = _goreSimulator.centerMesh;

            ExecutionClassesUtility.ReleaseSubModuleClass(subModuleClass);
            ExecutionClassesUtility.ReleaseExecutionCutClass(executionCutClass);

            return bonesClass.bone.name;
        }
    }
}