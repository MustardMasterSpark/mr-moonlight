// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PampelGames.Shared.Utility
{
    public static class PGSkinnedMeshUtility
    {
        /// <summary>
        ///     Checks if the specified bone in a SkinnedMeshRenderer has any attached vertex weights.
        /// </summary>
        /// <param name="smr">The SkinnedMeshRenderer to check.</param>
        /// <param name="boneName">The name of the bone to check for weights.</param>
        /// <returns>True if the specified bone has weights, otherwise false.</returns>
        public static bool DoesBoneHaveWeights(SkinnedMeshRenderer smr, string boneName)
        {
            return DoesBoneHaveWeightsInternal(smr, boneName);
        }

        /// <summary>
        ///     Combines multiple SkinnedMeshRenderers into a single SkinnedMeshRenderer.
        /// </summary>
        /// <param name="rootObj">The root GameObject that contains the SkinnedMeshRenderers to combine.</param>
        /// <param name="renderers">A list of SkinnedMeshRenderers to combine.</param>
        /// <param name="saveMesh">Determines whether to save the combined mesh as an asset.</param>
        /// <returns>Returns true if the mesh combination is successful; otherwise, returns false.</returns>
        public static bool CombineSkinnedMeshes(GameObject rootObj, List<SkinnedMeshRenderer> renderers, bool saveMesh)
        {
            return CombineSkinnedMeshesInternal(rootObj, renderers, saveMesh);
        }

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/


        private struct MeshCombineData
        {
            public Mesh mesh;
            public Matrix4x4 transform;
            public SkinnedMeshRenderer renderer;
        }


        private static bool CombineSkinnedMeshesInternal(GameObject rootObj, List<SkinnedMeshRenderer> renderers, bool saveMesh)
        {
            var combineData = new List<MeshCombineData>();
            var uniqueBones = new List<Transform>();
            var uniqueBindPoses = new List<Matrix4x4>();
            var allBoneWeights = new List<BoneWeight>();
            var boneLookup = new Dictionary<Transform, int>();

            /********************************************************************************************************************************/
            // Gather mesh data

            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                var mesh = renderer.sharedMesh;
                if (!mesh) continue;

                combineData.Add(new MeshCombineData
                {
                    mesh = mesh,
                    transform = renderer.transform.localToWorldMatrix,
                    renderer = renderer
                });

                // Local remapping for this renderer
                var boneRemap = new int[renderer.bones.Length];

                // Build unique bones
                for (var i = 0; i < renderer.bones.Length; i++)
                {
                    var bone = renderer.bones[i];
                    if (!boneLookup.TryGetValue(bone, out var globalIndex))
                    {
                        globalIndex = uniqueBones.Count;
                        boneLookup.Add(bone, globalIndex);
                        uniqueBones.Add(bone);
                        uniqueBindPoses.Add(mesh.bindposes[i]);
                    }

                    boneRemap[i] = globalIndex;
                }

                // Remap weights
                var sourceWeights = mesh.boneWeights;

                for (var i = 0; i < sourceWeights.Length; i++)
                {
                    var bw = sourceWeights[i];
                    bw.boneIndex0 = boneRemap[bw.boneIndex0];
                    bw.boneIndex1 = boneRemap[bw.boneIndex1];
                    bw.boneIndex2 = boneRemap[bw.boneIndex2];
                    bw.boneIndex3 = boneRemap[bw.boneIndex3];
                    allBoneWeights.Add(bw);
                }
            }

            /********************************************************************************************************************************/
            // Combine geometry

            var meshCombined = CombineMeshes(combineData, out var finalMaterials);

            if (allBoneWeights.Count != meshCombined.vertexCount)
            {
                Debug.LogError("Bone weight count does not match vertex count.");
                return false;
            }

            /********************************************************************************************************************************/
            // Blend Shapes

            var vertexOffset = 0;

            foreach (var renderer in renderers)
            {
                if (!renderer)
                    continue;

                var sourceMesh = renderer.sharedMesh;

                if (!sourceMesh)
                    continue;

                for (var shapeIndex = 0; shapeIndex < sourceMesh.blendShapeCount; shapeIndex++)
                {
                    var shapeName = sourceMesh.GetBlendShapeName(shapeIndex);

                    // Prevent duplicate names
                    if (meshCombined.GetBlendShapeIndex(shapeName) >= 0)
                        continue;

                    for (var frameIndex = 0; frameIndex < sourceMesh.GetBlendShapeFrameCount(shapeIndex); frameIndex++)
                    {
                        var frameWeight = sourceMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);

                        var deltaVertices = new Vector3[sourceMesh.vertexCount];
                        var deltaNormals = new Vector3[sourceMesh.vertexCount];
                        var deltaTangents = new Vector3[sourceMesh.vertexCount];

                        sourceMesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                        var finalDeltaVertices = new Vector3[meshCombined.vertexCount];
                        var finalDeltaNormals = new Vector3[meshCombined.vertexCount];
                        var finalDeltaTangents = new Vector3[meshCombined.vertexCount];

                        Array.Copy(deltaVertices, 0, finalDeltaVertices, vertexOffset, deltaVertices.Length);
                        Array.Copy(deltaNormals, 0, finalDeltaNormals, vertexOffset, deltaNormals.Length);
                        Array.Copy(deltaTangents, 0, finalDeltaTangents, vertexOffset, deltaTangents.Length);

                        meshCombined.AddBlendShapeFrame(shapeName, frameWeight, finalDeltaVertices, finalDeltaNormals, finalDeltaTangents);
                    }
                }

                vertexOffset += sourceMesh.vertexCount;
            }

            /********************************************************************************************************************************/

            meshCombined.boneWeights = allBoneWeights.ToArray();
            meshCombined.bindposes = uniqueBindPoses.ToArray();

            meshCombined.RecalculateBounds();

            var combinedRenderer = rootObj.GetComponent<SkinnedMeshRenderer>();
            if (!combinedRenderer) combinedRenderer = rootObj.AddComponent<SkinnedMeshRenderer>();
            combinedRenderer.sharedMesh = meshCombined;
            combinedRenderer.sharedMaterials = finalMaterials;
            combinedRenderer.bones = uniqueBones.ToArray();
            combinedRenderer.rootBone = GetMostUpperRootBone(renderers);

#if UNITY_EDITOR
            if (saveMesh)
                if (!SaveMesh(meshCombined))
                    return false;
#endif

            return true;
        }


        /********************************************************************************************************************************/

        private static Mesh CombineMeshes(List<MeshCombineData> combineInstances, out Material[] materials)
        {
            var finalMesh = new Mesh();

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector2>();
            var colors = new List<Color>();

            var materialToSubMesh = new Dictionary<Material, int>();
            var subMeshTriangles = new List<List<int>>();
            var finalMaterials = new List<Material>();
            var vertexOffset = 0;

            foreach (var ci in combineInstances)
            {
                var mesh = ci.mesh;
                Renderer renderer = ci.renderer;

                foreach (var v in mesh.vertices) vertices.Add(v);
                foreach (var n in mesh.normals) normals.Add(n);
                foreach (var t in mesh.tangents) tangents.Add(t);

                uv0.AddRange(mesh.uv);

                if (mesh.uv2.Length > 0) uv1.AddRange(mesh.uv2);
                if (mesh.colors.Length > 0) colors.AddRange(mesh.colors);

                // Submeshes
                for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                {
                    var material = renderer.sharedMaterials[subMeshIndex];

                    if (!materialToSubMesh.TryGetValue(material, out var finalSubMeshIndex))
                    {
                        finalSubMeshIndex = subMeshTriangles.Count;
                        materialToSubMesh.Add(material, finalSubMeshIndex);
                        subMeshTriangles.Add(new List<int>());
                        finalMaterials.Add(material);
                    }

                    var triangles = mesh.GetTriangles(subMeshIndex);
                    for (var i = 0; i < triangles.Length; i++)
                        subMeshTriangles[finalSubMeshIndex]
                            .Add(triangles[i] + vertexOffset);
                }

                vertexOffset += mesh.vertexCount;
            }

            finalMesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            finalMesh.SetVertices(vertices);

            if (normals.Count == vertices.Count) finalMesh.SetNormals(normals);
            if (tangents.Count == vertices.Count) finalMesh.SetTangents(tangents);
            if (uv0.Count == vertices.Count) finalMesh.SetUVs(0, uv0);
            if (uv1.Count == vertices.Count) finalMesh.SetUVs(1, uv1);
            if (colors.Count == vertices.Count) finalMesh.SetColors(colors);
            finalMesh.subMeshCount = subMeshTriangles.Count;
            for (var i = 0; i < subMeshTriangles.Count; i++) finalMesh.SetTriangles(subMeshTriangles[i], i);
            finalMesh.RecalculateBounds();
            materials = finalMaterials.ToArray();
            return finalMesh;
        }

        private static bool DoesBoneHaveWeightsInternal(SkinnedMeshRenderer smr, string boneName)
        {
            var mesh = smr.sharedMesh;
            var boneWeights = mesh.boneWeights;
            var boneIndicesDictionary = new Dictionary<int, string>();
            for (var i = 0; i < smr.bones.Length; i++) boneIndicesDictionary[i] = smr.bones[i].name;

            var boneIndicesWithWeights = new HashSet<int>();
            foreach (var bw in boneWeights)
            {
                boneIndicesWithWeights.Add(bw.boneIndex0);
                boneIndicesWithWeights.Add(bw.boneIndex1);
                boneIndicesWithWeights.Add(bw.boneIndex2);
                boneIndicesWithWeights.Add(bw.boneIndex3);
            }

            foreach (var boneIndex in boneIndicesWithWeights)
                if (boneIndicesDictionary.ContainsKey(boneIndex) && boneIndicesDictionary[boneIndex] == boneName)
                    return true;

            return false;
        }

        /********************************************************************************************************************************/

        private static Transform GetMostUpperRootBone(List<SkinnedMeshRenderer> renderers)
        {
            var allBones = new HashSet<Transform>(renderers.SelectMany(renderer => GetBonesHierarchy(renderer.rootBone)));
            var upperRootBone = allBones.FirstOrDefault(bone => allBones.All(otherBone => otherBone == bone || !IsChildOf(bone, otherBone)));

            if (upperRootBone == null) return GetMostUpperRootBoneLegacy(renderers);

            return upperRootBone;

            IEnumerable<Transform> GetBonesHierarchy(Transform root)
            {
                var bones = new List<Transform> {root};
                for (var i = 0; i < bones.Count; i++)
                    foreach (Transform child in bones[i])
                        bones.Add(child);
                return bones;
            }

            bool IsChildOf(Transform child, Transform parent)
            {
                while (child.parent != null)
                {
                    if (child.parent == parent) return true;
                    child = child.parent;
                }

                return false;
            }
        }

        private static Transform GetMostUpperRootBoneLegacy(List<SkinnedMeshRenderer> renderers)
        {
            Transform mostUpperRootBone = null;
            var highestDepth = int.MaxValue;

            foreach (var renderer in renderers)
            {
                var depth = 0;
                var parent = renderer.rootBone;

                while (parent != null)
                {
                    depth++;
                    parent = parent.parent;
                }

                if (depth < highestDepth)
                {
                    highestDepth = depth;
                    mostUpperRootBone = renderer.rootBone;
                }
            }

            return mostUpperRootBone;
        }


#if UNITY_EDITOR
        private static bool SaveMesh(Mesh mesh)
        {
            var defaultPath = "Assets/";
            var defaultName = "CombinedSkinnedMesh.asset";
            var message = "Save Combined Skinned Mesh";
            var defaultExtension = "asset";

            var savePath = EditorUtility.SaveFilePanelInProject(message, defaultName, defaultExtension,
                "Please enter a file name to save the mesh to.", defaultPath);
            if (string.IsNullOrEmpty(savePath)) return false;

            AssetDatabase.CreateAsset(mesh, savePath);
            AssetDatabase.SaveAssets();
            return true;
        }
#endif
    }
}