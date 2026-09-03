// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PampelGames.GoreSimulator
{
    public static class MathUtility
    {

        public static float DirectionToCenter(Vector3 originalPos, Vector3 direction, Vector3 centerPos)
        {
            Vector3 centerDirection = centerPos - originalPos;
            float dotProduct = Vector3.Dot(centerDirection.normalized, direction.normalized);
            return dotProduct > 0 ? -1 : 1;
        }

        public static Vector3 GetAveragePosition(List<Transform> bones)
        {
            return bones.Aggregate(Vector3.zero, (acc, t) => acc + t.position) / bones.Count;
        }
        public static Vector3 GetAveragePosition(List<BonesClass> bones)
        {
            return bones.Aggregate(Vector3.zero, (acc, t) => acc + t.bone.position) / bones.Count;
        }

        public static Vector3 GetCenterPosition(SkinnedMeshRenderer smr, List<int> indexes, Vector3[] vertices)
        {
            Vector3 sum = Vector3.zero;
            foreach (var index in indexes) sum += vertices[index];
            var center = sum / indexes.Count;
            var worldSpace = smr.transform.TransformPoint(center);
            return worldSpace;
        }
        
        public static Quaternion GetRandomRotation()
        {
            var xRotation = Random.Range(0.0f, 360.0f);
            var yRotation = Random.Range(0.0f, 360.0f);
            var zRotation = Random.Range(0.0f, 360.0f);
            return Quaternion.Euler(xRotation, yRotation, zRotation);
        }
        
        public static int UniqueGUID()
        {
            return Guid.NewGuid().GetHashCode();
        }
        
    }
}