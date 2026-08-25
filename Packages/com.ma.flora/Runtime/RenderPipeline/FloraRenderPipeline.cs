// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEngine;

namespace MA.Flora
{
    internal struct FloraRenderPipelineCameraSettings
    {
        public bool UseGPUOcclusionCulling;
    }

    internal abstract class FloraRenderPipeline : IDisposable
    {
        public abstract FloraRenderPipelineType PipelineType { get; }

        public virtual void Dispose() { }

        public abstract void EnqueueCameraPasses(Camera camera, FloraRenderPipelineCameraSettings cameraSettings);
    }
}
