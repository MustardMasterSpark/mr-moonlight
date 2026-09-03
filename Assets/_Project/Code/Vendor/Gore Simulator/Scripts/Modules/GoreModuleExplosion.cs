// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Utility;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PampelGames.GoreSimulator
{
    [Serializable]
    public class GoreModuleExplosion : GoreModuleBase
    {
        public override string ModuleName()
        {
            return "Explosion";
        }

        public override string ModuleInfo()
        {
            return "Explosion Module: Enables mesh explosion.\n" + "\n" +
                   "GoreSimulator.ExecuteExplosion();";
        }

        public override int imageIndex()
        {
            return 3;
        }

        public override void ClearSubmodules()
        {
            _goreSimulator.explosionModules.Clear();
        }

        public override void Reset(List<BonesClass> bonesClasses)
        {
            if (!_goreSimulator.meshCutInitialized) return;
            base.Reset(bonesClasses);
            for (var i = 0; i < _goreSimulator.explosionModules.Count; i++) _goreSimulator.explosionModules[i].Reset();
            ToggleCollider(_goreSimulator.goreBones, true);

            if (cachedPartsUsed)
                for (var i = 0; i < subModuleCachedParts.meshPartsParent.meshParts.Count; i++)
                    subModuleCachedParts.meshPartsParent.meshParts[i].gameObject.SetActive(false);
        }


        private readonly List<GameObject> currentPoolableObjects = new();
        private readonly List<GameObject> currentDestroyableObjects = new();

        private bool cachedPartsUsed;
        private SubModuleCachedParts subModuleCachedParts;

        public override void Initialize()
        {
            base.Initialize();

            if (Application.isPlaying)
            {
                subModuleCachedParts = _goreSimulator.explosionModules.OfType<SubModuleCachedParts>().FirstOrDefault();
                if (subModuleCachedParts != null)
                {
                    for (var i = 0; i < subModuleCachedParts.meshPartsParent.meshParts.Count; i++)
                        subModuleCachedParts.meshPartsParent.meshParts[i].gameObject.SetActive(false);

                    cachedPartsUsed = true;
                }
            }
        }

        public override void FinalizeExecution()
        {
            base.FinalizeExecution();

            for (var k = 0; k < _goreSimulator.explosionModules.Count; k++)
            {
                if (!_goreSimulator.explosionModules[k].moduleActive) continue;
                _goreSimulator.explosionModules[k].FinalizeExecution(
                    new List<GameObject>(currentPoolableObjects), new List<GameObject>(currentDestroyableObjects));
            }
        }

        /********************************************************************************************************************************/

        public List<GameObject> ExecuteExplosion(Vector3 position, float force)
        {
            currentPoolableObjects.Clear();

            ToggleCollider(_goreSimulator.goreBones, false);

            var subModuleClass = ExecutionClassesUtility.CreateSubModuleClass();
            subModuleClass.parent = _goreSimulator.gameObject;
            subModuleClass.children = _goreSimulator.nonBoneChildren;
            subModuleClass.position = position;
            subModuleClass.centerPosition = _goreSimulator.smr.bounds.center;

            // Skinned Children
            if (Application.isPlaying)
            {
                var detachedSkinnedChildren = SkinnedChildren.CreateSkinnedChildren(_goreSimulator);
                subModuleClass.children.AddRange(detachedSkinnedChildren);
            }
            
            // Fixed Children
            if (Application.isPlaying)
            {
                var fixedChildren = SkinnedChildren.CreateFixedChildren(_goreSimulator);
                subModuleClass.children.AddRange(fixedChildren);
            }

            if (cachedPartsUsed && Application.isPlaying)
                currentPoolableObjects.AddRange(ExecuteCachedExplosion(subModuleClass, position, force));
            else
                currentPoolableObjects.AddRange(ExecuteDefaultExplosion(subModuleClass, position, force));


            _goreSimulator.smr.enabled = false;

            for (var k = 0; k < _goreSimulator.explosionModules.Count; k++)
            {
                if (!_goreSimulator.explosionModules[k].moduleActive) continue;
                _goreSimulator.explosionModules[k].ExecuteModuleExplosion(subModuleClass);
            }

            if (Application.isPlaying)
                foreach (var child in _goreSimulator.nonBoneChildren)
                    child.parent = null;

            ExecutionClassesUtility.ReleaseSubModuleClass(subModuleClass);

            return currentPoolableObjects.ToList();
        }

        private List<GameObject> ExecuteDefaultExplosion(SubModuleClass subModuleClass, Vector3 position, float force)
        {
            var explosionParts = new List<GameObject>();

            _goreSimulator.AddCutMaterial();

            _goreSimulator.smr.sharedMesh = _goreSimulator.originalMesh;
            _goreSimulator.smr.BakeMesh(_goreSimulator.bakedMesh);

            subModuleClass.centerMesh = _goreSimulator.bakedMesh;
            var boundsCenter = _goreSimulator.bakedMesh.bounds.center;
            subModuleClass.centerPosition = _goreSimulator.smr.transform.TransformPoint(boundsCenter);

            var bakedVertices = PGMeshUtility.CreateVertexList(_goreSimulator.bakedMesh);
            var bakedNormals = new List<Vector3>(bakedVertices.Count);
            _goreSimulator.bakedMesh.GetNormals(bakedNormals);

            for (var i = 0; i < _goreSimulator.bonesClasses.Count; i++)
            {
                var bonesClass = _goreSimulator.bonesClasses[i];
                var chunkClasses = bonesClass.chunkClasses;

                var mass = 1f;
                if (bonesClass.goreBone._rigidbody != null) mass = bonesClass.goreBone._rigidbody.mass;

                for (var j = 0; j < chunkClasses.Count; j++)
                {
                    if (bonesClass.cutted && j >= bonesClass.cuttedIndex) continue;

                    MeshCutJobs.IndexesSnapshotExplosion(_goreSimulator.smr.transform, chunkClasses[j], bakedVertices, bakedNormals);

                    var detachedObj = ObjectCreationUtility.CreateMeshObject(_goreSimulator, chunkClasses[j].mesh,
                        _goreSimulator.gameObject.name + " - " + chunkClasses[j].boneName + " - " + chunkClasses[j].cutIndexClassIndex,
                        bonesClass.goreBone.boneTag);

#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        var meshPart = detachedObj.AddComponent<MeshPart>();
                        meshPart.boneName = bonesClass.bone.name;
                        meshPart.bonePosition = bonesClass.bone.transform.position;
                        meshPart.boneRotation = bonesClass.bone.transform.eulerAngles;
                        meshPart.meshFilter = detachedObj.GetComponent<MeshFilter>();
                        meshPart.meshRenderer = detachedObj.GetComponent<MeshRenderer>();
                        EditorUtility.SetDirty(meshPart);
                    }
#endif

                    explosionParts.Add(detachedObj);

                    subModuleClass.subModuleObjectClasses.Add(new SubModuleObjectClass());
                    var subModuleObjClass = subModuleClass.subModuleObjectClasses[^1];

                    if (detachedObj.TryGetComponent<Renderer>(out var renderer))
                    {
                        subModuleObjClass.renderer = renderer;
                        renderer.sharedMaterials = _goreSimulator.GetInstancedMaterialsStatic();
                    }

                    var smrTransform = _goreSimulator.smr.transform;
                    detachedObj.transform.SetPositionAndRotation(smrTransform.position, smrTransform.rotation);

                    subModuleObjClass.obj = detachedObj;
                    subModuleObjClass.mesh = chunkClasses[j].mesh;
                    subModuleObjClass.cutCenters = chunkClasses[j].cutCenters;
                    subModuleObjClass.boneTag = bonesClass.goreBone.boneTag;

                    var worldCenter = subModuleObjClass.obj.transform.TransformPoint(chunkClasses[j].mesh.bounds.center);
                    subModuleObjClass.centerPosition = worldCenter;
                    if (!Mathf.Approximately(force, 0f)) subModuleObjClass.force = (worldCenter - position).normalized * force;

                    subModuleObjClass.mass = mass;
                    subModuleObjClass.boundsSize = chunkClasses[j].boundsSize;
                }
            }

            return explosionParts;
        }

        private List<GameObject> ExecuteCachedExplosion(SubModuleClass subModuleClass, Vector3 position, float force)
        {
            var explosionParts = new List<GameObject>();
            subModuleClass.cachedPartsUsed = true;

            for (var i = 0; i < subModuleCachedParts.meshPartsParent.meshParts.Count; i++)
            {
                var meshPart = subModuleCachedParts.meshPartsParent.meshParts[i];

                if (!_goreSimulator.bonesDict.TryGetValue(meshPart.boneName, out var bonesClassTuple))
                {
                    meshPart.gameObject.SetActive(false);
                    continue;
                }

                if (bonesClassTuple.Item1.cutted) continue;

                var bone = bonesClassTuple.Item1.bone;

                meshPart.transform.position = bone.transform.position;
                meshPart.transform.eulerAngles = bone.transform.eulerAngles;
                meshPart.gameObject.SetActive(true);

                subModuleClass.subModuleObjectClasses.Add(new SubModuleObjectClass());
                var subModuleObjClass = subModuleClass.subModuleObjectClasses[^1];

                subModuleObjClass.obj = meshPart.gameObject;
                subModuleObjClass.mesh = meshPart.meshFilter.mesh;
                subModuleObjClass.renderer = meshPart.meshRenderer;

                if (!Mathf.Approximately(force, 0f)) subModuleObjClass.force = (meshPart.transform.position - position).normalized * force;

                explosionParts.Add(meshPart.gameObject);
            }

            subModuleCachedParts.meshPartsParent.gameObject.SetActive(true);

            return explosionParts;
        }

        /********************************************************************************************************************************/

        public void ToggleCollider(IEnumerable<GoreBone> goreBones, bool active)
        {
            if (!_goreSimulator.colliderInitialized) return;
            foreach (var goreBone in goreBones) goreBone._collider.enabled = active;
        }
    }
}