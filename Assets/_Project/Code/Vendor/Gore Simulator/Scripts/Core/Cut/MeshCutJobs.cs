// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    [BurstCompile]
    public static class MeshCutJobs
    {
        public static void GetVerticesCutSides(List<int> indexes, Mesh bakedMesh, Vector3 cutPoint, Vector3 planeNormal, Transform transform,
            out List<int> indexesOnCutSide, out List<int> indexesOnNonCutSide)
        {
            var localCutPoint = transform.InverseTransformPoint(cutPoint);
            var localPlaneNormal = transform.InverseTransformDirection(planeNormal);

            var indexesArray = new NativeArray<int>(indexes.ToArray(), Allocator.TempJob);
            var verticesArray = new NativeArray<Vector3>(bakedMesh.vertices, Allocator.TempJob);

            var indexesOnCutSideNative = new NativeList<int>(Allocator.TempJob);
            var indexesOnNonCutSideNative = new NativeList<int>(Allocator.TempJob);

            var job = new GetVerticesOnCutSideJob
            {
                count = indexes.Count,
                indexes = indexesArray,
                vertices = verticesArray,
                cutPoint = localCutPoint,
                planeNormal = localPlaneNormal,
                indexesOnCutSide = indexesOnCutSideNative,
                indexesOnNonCutSide = indexesOnNonCutSideNative
            };

            var handle = job.Schedule();
            handle.Complete();

            indexesOnCutSide = new List<int>(indexesOnCutSideNative.AsArray());
            indexesOnNonCutSide = new List<int>(indexesOnNonCutSideNative.AsArray());

            indexesArray.Dispose();
            verticesArray.Dispose();
            indexesOnCutSideNative.Dispose();
            indexesOnNonCutSideNative.Dispose();
        }

        [BurstCompile]
        private struct GetVerticesOnCutSideJob : IJob
        {
            [ReadOnly] public int count;
            [ReadOnly] public NativeArray<int> indexes;
            [ReadOnly] public NativeArray<Vector3> vertices;
            [ReadOnly] public float3 cutPoint;
            [ReadOnly] public float3 planeNormal;

            public NativeList<int> indexesOnCutSide;
            public NativeList<int> indexesOnNonCutSide;

            public void Execute()
            {
                for (var i = 0; i < count; i++)
                    if (indexes[i] < vertices.Length)
                    {
                        float3 worldVertex = vertices[indexes[i]]; // Assume the vertex is in the world space
                        var worldVertexToCutPoint = worldVertex - cutPoint;

                        // Dot product > 0 means it's on the positive side of the plane.
                        if (math.dot(worldVertexToCutPoint, planeNormal) > 0)
                            indexesOnCutSide.Add(indexes[i]);
                        else
                            indexesOnNonCutSide.Add(indexes[i]);
                    }
            }
        }

        /********************************************************************************************************************************/


        public static void GetDirectConnections(List<int> indexesCut, List<int> indexesNonCut, Dictionary<int, List<int>> adjacencyList,
            out List<int> connectionsCut)
        {
            var indexesSetNative = new NativeHashSet<int>(indexesNonCut.Count, Allocator.TempJob);
            foreach (var index in indexesNonCut) indexesSetNative.Add(index);


            var trianglesCutNative = new NativeList<int>(0, Allocator.TempJob);
            var connectionsCutNative = new NativeList<int>(0, Allocator.TempJob);

            foreach (var vertexIndex in indexesCut)
                if (adjacencyList.TryGetValue(vertexIndex, out var triangles))
                    foreach (var tri in triangles)
                        trianglesCutNative.Add(tri);

            var findConnectionsJobCut = new FindConnectionsJob
            {
                TrianglesNative = trianglesCutNative,
                IndexesSetNative = indexesSetNative,
                ConnectionsNative = connectionsCutNative
            };
            var cutJobHandle = findConnectionsJobCut.Schedule();
            cutJobHandle.Complete();

            connectionsCut = new List<int>(connectionsCutNative.AsArray());

            trianglesCutNative.Dispose();
            connectionsCutNative.Dispose();
            indexesSetNative.Dispose();
        }


        [BurstCompile]
        private struct FindConnectionsJob : IJob
        {
            [ReadOnly] public NativeList<int> TrianglesNative;
            [ReadOnly] public NativeHashSet<int> IndexesSetNative;
            public NativeList<int> ConnectionsNative;

            public void Execute()
            {
                for (var index = 0; index < TrianglesNative.Length; index++)
                {
                    var adjacentVertex = TrianglesNative[index];
                    if (IndexesSetNative.Contains(adjacentVertex) && !ConnectionsNative.Contains(adjacentVertex))
                        ConnectionsNative.Add(adjacentVertex);
                }
            }
        }


        /********************************************************************************************************************************/


        public static List<int> GetOppositeIndexes(MeshNativeDataClass meshNativeDataClass, List<int> indexes)
        {
            var indexSet = new NativeHashSet<int>(indexes.Count, Allocator.TempJob);
            foreach (var index in indexes) indexSet.Add(index);
            var nativeOppositeIndexes = new NativeList<int>(Allocator.TempJob);

            var job = new GetOppositeIndexJob
            {
                allIndexes = meshNativeDataClass.indexesNative,
                indexSet = indexSet,
                oppositeIndexes = nativeOppositeIndexes
            };

            var handle = job.Schedule();
            handle.Complete();

            var oppositeIndexes = nativeOppositeIndexes.AsArray().ToList();

            nativeOppositeIndexes.Dispose();
            indexSet.Dispose();

            return oppositeIndexes;
        }

        [BurstCompile]
        private struct GetOppositeIndexJob : IJob
        {
            [ReadOnly] public NativeArray<int> allIndexes;
            [ReadOnly] public NativeHashSet<int> indexSet;
            public NativeList<int> oppositeIndexes;

            public void Execute()
            {
                for (var i = 0; i < allIndexes.Length; i++)
                    if (!indexSet.Contains(allIndexes[i]))
                        oppositeIndexes.Add(allIndexes[i]);
            }
        }

        /********************************************************************************************************************************/


        public static void IndexesSnapshotExplosion(Transform smrTransform, ChunkClass chunkClass, 
            List<Vector3> bakedVertices, List<Vector3> bakedNormals)
        {
            var newVertices = new List<Vector3>(chunkClass.mesh.vertexCount);
            chunkClass.mesh.GetVertices(newVertices);
            var cutCenters = new List<Vector3>();
            
            var newNormals = new List<Vector3>(chunkClass.mesh.vertexCount);
            chunkClass.mesh.GetNormals(newNormals);

            for (var i = 0; i < chunkClass.keys.Count; i++)
            {
                var oldIndex = chunkClass.keys[i];
                var newIndex = chunkClass.values[i];
                newVertices[newIndex] = bakedVertices[oldIndex];
                newNormals[newIndex] = bakedNormals[oldIndex];
            }

            // Moving sewIndexes to the middle.
            for (var i = 0; i < chunkClass.indexClasses.Count; i++)
            {
                var indexClass = chunkClass.indexClasses[i];
                if (indexClass.cutIndexes.Count == 0)
                {
                    cutCenters.Add(Vector3.zero);
                    continue;
                }

                var sum = Vector3.zero;
                var division = indexClass.cutIndexes.Count / 4; // Only 4, less expensive.
                for (var j = 0; j < 4; j++)
                {
                    var index = indexClass.cutIndexes[division * j];
                    sum += newVertices[index];
                }

                cutCenters.Add(sum / 4);

                for (var j = 0; j < indexClass.sewIndexes.Count; j++) newVertices[indexClass.sewIndexes[j]] = cutCenters[^1];
            }

            for (var i = 0; i < cutCenters.Count; i++)
            {
                if (cutCenters[i] == Vector3.zero) continue;
                cutCenters[i] = smrTransform.TransformPoint(cutCenters[i]);
            }

            chunkClass.cutCenters = cutCenters;
            chunkClass.mesh.SetVertices(newVertices);
            chunkClass.mesh.SetNormals(newNormals);


            // New: Stitching
            StitchUtility.PrepareStitching(smrTransform, chunkClass, cutCenters,
                out var stitchVerticesList, out var centerVertices, out var cutIndexPositions);

            chunkClass.mesh.OptimizeReorderVertexBuffer(); // Not recalculating normals, otherwise edges are visible.
            chunkClass.mesh.RecalculateTangents();

            StitchUtility.StitchCutMesh(chunkClass, stitchVerticesList, centerVertices, cutIndexPositions);
        }

        public static bool IndexesSnapshotCut(MeshNativeDataClass meshNativeDataClass,
            Mesh originalMesh, ExecutionCutClass executionCutClass, Mesh mesh)
        {
            var cutCenters = new List<Vector3>();
            mesh.triangles = Array.Empty<int>();

            var newIndexes = new List<int>(executionCutClass.newIndexes);

            // Adding sewIndexes to the newIndexes.
            for (var i = 0; i < executionCutClass.sewIndexes.Count; i++) newIndexes.AddRange(executionCutClass.sewIndexes[i]);

            var originalBakedVertices = originalMesh.vertices;
            var bakedVertices = new Vector3[originalBakedVertices.Length];
            Array.Copy(originalBakedVertices, bakedVertices, originalBakedVertices.Length);

            // Moving sewIndexes to the middle.
            for (var i = 0; i < executionCutClass.cutIndexes.Count; i++)
            {
                if (executionCutClass.cutIndexes[i].Count == 0) continue;
                var sum = Vector3.zero;
                var division = executionCutClass.cutIndexes[i].Count / 4; // Only 4, less expensive.
                for (var j = 0; j < 4; j++)
                {
                    var index = executionCutClass.cutIndexes[i][division * j];
                    sum += originalBakedVertices[index];
                }

                cutCenters.Add(sum / 4);
                for (var j = 0; j < executionCutClass.sewIndexes[i].Count; j++)
                    bakedVertices[executionCutClass.sewIndexes[i][j]] = cutCenters[^1];
            }


            var bakedNormals = originalMesh.normals;
            var bakedTangents = originalMesh.tangents;
            var bakedBoneWeights = originalMesh.boneWeights;
            var bakedUV = originalMesh.uv;

            var newVertices = new Vector3[newIndexes.Count];
            var newNormals = new Vector3[newIndexes.Count];
            var newTangents = new Vector4[newIndexes.Count];
            var newBoneWeights = new BoneWeight[newIndexes.Count];
            var newUV = new Vector2[newIndexes.Count];

            var newTrianglesPerSubmesh = new List<NativeList<int>>();
            for (var i = 0; i < originalMesh.subMeshCount; i++) newTrianglesPerSubmesh.Add(new NativeList<int>(Allocator.TempJob));

            var newIndex = 0;

            foreach (var vertexIndex in newIndexes)
            {
                newVertices[newIndex] = bakedVertices[vertexIndex];
                newNormals[newIndex] = bakedNormals[vertexIndex];
                if (vertexIndex < bakedTangents.Length) newTangents[newIndex] = bakedTangents[vertexIndex]; // Added check, tangents.length could be 0
                newBoneWeights[newIndex] = bakedBoneWeights[vertexIndex];
                newUV[newIndex] = bakedUV[vertexIndex];
                newIndex++;
            }


            var newIndexesNative = new NativeArray<int>(newIndexes.ToArray(), Allocator.TempJob);
            var setHashMapIndexesJob = new SetHashMapIndexesJob
            {
                newIndexes = newIndexesNative,
                origToNewMap = meshNativeDataClass.origToNewMapNative
            };
            setHashMapIndexesJob.Schedule().Complete();
            newIndexesNative.Dispose();


            for (var i = 0; i < originalMesh.subMeshCount; i++)
            {
                var triangleMappingJob = new TriangleMappingJob
                {
                    origToNewMap = meshNativeDataClass.origToNewMapNative,
                    bakedTriangles = meshNativeDataClass.trianglesNativePerSubM[i],
                    newTriangles = newTrianglesPerSubmesh[i]
                };
                meshNativeDataClass.jobHandles[i] = triangleMappingJob.Schedule();
            }

            var combinedHandle = JobHandle.CombineDependencies(meshNativeDataClass.jobHandles);
            combinedHandle.Complete();


            var totalSetTrianglesCount = executionCutClass.sewTriangles.Sum(sewTriangles => sewTriangles.Count);
            var newSewTrianglesNative = new NativeArray<int>(totalSetTrianglesCount, Allocator.TempJob);
            var verifyIndexes = new NativeReference<bool>(true, Allocator.TempJob);
            var sewTriangleindex = new NativeReference<int>(Allocator.TempJob);

            for (var i = 0; i < executionCutClass.sewTriangles.Count; i++)
            {
                var sewTriangles = executionCutClass.sewTriangles[i];
                var sewTrianglesNative = new NativeArray<int>(sewTriangles.ToArray(), Allocator.TempJob);

                var fillSewTrianglesJob = new FillSewIndexesJob
                {
                    origToNewMap = meshNativeDataClass.origToNewMapNative,
                    sewTriangles = sewTrianglesNative,
                    newSewTriangles = newSewTrianglesNative,
                    verifyIndexes = verifyIndexes,
                    sewTriangleindex = sewTriangleindex
                };
                fillSewTrianglesJob.Schedule().Complete();

                // if (!fillSewTrianglesJob.verifyIndexes.Value) // Triangle Index not existing!

                sewTrianglesNative.Dispose();
            }

            mesh.SetVertices(newVertices);
            mesh.SetNormals(newNormals);
            mesh.SetTangents(newTangents);
            mesh.SetUVs(0, newUV);
            mesh.boneWeights = newBoneWeights;
            mesh.bindposes = originalMesh.bindposes;
            mesh.subMeshCount = originalMesh.subMeshCount;

            for (var i = 0; i < mesh.subMeshCount; i++)
                mesh.SetTriangles(newTrianglesPerSubmesh[i].AsArray().ToArray(), i);

            mesh.subMeshCount++;
            var newSewTriangles = newSewTrianglesNative.ToArray();
            mesh.SetTriangles(newSewTriangles, mesh.subMeshCount - 1);

            if (Application.isPlaying) mesh.OptimizeReorderVertexBuffer(); // Initialization needs original mesh
            mesh.RecalculateBounds();

            DisposeNatives();

            return true;

            void DisposeNatives()
            {
                for (var i = 0; i < originalMesh.subMeshCount; i++) newTrianglesPerSubmesh[i].Dispose();
                newSewTrianglesNative.Dispose();
                verifyIndexes.Dispose();
                sewTriangleindex.Dispose();
            }
        }


        [BurstCompile]
        private struct SetHashMapIndexesJob : IJob
        {
            public NativeHashMap<int, int> origToNewMap;
            [ReadOnly] public NativeArray<int> newIndexes;

            public void Execute()
            {
                origToNewMap.Clear();
                var newIndex = 0;
                foreach (var index in newIndexes)
                {
                    origToNewMap[index] = newIndex;
                    newIndex++;
                }
            }
        }

        [BurstCompile]
        private struct TriangleMappingJob : IJob
        {
            [ReadOnly] public Mesh.MeshData meshData;
            [ReadOnly] public NativeHashMap<int, int> origToNewMap;
            [ReadOnly] public NativeArray<int> bakedTriangles;
            public NativeList<int> newTriangles;

            public void Execute()
            {
                for (var i = 0; i < bakedTriangles.Length; i += 3)
                    if (origToNewMap.TryGetValue(bakedTriangles[i], out var newIdx1) &&
                        origToNewMap.TryGetValue(bakedTriangles[i + 1], out var newIdx2) &&
                        origToNewMap.TryGetValue(bakedTriangles[i + 2], out var newIdx3))
                    {
                        newTriangles.Add(newIdx1);
                        newTriangles.Add(newIdx2);
                        newTriangles.Add(newIdx3);
                    }
            }
        }

        [BurstCompile]
        private struct FillSewIndexesJob : IJob
        {
            [ReadOnly] public NativeHashMap<int, int> origToNewMap;
            [ReadOnly] public NativeArray<int> sewTriangles;
            public NativeArray<int> newSewTriangles;
            public NativeReference<bool> verifyIndexes;
            public NativeReference<int> sewTriangleindex;

            public void Execute()
            {
                for (var i = 0; i < sewTriangles.Length; i++)
                    if (origToNewMap.TryGetValue(sewTriangles[i], out var newTriangleIndex))
                    {
                        newSewTriangles[sewTriangleindex.Value] = newTriangleIndex;
                        sewTriangleindex.Value++;
                    }
                    else // Triangle Index not existing!
                    {
                        sewTriangleindex.Value++;
                        verifyIndexes.Value = false;
                    }
            }
        }

        /********************************************************************************************************************************/

        public static List<int> GetInfluencingBones(Mesh mesh, List<int> vertexIndexes)
        {
            var boneIndexes = new HashSet<int>();
            var boneWeights = mesh.boneWeights;

            for (var i = 0; i < vertexIndexes.Count; i++)
            {
                var boneWeight = boneWeights[vertexIndexes[i]];

                if (boneWeight.weight0 > 0)
                    boneIndexes.Add(boneWeight.boneIndex0);

                if (boneWeight.weight1 > 0)
                    boneIndexes.Add(boneWeight.boneIndex1);

                if (boneWeight.weight2 > 0)
                    boneIndexes.Add(boneWeight.boneIndex2);

                if (boneWeight.weight3 > 0)
                    boneIndexes.Add(boneWeight.boneIndex3);
            }

            return boneIndexes.ToList();
        }
    }
}