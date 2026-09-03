// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace PampelGames.Shared.Utility
{
    public static class PGMathUtility
    {
        /// <summary>
        ///     Calculates a list of evenly spaced normalized values between 0 and 1 for a given length and spacing.
        /// </summary>
        /// <returns>A list of float values normalized between 0 and 1, representing evenly spaced positions along the length.</returns>
        public static List<float> GetEvenlySpacedValues(float length, float spacing)
        {
            return GetEvenlySpacedValues(length, spacing, out _);
        }

        public static List<float> GetEvenlySpacedValues(float length, float spacing, out float gap)
        {
            var evaluations = new List<float>();
            gap = 0f;

            var count = Mathf.FloorToInt(length / spacing);
            if (count < 1)
            {
                evaluations.Add(0.5f);
                return evaluations;
            }

            var leftover = length - count * spacing;
            gap = leftover / count;
            var adjustedSpacing = spacing + gap;

            for (var i = 0; i < count; i++)
            {
                var distance = (i + 0.5f) * adjustedSpacing;
                var t = distance / length;
                evaluations.Add(t);
            }

            return evaluations;
        }
    }
}