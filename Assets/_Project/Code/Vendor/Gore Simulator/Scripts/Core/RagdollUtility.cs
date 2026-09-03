// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    internal static class RagdollUtility
    {
        public static void ToggleRagdoll(IEnumerable<GoreBone> goreBones, bool active, SkinnedMeshRenderer smr, bool updateWhenOffscreenDefault)
        {
            if (active)
            {
                foreach (var goreBone in goreBones)
                {
                    goreBone._rigidbody.isKinematic = false;
                    goreBone._rigidbody.linearVelocity = Vector3.zero;
                    goreBone._rigidbody.angularVelocity = Vector3.zero;
                }

                if(Application.isPlaying) smr.updateWhenOffscreen = true;
            }
            else
            {
                foreach (var goreBone in goreBones)
                    // goreBone._rigidbody.velocity = Vector3.zero;
                    // goreBone._rigidbody.angularVelocity = Vector3.zero;
                    goreBone._rigidbody.isKinematic = true;

                if(Application.isPlaying) smr.updateWhenOffscreen = updateWhenOffscreenDefault;
            }
        }

        /********************************************************************************************************************************/
        // Sub-module Ragdoll Cut 
        /********************************************************************************************************************************/

        public static bool ExecuteRagdollCut(GoreSimulator _goreSimulator, SubModuleCutRagdoll moduleCutRagdoll,
            BonesClass bonesClass, CutIndexClass cutIndexClass, ExecutionCutClass executionCutClass, SubModuleClass subModuleClass,
            out GoreMultiCut goreMultiCut)
        {
            goreMultiCut = null;
            var indexesStorageClass = bonesClass.indexesStorageClass;

            if (indexesStorageClass.cutMeshes[cutIndexClass.cutIndex])
                indexesStorageClass.cutMeshes[cutIndexClass.cutIndex].Clear();
            else
                indexesStorageClass.cutMeshes[cutIndexClass.cutIndex] = new Mesh();

            var validateContinue = MeshCutJobs.IndexesSnapshotCut(_goreSimulator.meshNativeDataClass,
                _goreSimulator.originalMesh, executionCutClass, indexesStorageClass.cutMeshes[cutIndexClass.cutIndex]);
            if (!validateContinue) return false;

            var detachedObject = CreateRagdollObject(_goreSimulator.smr,
                _goreSimulator.gameObject.name + " - " + bonesClass.bone.name + " - " + cutIndexClass.cutIndex + " - Ragdoll");

            goreMultiCut = detachedObject.gameObject.AddComponent<GoreMultiCut>();
            goreMultiCut.status = Enums.MultiCutStatus.Ragdoll;
            goreMultiCut._goreSimulator = _goreSimulator;
            goreMultiCut.bakedMesh = _goreSimulator.bakedMesh;

            subModuleClass.subRagdoll = true;

            var cutCenter = CutUtility.GetCutCenter(executionCutClass.cutIndexes[^1], _goreSimulator.bakedMesh.vertices, detachedObject.transform);

            subModuleClass.cutPosition = cutCenter;
            subModuleClass.cutDirection = CutUtility.GetCutDirection(_goreSimulator.smr.transform,
                indexesStorageClass.cutMeshes[cutIndexClass.cutIndex].bounds.center, subModuleClass.cutPosition);

            moduleCutRagdoll.ExecuteRagdollSubModule(detachedObject, indexesStorageClass.cutMeshes[cutIndexClass.cutIndex], cutIndexClass, bonesClass,
                cutCenter, subModuleClass, goreMultiCut);

            return true;
        }

        private static GameObject CreateRagdollObject(SkinnedMeshRenderer smr, string objName)
        {
            var smrTransform = smr.transform;
            var originalSMRParent = smrTransform.parent;
            var originalRootBoneParent = smr.rootBone.parent;

            var tempObj = new GameObject();
            tempObj.transform.SetPositionAndRotation(smrTransform.position, smrTransform.rotation);

            smr.transform.SetParent(tempObj.transform);
            smr.rootBone.SetParent(tempObj.transform);
            var ragdollCutCopy = Object.Instantiate(tempObj);
            ragdollCutCopy.name = objName;

            smr.transform.SetParent(originalSMRParent);
            smr.rootBone.SetParent(originalRootBoneParent);

            Object.Destroy(tempObj);
            return ragdollCutCopy;
        }
    }
}