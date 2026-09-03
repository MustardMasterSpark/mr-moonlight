// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    public class MeshParts : MonoBehaviour
    {
        public List<MeshPart> meshParts = new();

        public Vector2 seperationDirection = new(2f, 2f);
        public float seperationSlider;

        public List<Vector3> localPositions = new();

        /********************************************************************************************************************************/
        public void ApplySeperation()
        {
            if (localPositions.Count != meshParts.Count)
                StoreLocalPositions();

            for (var i = 0; i < meshParts.Count; i++)
            {
                var targetX = localPositions[i].x * seperationDirection.x;
                var targetY = localPositions[i].y * seperationDirection.y;

                var targetPosition = new Vector3(targetX, targetY, localPositions[i].z);

                var finalPosition = Vector3.Lerp(localPositions[i], targetPosition, seperationSlider);

                meshParts[i].transform.localPosition = finalPosition;
            }
        }

        /********************************************************************************************************************************/

        private void StoreLocalPositions()
        {
            localPositions.Clear();
            for (var i = 0; i < meshParts.Count; i++)
            {
                var part = meshParts[i];
                var localBoundsPosition = part.transform.localPosition;
                localPositions.Add(localBoundsPosition);
            }
        }
    }
}