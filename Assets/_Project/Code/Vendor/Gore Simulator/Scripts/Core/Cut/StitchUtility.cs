// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Utility;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    internal static class StitchUtility
    {
        public static void PrepareStitching(Transform smrTransform, ChunkClass chunkClass, List<Vector3> cutCenters,
            out List<List<Vector3>> stitchVerticesList, out List<Vector3> centerVertices, out List<Vector3> cutIndexPositions)
        {
            var chunkVertices = chunkClass.mesh.vertices;

            stitchVerticesList = new List<List<Vector3>>();
            centerVertices = new List<Vector3>();
            cutIndexPositions = new List<Vector3>();

            for (var i = 0; i < chunkClass.indexClasses.Count; i++)
            {
                var indexClass = chunkClass.indexClasses[i];
                if (indexClass.cutIndexes.Count == 0)
                {
                    stitchVerticesList.Add(new List<Vector3>());
                    centerVertices.Add(Vector3.zero);
                    cutIndexPositions.Add(Vector3.zero);
                    continue;
                }

                var stitchVertices = indexClass.cutIndexes.Select(t => chunkVertices[t]).ToList();
                stitchVerticesList.Add(stitchVertices);

                var centerVertex = smrTransform.InverseTransformPoint(cutCenters[i]);
                centerVertices.Add(centerVertex);

                var cutIndexPosition = chunkVertices[indexClass.cutIndexes[0]];
                cutIndexPositions.Add(cutIndexPosition);
            }
        }

        public static void StitchCutMesh(ChunkClass chunkClass,
            List<List<Vector3>> stitchVerticesList, List<Vector3> centerVertices, List<Vector3> cutIndexPositions)
        {
            var stitchMeshes = new List<Mesh>();

            var boundCenter = chunkClass.mesh.bounds.center;

            for (var i = 0; i < chunkClass.indexClasses.Count; i++)
            {
                var indexClass = chunkClass.indexClasses[i];
                if (indexClass.cutIndexes.Count == 0) continue;

                var stitchVertices = stitchVerticesList[i];
                var centerVertex = centerVertices[i];
                var cutIndexPosition = cutIndexPositions[i];

                var faceDirection = cutIndexPosition - boundCenter;
                stitchMeshes.Add(CreateStitchMesh(stitchVertices, centerVertex, faceDirection));
            }

            var combinedStitchMesh = PGMeshUtility.CombineMeshes(stitchMeshes.ToArray(), true);
            for (var i = 0; i < stitchMeshes.Count; i++) Pool.ReleaseMesh(stitchMeshes[i]);
            var newMesh = PGMeshUtility.CombineMeshesManually(new[] {chunkClass.mesh, combinedStitchMesh});
            Pool.ReleaseMesh(combinedStitchMesh);
            Pool.ReleaseMesh(chunkClass.mesh);
            chunkClass.mesh = newMesh;
        }

        private static Mesh CreateStitchMesh(List<Vector3> stitchVertices, Vector3 centerVertex, Vector3 faceDirection)
        {
            centerVertex += faceDirection.normalized * 0.01f; // Moving before the sew vertices (not perfect).

            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var vertices = new List<Vector3> {centerVertex};
            normals.Add(Vector3.up);

            uvs.Add(new Vector2(0.5f, 0.5f)); // Center vertex

            for (var i = 0; i < stitchVertices.Count; i++)
            {
                vertices.Add(stitchVertices[i]);
                normals.Add(Vector3.up);

                var angle = i * Mathf.PI * 2 / stitchVertices.Count;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f;
                var uv = new Vector2(0.5f, 0.5f) + offset;
                uvs.Add(uv);
            }

            // Triangles
            // First checking which face direction is dominant, then using that for all.
            var dot = 0;
            for (var i = 0; i < stitchVertices.Count; i++)
            {
                var v0 = 0; // Index for the center vertex
                var v1 = i + 1; // Current stitch vertex index
                var v2 = i < stitchVertices.Count - 1
                    ? i + 2
                    : 1; // The next vertex index or, if it's the last vertex, wrap back to the first stitch vertex

                var faceNormal = Vector3.Cross(vertices[v1] - vertices[v0], vertices[v2] - vertices[v0]).normalized;
                if (PGTrigonometryUtility.IsSameDirection(faceNormal, faceDirection)) dot--;
                else dot++;
            }

            for (var i = 0; i < stitchVertices.Count; i++)
            {
                var v0 = 0;
                var v1 = i + 1;
                var v2 = i < stitchVertices.Count - 1 ? i + 2 : 1;

                if (dot > 0) (v1, v2) = (v2, v1);
                triangles.AddRange(new[] {v0, v1, v2});
            }

            var mesh = new Mesh
            {
                vertices = vertices.ToArray(),
                triangles = triangles.ToArray(),
                uv = uvs.ToArray(),
                normals = normals.ToArray(),
                tangents = new Vector4[vertices.Count]
            };

            mesh.RecalculateTangents();

            return mesh;
        }
    }
}