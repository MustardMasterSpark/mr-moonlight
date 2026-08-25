// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace MA.Flora.Editor
{
    [VolumeParameterDrawer(typeof(FloraScreenSizeParameter))]
    [UsedImplicitly]
    internal sealed class FloraScreenSizeParameterDrawer : VolumeParameterDrawer
    {
        private const int   PercentDecimals = 1;
        private const float Epsilon         = 1e-4f;
        private const float FieldWidth      = 70f;
        private const float FieldSpacing    = 4f;

        private static class Styles
        {
            public static readonly GUIStyle MiniLeft   = new(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            public static readonly GUIStyle MiniCenter = new(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            public static readonly GUIStyle MiniRight  = new(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
        }

        public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
        {
            var value = parameter.value;
            if (value.propertyType != SerializedPropertyType.Float)
                return false;

            var o = parameter.GetObjectRef<FloraScreenSizeParameter>();
            var min = o.min; // [0..1]
            var max = o.max; // [0..1]

            var percentMin = min * 100f;
            var percentMax = max * 100f;
            var percentValue = Mathf.Clamp(value.floatValue, min, max) * 100f;

            var prevMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = value.hasMultipleDifferentValues;

            var rowRect     = EditorGUILayout.GetControlRect(hasLabel: true);
            var contentRect = EditorGUI.PrefixLabel(rowRect, title);

            var sliderRect = new Rect(contentRect.x, contentRect.y, contentRect.width - FieldWidth - FieldSpacing, contentRect.height);
            var fieldRect  = new Rect(sliderRect.xMax + FieldSpacing, contentRect.y, FieldWidth, contentRect.height);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                // Reversed slider (LOD-style): left=high %, right=low %
                float mirrored = percentMin + percentMax - percentValue;
                mirrored = GUI.HorizontalSlider(sliderRect, mirrored, percentMin, percentMax);
                percentValue   = percentMin + percentMax - mirrored;

                double uiPercent = Math.Round(percentValue, PercentDecimals, MidpointRounding.AwayFromZero);
                uiPercent = EditorGUI.FloatField(fieldRect, (float)uiPercent);

                if (check.changed)
                {
                    float quantized = Mathf.Clamp((float)uiPercent, percentMin, percentMax);
                    quantized       = Quantize(quantized, PercentDecimals);
                    quantized       = SnapToEnds(quantized, percentMin, percentMax, Epsilon);
                    value.floatValue = quantized * 0.01f; // back to [0..1]
                }
            }

            EditorGUI.showMixedValue = prevMixedValue;

            // Mini labels
            var labelsRect = sliderRect;
            labelsRect.y += labelsRect.height * 0.5f;

            var leftLabel   = max.ToString("P0");
            var centerLabel = ((max - min) * 0.5f).ToString("P" + PercentDecimals);
            var rightLabel  = min.ToString("P0");

            using (new EditorGUI.DisabledGroupScope(true))
            {
                GUI.Label(labelsRect, leftLabel,   Styles.MiniLeft);
                GUI.Label(labelsRect, centerLabel, Styles.MiniCenter);
                GUI.Label(labelsRect, rightLabel,  Styles.MiniRight);
            }

            return true;
        }

        private static float Quantize(float value, int decimals)
        {
            float step = 1f / Mathf.Pow(10f, decimals);
            return Mathf.Round(value / step) * step;
        }

        private static float SnapToEnds(float value, float min, float max, float eps)
        {
            if (Mathf.Abs(value - min) <= eps) return min;
            if (Mathf.Abs(value - max) <= eps) return max;
            return value;
        }
    }

    [VolumeParameterDrawer(typeof(FloraScreenRangeParameter))]
    [UsedImplicitly]
    internal sealed class FloraScreenRangeParameterDrawer : VolumeParameterDrawer
    {
        private static class Styles
        {
            public static readonly GUIStyle MiniLeft   = new(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            public static readonly GUIStyle MiniCenter = new(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            public static readonly GUIStyle MiniRight  = new(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
        }

        public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
        {
            var value = parameter.value;
            if (value.propertyType != SerializedPropertyType.Vector2)
                return false;

            var o = parameter.GetObjectRef<FloraScreenRangeParameter>();
            var v = value.vector2Value;

            var minValue = 1f - v.x;
            var maxValue = 1f - v.y;
            var minLimit = 1f - o.min;
            var maxLimit = 1f - o.max;

            var prevMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = value.hasMultipleDifferentValues;

            var rowRect     = EditorGUILayout.GetControlRect(hasLabel: true);
            var contentRect = EditorGUI.PrefixLabel(rowRect, title);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUI.MinMaxSlider(contentRect, ref minValue, ref maxValue, minLimit, maxLimit);

                if (check.changed)
                {
                    minValue = Mathf.Clamp(minValue, minLimit, maxLimit);
                    maxValue = Mathf.Clamp(maxValue, minLimit, maxLimit);
                    if (maxValue < minValue)
                        (minValue, maxValue) = (maxValue, minValue);

                    value.vector2Value = new Vector2(1f - minValue, 1f - maxValue);
                }
            }

            EditorGUI.showMixedValue = prevMixedValue;

            // Mini labels
            var labelsRect = contentRect;
            labelsRect.y += labelsRect.height * 0.5f;

            var leftLabel   = o.min.ToString("P0");
            var centerLabel = Mathf.Abs((o.max - o.min) * 0.5f).ToString("P1");
            var rightLabel  = o.max.ToString("P0");

            using (new EditorGUI.DisabledGroupScope(true))
            {
                GUI.Label(labelsRect, leftLabel,   Styles.MiniLeft);
                GUI.Label(labelsRect, centerLabel, Styles.MiniCenter);
                GUI.Label(labelsRect, rightLabel,  Styles.MiniRight);
            }

            return true;
        }
    }
}
