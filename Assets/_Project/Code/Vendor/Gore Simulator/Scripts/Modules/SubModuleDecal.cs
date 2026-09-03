// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PampelGames.GoreSimulator
{
    [Serializable]
    public class SubModuleDecal : SubModuleBase
    {
        public override string ModuleName()
        {
            return "Decal";
        }

        public override string ModuleInfo()
        {
            return "Projects custom decals on renderers.";
        }

        public override int imageIndex()
        {
            return 0;
        }

        public override bool CompatibleRagdoll()
        {
            return false;
        }


        public override void ModuleAdded(Type type)
        {
            base.ModuleAdded(type);
#if UNITY_EDITOR
            uvTransformation = _goreSimulator._defaultReferences.uvTransformation;
#endif
        }

        /********************************************************************************************************************************/

        public float radius = 0.25f;
        public float strength = 0.75f;

        public Material uvTransformation;

        private Material centerMaterial;
        private CommandBuffer command;
        private RenderTexture centerMaskRenderTexture;

        private readonly Dictionary<GameObject, RenderTexture> renderTextures = new();

        private const int TEXTURE_RESOLUTION = 2048;
        private const float HARDNESS = 1f;

        private bool centerMeshApplied;
        private List<Material> createdMaterials = new();

        /********************************************************************************************************************************/

        public override void Initialize()
        {
            base.Initialize();
            command = new CommandBuffer();
            command.name = "UV Space Renderer";
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            ClearCreatedAssets();
            centerMaskRenderTexture = new RenderTexture(TEXTURE_RESOLUTION, TEXTURE_RESOLUTION, 0);
            centerMeshApplied = false;
        }

        /********************************************************************************************************************************/

        public override void ExecuteModuleCut(SubModuleClass subModuleClass)
        {
            var cutCenters = new List<Vector3> {subModuleClass.cutPosition};

            // Center
            if (!subModuleClass.multiCut) CreateTriangleIndex(subModuleClass.centerMesh);
            if (!centerMeshApplied && subModuleClass.subModuleObjectClasses.Count > 0)
            {
                // Bug: Can only apply decal to center mesh once, doing it again gives error:
                // SkinnedMeshRenderer: Rendering stopped because the data for mesh (...) does not match the expected mesh data size and vertex stride.
                ApplyMaskToMesh(_goreSimulator.smr, centerMaskRenderTexture, subModuleClass.centerMesh, cutCenters);
                centerMeshApplied = true;
            }

            ExecuteModuleInternal(subModuleClass, cutCenters);
        }

        public override void ExecuteModuleExplosion(SubModuleClass subModuleClass)
        {
            ExecuteModuleInternal(subModuleClass, new List<Vector3>());
        }

        public override void ExecuteModuleRagdoll(List<GoreBone> goreBones)
        {
        }


        /********************************************************************************************************************************/

        private void ExecuteModuleInternal(SubModuleClass subModuleClass, List<Vector3> cutCenters)
        {
            if (subModuleClass.cachedPartsUsed) return;
            
            if (_goreSimulator.AddDecalMaterial())
            {
                _goreSimulator.smr.sharedMaterials[^1].SetTexture(ShaderConstants.MaskTexture, centerMaskRenderTexture);
            }

            var newEntry = false;
            if (!renderTextures.TryGetValue(subModuleClass.parent, out var renderTexture))
            {
                renderTexture = new RenderTexture(TEXTURE_RESOLUTION, TEXTURE_RESOLUTION, 0);
                renderTextures.Add(subModuleClass.parent, renderTexture);
                newEntry = true;
            }


            /********************************************************************************************************************************/
            // Adding the new decal material instance, assuming all child renderer using the same materials.

            var _rendererReference = subModuleClass.subModuleObjectClasses[^1].renderer;
            var materials = _rendererReference.sharedMaterials;

            if (newEntry)
            {
                var newDecalMaterial = Object.Instantiate(_goreSimulator.decalMaterial);
                createdMaterials.Add(newDecalMaterial);
                if (Application.isPlaying)
                {
                    newDecalMaterial.SetTexture(ShaderConstants.MaskTexture, renderTexture);
                }
                else
                {
                    var baseTexture = newDecalMaterial.GetTexture(ShaderConstants.BaseMap);
                    newDecalMaterial.SetTexture(ShaderConstants.MaskTexture, baseTexture);
                }

                var decalMaterialName = _goreSimulator.decalMaterial.name;
                var decalMaterialAdded = false;
                for (var j = 0; j < materials.Length; j++)
                {
                    if (!materials[j].name.Contains(decalMaterialName)) continue;
                    materials[j] = newDecalMaterial;
                    decalMaterialAdded = true;
                    break;
                }

                if (!decalMaterialAdded)
                {
                    Array.Resize(ref materials, materials.Length + 1);
                    materials[^1] = newDecalMaterial;
                }
            }
            /********************************************************************************************************************************/

            var trianglesAlreadySet = TrianglesAlreadySet(subModuleClass.subModuleObjectClasses[0].mesh);
            for (var i = 0; i < subModuleClass.subModuleObjectClasses.Count; i++)
            {
                var subModuleObjectClass = subModuleClass.subModuleObjectClasses[i];
                var _renderer = subModuleObjectClass.renderer;

                _renderer.sharedMaterials = materials;

                if (!subModuleClass.multiCut && !trianglesAlreadySet) CreateTriangleIndex(subModuleObjectClass.mesh);

                var usingCutCenters = cutCenters.Count > 0 ? cutCenters : subModuleObjectClass.cutCenters;
                // if (!subModuleClass.multiCut) // This does nothing?
                //     ApplyMaskToMesh(_renderer, centerMaskRenderTexture, subModuleObjectClass.mesh, usingCutCenters);
                ApplyMaskToMesh(_renderer, renderTexture, subModuleObjectClass.mesh, usingCutCenters);
            }
        }

        /********************************************************************************************************************************/

        private void CreateTriangleIndex(Mesh mesh)
        {
            var allTriangles = mesh.triangles;
            var originalSubmeshCount = mesh.subMeshCount;
            mesh.subMeshCount += 1;
            mesh.SetTriangles(allTriangles, originalSubmeshCount);
        }

        private bool TrianglesAlreadySet(Mesh mesh)
        {
            if (mesh.subMeshCount < 2) return false;

            var lastSubMeshIndices = mesh.GetTriangles(mesh.subMeshCount - 1);
            var countOfLastSubMeshTriangles = lastSubMeshIndices.Length;

            var countOfSubMeshTriangles = 0;
            for (var i = 0; i < mesh.subMeshCount - 1; i++)
            {
                var subMeshIndices = mesh.GetTriangles(i);
                countOfSubMeshTriangles += subMeshIndices.Length;
            }

            return countOfSubMeshTriangles == countOfLastSubMeshTriangles;
        }


        private void ApplyMaskToMesh(Renderer _renderer, RenderTexture renderTexture, Mesh mesh, List<Vector3> cutCenters)
        {
            if (cutCenters.Count == 0) return;

            uvTransformation.SetFloat(ShaderConstants.hardnessID, HARDNESS);
            uvTransformation.SetFloat(ShaderConstants.strengthID, strength);
            uvTransformation.SetFloat(ShaderConstants.radiusID, radius);
            uvTransformation.SetInt(ShaderConstants.blendOpID, (int) BlendOp.Add);

            command.Clear();
            command.SetRenderTarget(renderTexture);

            uvTransformation.SetVector(ShaderConstants.centerID, cutCenters[0]); // First item only
            for (var i = 0; i < mesh.subMeshCount; i++)
                command.DrawRenderer(_renderer, uvTransformation, i);

            Graphics.ExecuteCommandBuffer(command);
        }


        /********************************************************************************************************************************/

        private void ClearCreatedAssets()
        {
            if(!Application.isPlaying) return;
            
            if (centerMaskRenderTexture) centerMaskRenderTexture.Release();
            Object.Destroy(centerMaskRenderTexture);
            foreach (var pair in renderTextures) pair.Value.Release();
            foreach (var pair in renderTextures) Object.Destroy(pair.Value);
            renderTextures.Clear();
            
            for (int i = 0; i < createdMaterials.Count; i++) Object.Destroy(createdMaterials[i]);
            createdMaterials.Clear();
        }

        public override void Destroyed()
        {
            base.Destroyed();
            ClearCreatedAssets();
            command.Release();
        }
    }
}