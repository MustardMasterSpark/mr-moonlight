// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PampelGames.Shared.Utility
{
    [AddComponentMenu("Pampel Games/Shared/Mesh Modifier")]
    [PGEditorAuto]
    public class MeshModifier : MonoBehaviour
    {
        
        public MeshFilter meshFilter;

        [PGMargin(9f, float.MinValue)]
        public Vector3 scaleFactor = Vector3.one;
        
        [PGButtonMethod(nameof(ScaleMesh), 20f)]
        public string scaleMesh;
        
        [PGMargin(9f, float.MinValue)]
        [PGButtonMethod(nameof(SetPivotToLowestVertex), 20f)]
        public string setPivotToLowestVertex;

        [PGMargin(9f, float.MinValue)]
        [PGButtonMethod(nameof(Undo), 20f)]
        public string undo;
        
        
        private Mesh originalMesh;
        
        /********************************************************************************************************************************/
        
        public void ScaleMesh()
        {
            SaveMeshForUndo();
            var mesh = meshFilter.sharedMesh;
            var vertices = new List<Vector3>();
            mesh.GetVertices(vertices);
            var localCenter = meshFilter.transform.InverseTransformPoint(meshFilter.transform.position);
            PGMeshUtility.PGScaleVertices(vertices, scaleFactor, localCenter);
            mesh.SetVertices(vertices);
            mesh.RecalculateBounds();
        }

        public void SetPivotToLowestVertex()
        {
            SaveMeshForUndo();
            var mesh = meshFilter.sharedMesh;
            var vertices = new List<Vector3>();
            mesh.GetVertices(vertices);

            var lowestVertexIndex = -1;
            var lowestHeight = Mathf.Infinity;
            for (int i = 0; i < vertices.Count; i++)
            {
                var vertexHeight = vertices[i].y;
                if (vertexHeight < lowestHeight)
                {
                    lowestHeight = vertexHeight;
                    lowestVertexIndex = i;
                }
            }
            
            var vertexWorld = meshFilter.transform.TransformPoint(vertices[lowestVertexIndex]);
            PGMeshUtility.SetPivotPosition(mesh, meshFilter.transform, vertexWorld);
            mesh.RecalculateBounds();
        }


        private void SaveMeshForUndo()
        {
            originalMesh = Instantiate(meshFilter.sharedMesh);
        }
        
        public void Undo()
        {
            if (!originalMesh) return;
            var mesh = meshFilter.sharedMesh;
            mesh.Clear();
            mesh.vertices = originalMesh.vertices;
            mesh.normals = originalMesh.normals;
            mesh.tangents = originalMesh.tangents;
            mesh.uv = originalMesh.uv;
            mesh.uv2 = originalMesh.uv2;
            mesh.colors = originalMesh.colors;
            
            mesh.subMeshCount = originalMesh.subMeshCount;
            for (int i = 0; i < originalMesh.subMeshCount; i++) mesh.SetTriangles(originalMesh.GetTriangles(i), i);
            
            mesh.RecalculateBounds();
        }
        
    }
}