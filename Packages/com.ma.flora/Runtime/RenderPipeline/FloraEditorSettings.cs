// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;

namespace MA.Flora
{
    /// <summary>
    /// Provides project-wide, editor settings stored within the render pipeline graphics settings.
    /// </summary>
    [Serializable]
    [SupportedOnRenderPipeline]
    [CategoryInfo(Name = "Flora", Order = 110)]
    [ElementInfo(Name = "Editor", Order = 1)]
    [HelpURL("https://flora.magneticarcade.com/scripts/editor-settings")]
    public class FloraEditorSettings : IRenderPipelineGraphicsSettings
    {
        internal enum Version
        {
            Initial,
            Count,
            Last = Count - 1
        }

        [SerializeField, HideInInspector] private Version m_Version = Version.Last;

        /// <inheritdoc />
        public bool isAvailableInPlayerBuild => false;

        /// <inheritdoc />
        public int version => (int)m_Version;

        [SerializeField, Tooltip("Determines if Flora instance renderers are allowed to be rendered in edit mode. " +
                                 "This can be used to avoid selection issues in the editor for instance renderers (i.e. rect selection).")]
        private bool m_DisableInstanceRenderersInEditMode = false;

        /// <summary>
        /// When set to true, instance renderers will only be rendered in play mode. This can be used to avoid selection issues in the
        /// editor when shaders that aren't compatible with Flora's selection instancing (i.e. rect selection).
        /// </summary>
        public bool DisableInstanceRenderersInEditMode
        {
            get => m_DisableInstanceRenderersInEditMode;
            set => this.SetValueAndNotify(ref m_DisableInstanceRenderersInEditMode, value);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void SubscribeToSettingsChanges()
        {
            GraphicsSettings.Subscribe<FloraEditorSettings>((settings, propertyName) =>
            {
                if (propertyName.Equals(nameof(m_DisableInstanceRenderersInEditMode)))
                {
#if UNITY_6000_5_OR_NEWER
                    var instanceRenderers = UnityEngine.Object.FindObjectsByType<FloraInstanceRenderer>(FindObjectsInactive.Include);
#else
                    var instanceRenderers = UnityEngine.Object.FindObjectsByType<FloraInstanceRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif

                    foreach (var renderer in instanceRenderers)
                    {
                        if (!renderer.enabled)
                            continue; // User may have disabled the renderer intentionally

                        renderer.enabled = false;
                        renderer.enabled = true;
                    }
                }
            });
        }
#endif
    }
}
