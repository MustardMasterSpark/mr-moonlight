// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace PampelGames.GoreSimulator.Editor
{
    internal static class BoneWeightsUtility 
    {
        
        public static bool DoesBoneHaveWeights(SkinnedMeshRenderer smr, string boneName)
        {
            int boneIndex = Array.FindIndex(smr.bones, bone => bone.name == boneName);
            if (boneIndex == -1) return false;

            var mesh = smr.sharedMesh;
            var boneWeights = new NativeArray<BoneWeight>(mesh.boneWeights, Allocator.TempJob);
            var boneIndicesWithWeights = new NativeList<int>(Allocator.TempJob);
            var containsWeight = new NativeArray<bool>(1, Allocator.TempJob); // workaround for bool as jobs can't return boolean.
            
            var boneWeightJob = new BoneWeightProcessingJob
            {
                boneIndex = boneIndex,
                contains = containsWeight,
                boneWeights = boneWeights,
                boneIndicesWithWeights = boneIndicesWithWeights,
            };
        
            var jobHandle = boneWeightJob.Schedule();
            jobHandle.Complete();

            var contains = containsWeight[0];

            containsWeight.Dispose();
            boneWeights.Dispose();
            boneIndicesWithWeights.Dispose();
        
            return contains;
        }
        
        [BurstCompile]
        struct BoneWeightProcessingJob : IJob
        {
            public int boneIndex;

            public NativeArray<bool> contains;
            public NativeArray<BoneWeight> boneWeights;
            public NativeList<int> boneIndicesWithWeights;
        
            public void Execute()
            {
                for (var i = 0; i < boneWeights.Length; i++)
                {
                    boneIndicesWithWeights.Add(boneWeights[i].boneIndex0);
                    boneIndicesWithWeights.Add(boneWeights[i].boneIndex1);
                    boneIndicesWithWeights.Add(boneWeights[i].boneIndex2);
                    boneIndicesWithWeights.Add(boneWeights[i].boneIndex3);
                }
                
                contains[0] = boneIndicesWithWeights.Contains(boneIndex);
            }
        }
    
        // public static bool DoesBoneHaveWeights(SkinnedMeshRenderer smr, string boneName)
        // {
        //     var contains = false;
        //     Mesh mesh = smr.sharedMesh;
        //
        //     int boneIndex = Array.FindIndex(smr.bones, bone => bone.name == boneName);
        //     if (boneIndex == -1) return false;
        //
        //     var boneWeights = new NativeArray<BoneWeight>(mesh.boneWeights, Allocator.TempJob);
        //     var boneIndicesWithWeights = new NativeList<int>(Allocator.TempJob);
        //
        //     foreach (var bw in boneWeights)
        //     {
        //         boneIndicesWithWeights.Add(bw.boneIndex0);
        //         boneIndicesWithWeights.Add(bw.boneIndex1);
        //         boneIndicesWithWeights.Add(bw.boneIndex2);
        //         boneIndicesWithWeights.Add(bw.boneIndex3);
        //     }
        //
        //     if (boneIndicesWithWeights.Contains(boneIndex))
        //         contains = true;
        //
        //     boneWeights.Dispose();
        //     boneIndicesWithWeights.Dispose();
        //     
        //     return contains;
        // }
    }
}