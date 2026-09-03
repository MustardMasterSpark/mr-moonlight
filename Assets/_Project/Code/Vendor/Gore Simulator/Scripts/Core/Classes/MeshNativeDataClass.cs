// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    public class MeshNativeDataClass
    {
        public NativeArray<JobHandle> jobHandles;
        public NativeArray<int> indexesNative;
        public NativeHashMap<int, int> origToNewMapNative;
        public List<NativeArray<int>> trianglesNativePerSubM;
        
        public void InitializeRuntimeMeshData(Mesh mesh, int[] indexes)
        {
            jobHandles = new NativeArray<JobHandle>(mesh.subMeshCount, Allocator.Persistent);
            indexesNative = new NativeArray<int>(indexes, Allocator.Persistent);
            origToNewMapNative = new NativeHashMap<int, int>(mesh.triangles.Length, Allocator.Persistent);

            trianglesNativePerSubM = new List<NativeArray<int>>();
            for (int i = 0; i < mesh.subMeshCount; i++) 
            {
                var triangles = mesh.GetTriangles(i);
                trianglesNativePerSubM.Add(new NativeArray<int>(triangles, Allocator.Persistent));
            }
        }
        
        public void DisposeRuntimeMeshData()
        {
            jobHandles.Dispose();
            indexesNative.Dispose();
            origToNewMapNative.Dispose();
            for (int i = 0; i < trianglesNativePerSubM.Count; i++)
            {
                trianglesNativePerSubM[i].Dispose();
            }
            
        }
    }
}
