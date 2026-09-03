// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace PampelGames.Shared.Utility
{
    // From Unite Now 2020: https://www.youtube.com/watch?v=i2IpJHUyZLM
    public class PGProfiler : MonoBehaviour
    {
        public float size = 25;
        public float interval = 0.5f;
        public bool showMinMax;

        private string statsText;
        private float timer;

        private readonly List<long> gcMemorySamples = new();
        private readonly List<long> systemMemorySamples = new();
        private readonly List<long> drawCallSamples = new();
        private readonly List<long> mainThreadTimeSamples = new();

        private ProfilerRecorder systemMemoryRecorder;
        private ProfilerRecorder gcMemoryRecorder;
        private ProfilerRecorder mainThreadTimeRecorder;
        private ProfilerRecorder drawCallsCountRecorder;

        private static double GetAverage(List<long> samples)
        {
            if (samples.Count == 0) return 0;
            double sum = 0;
            foreach (var s in samples) sum += s;
            return sum / samples.Count;
        }

        private void OnEnable()
        {
            systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
            mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            drawCallsCountRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        }

        private void OnDisable()
        {
            systemMemoryRecorder.Dispose();
            gcMemoryRecorder.Dispose();
            mainThreadTimeRecorder.Dispose();
            drawCallsCountRecorder.Dispose();
        }

        private void Update()
        {
            gcMemorySamples.Add(gcMemoryRecorder.LastValue);
            systemMemorySamples.Add(systemMemoryRecorder.LastValue);
            drawCallSamples.Add(drawCallsCountRecorder.LastValue);
            mainThreadTimeSamples.Add(mainThreadTimeRecorder.LastValue);

            timer += Time.deltaTime;
            if (timer >= interval)
            {
                // Compute averages
                var avgFrameTime = GetAverage(mainThreadTimeSamples) * 1e-6; // ns to ms
                var avgGCMem = GetAverage(gcMemorySamples) / (1024.0 * 1024.0);
                var avgSystemMem = GetAverage(systemMemorySamples) / (1024.0 * 1024.0);
                var avgDrawCalls = GetAverage(drawCallSamples);

                // Compute min/max frame time if requested
                double minFrameTime = 0;
                double maxFrameTime = 0;
                if (showMinMax && mainThreadTimeSamples.Count > 0)
                {
                    var minVal = long.MaxValue;
                    var maxVal = long.MinValue;
                    foreach (var s in mainThreadTimeSamples)
                    {
                        if (s < minVal) minVal = s;
                        if (s > maxVal) maxVal = s;
                    }

                    minFrameTime = minVal * 1e-6; // ns to ms
                    maxFrameTime = maxVal * 1e-6;
                }

                // Build text
                var sb = new StringBuilder(500);
                sb.Append($"Frame Time: {avgFrameTime:F1} ms");
                if (showMinMax) sb.Append($" (Min: {minFrameTime:F1}, Max: {maxFrameTime:F1})");
                sb.AppendLine();
                sb.AppendLine($"GC Memory: {avgGCMem:F1} MB");
                sb.AppendLine($"System Memory: {avgSystemMem:F1} MB");
                sb.AppendLine($"Draw Calls: {avgDrawCalls:F0}");
                statsText = sb.ToString();

                // Reset
                timer = 0f;
                gcMemorySamples.Clear();
                systemMemorySamples.Clear();
                drawCallSamples.Clear();
                mainThreadTimeSamples.Clear();
            }
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.textArea);
            style.fontSize = Mathf.RoundToInt(size);
            var textSize = style.CalcSize(new GUIContent(statsText));
            var padding = 8f;
            var rect = new Rect(10, 30, textSize.x + padding, textSize.y + padding);
            GUI.TextArea(rect, statsText, style);
        }
    }
}