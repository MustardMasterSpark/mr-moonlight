using System;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace MA.Flora
{
    /// <summary>
    /// Provides project-wide, runtime settings stored within the render pipeline graphics settings.
    /// </summary>
    [Serializable]
    [SupportedOnRenderPipeline]
    [CategoryInfo(Name = "Flora", Order = 100)]
    [ElementInfo(Name = "Runtime", Order = 0)]
    [HelpURL("https://flora.magneticarcade.com/scripts/runtime-settings")]
    public class FloraRuntimeSettings : IRenderPipelineGraphicsSettings
    {
        internal enum Version
        {
            Initial,
            RenameClass,
            Count,
            Last = Count - 1
        }

        [SerializeField, HideInInspector] private Version m_Version = Version.Last;

        [Obsolete("Flora now always uses the BatchRendererGroup culling pipeline. This setting has no effect.")]
        private FloraCullingPipeline m_DefaultCullingPipeline = FloraCullingPipeline.BatchRendererGroup;

        [SerializeField, Tooltip("Disables GPU occlusion culling project-wide.")]
        private bool m_DisableGPUOcclusionCulling;

        [SerializeField, Tooltip("Disables legacy light probe storage project-wide.")]
        private bool m_DisableLegacyLightProbes;

        [SerializeField, Tooltip("Disables motion vector storage and passes project-wide.")]
        private bool m_DisablePerObjectMotionVectors;

        /// <inheritdoc />
        public bool isAvailableInPlayerBuild => true;

        /// <inheritdoc />
        public int version => (int)m_Version;

        /// <summary>
        /// Disables GPU occlusion culling for all scenes in the project.
        /// </summary>
        public bool DisableGPUOcclusionCulling
        {
            get => m_DisableGPUOcclusionCulling;
            set => this.SetValueAndNotify(ref m_DisableGPUOcclusionCulling, value);
        }

        /// <summary>
        /// Disables legacy light probes for all scenes in the project.
        /// </summary>
        public bool DisableLegacyLightProbes
        {
            get => m_DisableLegacyLightProbes;
            set => this.SetValueAndNotify(ref m_DisableLegacyLightProbes, value);
        }

        /// <summary>
        /// Disables per-object motion vectors for all scenes in the project.
        /// </summary>
        public bool DisablePerObjectMotionVectors
        {
            get => m_DisablePerObjectMotionVectors;
            set => this.SetValueAndNotify(ref m_DisablePerObjectMotionVectors, value);
        }

        #region Obsolete

        [Obsolete("Flora now always uses the BatchRendererGroup culling pipeline. This setting has no effect.")]
        public FloraCullingPipeline DefaultCullingPipeline
        {
            get => m_DefaultCullingPipeline;
            set => this.SetValueAndNotify(ref m_DefaultCullingPipeline, value);
        }

        #endregion
    }
}
