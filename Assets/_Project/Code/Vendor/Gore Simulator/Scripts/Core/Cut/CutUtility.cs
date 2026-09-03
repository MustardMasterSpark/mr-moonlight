// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    internal static class CutUtility
    {
        public static int FindNearestTransformIndex(List<Transform> transforms, Vector3 position)
        {
            var nearestIndex = 0;
            var nearestSqrMagnitude = float.MaxValue;

            for (var i = 0; i < transforms.Count; i++)
            {
                var delta = transforms[i].position - position;
                var sqrMagnitude = delta.sqrMagnitude;

                if (sqrMagnitude < nearestSqrMagnitude)
                {
                    nearestSqrMagnitude = sqrMagnitude;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        public static int GetCutIndex(Transform smrTransform, BonesStorageClass bonesStorageClass, List<Vector3> vertices, Vector3 position)
        {
            if (bonesStorageClass.cutIndexClasses.Count == 1) return 0;

            var chunkCenters = new List<Vector3>();

            for (var i = 0; i < bonesStorageClass.cutIndexClasses.Count; i++)
            {
                var averageIndexes = bonesStorageClass.cutIndexClasses[i].chunkAverageIndexes;

                var sum = Vector3.zero;
                for (var j = 0; j < averageIndexes.Count; j++) sum += vertices[averageIndexes[j]];
                var worldSpace = smrTransform.TransformPoint(sum / averageIndexes.Count);
                chunkCenters.Add(worldSpace);
            }

            var nearestIndex = 0;
            var nearestSqrMagnitude = float.MaxValue;

            for (var i = 0; i < chunkCenters.Count; i++)
            {
                var delta = chunkCenters[i] - position;
                var sqrMagnitude = delta.sqrMagnitude;

                if (sqrMagnitude < nearestSqrMagnitude)
                {
                    nearestSqrMagnitude = sqrMagnitude;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        /********************************************************************************************************************************/

        public static List<ChunkClass> AddBoneHierarchy(GoreSimulator _goreSimulator, Tuple<BonesClass, BonesStorageClass> bonesClassTuple,
            ExecutionCutClass executionCutClass, int cutIndex, out int boneAmount)
        {
            var innerChunkClasses = new List<ChunkClass>();
            boneAmount = 1;

            var bonesClass = bonesClassTuple.Item1;
            var bonesStorageClass = bonesClassTuple.Item2;
            var cutIndexClass = bonesStorageClass.cutIndexClasses[cutIndex];
            var chunkClasses = bonesClass.chunkClasses;

            if (bonesClass.cutted)
            {
                if (bonesClass.cuttedIndex == -1) return innerChunkClasses;
                if (cutIndexClass.cutIndex >= bonesClass.cuttedIndex) return innerChunkClasses;

                AddSameBoneHierarchy(executionCutClass, cutIndexClass.cutIndex, bonesStorageClass,
                    bonesClass.cuttedIndex, chunkClasses, innerChunkClasses, true, new List<string>());
            }
            else
            {
                var notCuttedChildren = new List<Tuple<BonesClass, BonesStorageClass>>();
                var cuttedChildren = new List<Tuple<BonesClass, BonesStorageClass>>();

                for (var i = 0; i < bonesClass.boneChildrenSel.Count; i++)
                {
                    var childBone = bonesClass.boneChildrenSel[i];
                    if (!_goreSimulator.bonesDict.TryGetValue(childBone.name, out var childTuple)) continue;
                    if (!childTuple.Item1.cutted) notCuttedChildren.Add(childTuple);
                    else cuttedChildren.Add(childTuple);
                }

                boneAmount += notCuttedChildren.Count;

                if (cuttedChildren.Count > 0)
                {
                    for (var i = 0; i < notCuttedChildren.Count; i++)
                    {
                        var childTuple = notCuttedChildren[i];
                        var cuttedIndex = childTuple.Item1.cuttedIndex;
                        if (childTuple.Item1.firstChildCutted) cuttedIndex = childTuple.Item2.cutIndexClasses.Count;
                        AddChildBoneHierarchy(executionCutClass, childTuple.Item2,
                            cuttedIndex, childTuple.Item1.chunkClasses, innerChunkClasses, true);
                    }

                    var cuttedChildrenNames = new List<string>(cuttedChildren.Count);
                    for (var i = 0; i < cuttedChildren.Count; i++)
                    {
                        var childTuple = cuttedChildren[i];
                        cuttedChildrenNames.Add(childTuple.Item1.bone.name);
                        if (childTuple.Item1.cuttedIndex == -1) continue;
                        AddChildBoneHierarchy(executionCutClass, childTuple.Item2,
                            childTuple.Item1.cuttedIndex, childTuple.Item1.chunkClasses, innerChunkClasses, true);
                    }

                    var usedCutIndex = bonesClass.cuttedIndex;
                    if (bonesClass.firstChildCutted) usedCutIndex = bonesStorageClass.cutIndexClasses.Count;

                    AddSameBoneHierarchy(executionCutClass, cutIndexClass.cutIndex, bonesStorageClass,
                        usedCutIndex, chunkClasses, innerChunkClasses, true, cuttedChildrenNames);
                }
                else // Add everything from the bonesClass down.
                {
                    executionCutClass.newIndexes.UnionWith(cutIndexClass.indexesCutSide);

                    AddSameBoneHierarchy(executionCutClass, cutIndexClass.cutIndex, bonesStorageClass,
                        -1, chunkClasses, innerChunkClasses, false, new List<string>());

                    for (var i = 0; i < bonesClass.boneChildrenSel.Count; i++)
                    {
                        var childBone = bonesClass.boneChildrenSel[i];

                        if (!_goreSimulator.bonesDict.TryGetValue(childBone.name, out var childTuple)) continue;

                        var cuttedIndex = -1;
                        if (childTuple.Item1.firstChildCutted) cuttedIndex = childTuple.Item2.cutIndexClasses.Count;

                        AddChildBoneHierarchy(executionCutClass, childTuple.Item2, cuttedIndex,
                            childTuple.Item1.chunkClasses, innerChunkClasses, false);
                    }
                }
            }

            return innerChunkClasses;
        }

        /// <summary>
        ///     Adding the chunks from the same bone until the used chunk.
        /// </summary>
        private static void AddSameBoneHierarchy(ExecutionCutClass executionCutClass, int cutIndex, BonesStorageClass usedBonesStorageClass,
            int usedCutIndex, List<ChunkClass> chunkClasses, List<ChunkClass> chunkClassesNew,
            bool unionWithChunks, List<string> cuttedChildrenNames)
        {
            var addExecutionIndexes = true;
            if (usedCutIndex == -1)
            {
                usedCutIndex = usedBonesStorageClass.cutIndexClasses.Count;
                addExecutionIndexes = false;
            }

            for (var i = cutIndex; i < usedCutIndex; i++)
            {
                chunkClassesNew.Add(chunkClasses[i]);
                var usedCutIndexClass = usedBonesStorageClass.cutIndexClasses[i];
                if (!unionWithChunks) continue;

                executionCutClass.newIndexes.UnionWith(usedCutIndexClass.chunkIndexes);

                if (i == usedCutIndex - 1 && addExecutionIndexes)
                    for (var j = 0; j < usedCutIndexClass.cutIndexChildClasses.Count; j++)
                    {
                        var cutChildClass = usedCutIndexClass.cutIndexChildClasses[j];
                        if (!cuttedChildrenNames.Contains(cutChildClass.childBoneName)) continue;
                        executionCutClass.AddExecutionIndexes(cutChildClass.cutIndexes, cutChildClass.sewIndexes, cutChildClass.sewTriangles);
                    }
            }
        }

        /// <summary>
        ///     Adding the children chunks until the last cutted chunk.
        /// </summary>
        private static void AddChildBoneHierarchy(ExecutionCutClass executionCutClass, BonesStorageClass usedBonesStorageClass, int usedCutIndex,
            List<ChunkClass> chunkClasses, List<ChunkClass> chunkClassesNew, bool unionWithChunks)
        {
            var addExecutionIndexes = true;
            if (usedCutIndex == -1)
            {
                usedCutIndex = usedBonesStorageClass.cutIndexClasses.Count;
                addExecutionIndexes = false;
            }

            for (var i = 0; i < usedCutIndex; i++)
            {
                var usedCutIndexClass = usedBonesStorageClass.cutIndexClasses[i];

                chunkClassesNew.Add(chunkClasses[i]);
                if (!unionWithChunks) continue;

                executionCutClass.newIndexes.UnionWith(usedCutIndexClass.chunkIndexes);

                if (i == usedCutIndex - 1 && addExecutionIndexes)
                    for (var j = 0; j < usedCutIndexClass.cutIndexChildClasses.Count; j++)
                    {
                        var cutChildClass = usedCutIndexClass.cutIndexChildClasses[j];
                        executionCutClass.AddExecutionIndexes(cutChildClass.cutIndexes, cutChildClass.sewIndexes, cutChildClass.sewTriangles);
                    }
            }
        }

        /********************************************************************************************************************************/

        public static void RemoveUsedChildren(List<UsedBonesClass> usedBonesClasses, ExecutionCutClass centerExecutionCutClass, BonesClass bonesClass)
        {
            for (var i = usedBonesClasses.Count - 1; i >= 0; i--)
            {
                var usedBone = usedBonesClasses[i].usedBone;
                if (usedBone == bonesClass.bone)
                {
                    centerExecutionCutClass.RemoveIndexes(i);
                    usedBonesClasses.RemoveAt(i);
                    continue;
                }

                if (usedBone.IsChildOf(bonesClass.bone))
                {
                    centerExecutionCutClass.RemoveIndexes(i);
                    usedBonesClasses.RemoveAt(i);
                }
            }
        }

        public static SubModuleObjectClass CreateSubModuleObjectClass(GoreMultiCut goreMultiCut, ChunkClass chunkClass,
            Transform transform, List<Vector3> bakedVertices, List<Vector3> bakedNormals, BoneTag boneTag)
        {
            var subModuleObjClass = new SubModuleObjectClass();

            MeshCutJobs.IndexesSnapshotExplosion(transform, chunkClass, bakedVertices, bakedNormals);

            var detachedChild = ObjectCreationUtility.CreateMeshObject(goreMultiCut._goreSimulator, chunkClass.mesh,
                goreMultiCut.gameObject.name + " - " + chunkClass.boneName + " - " + chunkClass.cutIndexClassIndex, boneTag);

            detachedChild.transform.SetPositionAndRotation(transform.position, transform.rotation);

            subModuleObjClass.obj = detachedChild;
            subModuleObjClass.mesh = chunkClass.mesh;
            if (detachedChild.TryGetComponent<Renderer>(out var _renderer)) subModuleObjClass.renderer = _renderer;
            subModuleObjClass.cutCenters = chunkClass.cutCenters;
            subModuleObjClass.boneTag = boneTag;

            var goreMesh = detachedChild.AddComponent<GoreMesh>();
            goreMesh._goreMultiCut = goreMultiCut;
            goreMesh._boneName = chunkClass.boneName;

            subModuleObjClass.boundsSize = chunkClass.boundsSize;

            return subModuleObjClass;
        }


        public static Vector3 GetCutDirection(Transform transform, Vector3 boundsCenter, Vector3 cutCenter)
        {
            var boundsCenterWorld = transform.TransformPoint(boundsCenter);
            var cutDirection = math.normalizesafe(boundsCenterWorld - cutCenter);

            return cutDirection;
        }

        public static Vector3 GetCutCenter(List<int> cutIndexes, Vector3[] vertices, Transform transform)
        {
            var step = cutIndexes.Count / 4;
            var divisionStep = 0;

            var sum = Vector3.zero;

            for (var i = 0; i < 4; i++) // Just four iterations are needed.
            {
                if (i * step >= cutIndexes.Count) break;
                var index = cutIndexes[i * step];
                sum += vertices[index];
                divisionStep++;
            }

            if (divisionStep == 0) divisionStep = 1;

            var cutCenter = sum / divisionStep;
            cutCenter = transform.TransformPoint(cutCenter);
            return cutCenter;
        }
    }
}