// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace MA.Flora
{
    /// <summary>
    /// A component that automatically registers and renders a scene-based <see cref="LODGroup"/> or <see cref="MeshRenderer"/>
    /// with the <see cref="FloraSystem"/> as a single instance.
    /// </summary>
    /// <remarks>
    /// Disabling or removing the component automatically unregisters the renderer with the <see cref="FloraSystem"/>.
    /// A prefab reference is optional and is only used as the identity source for identity-based filtering and tooling.
    /// When unset, the scene object is used as both the identity source and the render source.
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Flora/Flora Instance Renderer")]
    [Icon("Packages/com.ma.flora/Editor/EditorResources/Icons/InstanceRenderer Icon.png")]
    [HelpURL("https://flora.magneticarcade.com/scripts/instance-renderer")]
    [MovedFrom(true, "MA.Flora", "MA.Flora", "InstancedObjectLink")]
    public sealed class FloraInstanceRenderer : MonoBehaviour
    {
        [SerializeField] private GameObject m_Prefab;
        [NonSerialized] private FloraInstanceHandle m_InstanceHandle;

        internal GameObject IdentitySource => m_Prefab ? m_Prefab : gameObject;
        internal GameObject RenderSource => gameObject;

        /// <summary>
        /// The optional identity source prefab that this renderer is associated with.
        /// </summary>
        /// <remarks>
        /// Scene-only renderers can leave this unset and use the scene object as their identity source.
        /// Runtime-instantiated objects can set this manually to preserve identity-based filtering and tooling.
        /// </remarks>
        public GameObject Prefab
        {
            get => m_Prefab;
            set
            {
                if (m_Prefab != value)
                {
                    FloraSystem.Instance?.UnregisterInstanceRenderer(this);
                    m_Prefab = value;
                    if (gameObject.scene.IsValid())
                        FloraSystem.Instance?.RegisterInstanceRenderer(this);
                }
            }
        }

        /// <summary>
        /// The current instance handle representing this renderer in the <see cref="FloraSystem"/>.
        /// </summary>
        public FloraInstanceHandle InstanceHandle
        {
            get => m_InstanceHandle;
            internal set => m_InstanceHandle = value;
        }

        /// <summary>
        /// The world-space bounds of the instance managed by this renderer.
        /// </summary>
        public Bounds Bounds => FloraSystem.Instance?.GetInstanceBounds(m_InstanceHandle) ?? gameObject.CalculateWorldBounds();

        /// <summary>
        /// The local-space bounds of the instance managed by this renderer.
        /// </summary>
        public Bounds LocalBounds => Bounds.TransformBy(transform.worldToLocalMatrix);

        private void OnEnable()
        {
            var canRegister = gameObject.scene.IsValid();

#if UNITY_EDITOR
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var isInPrefabStage = prefabStage != null && gameObject.scene == prefabStage.scene;

            if (!TryGetComponent(out MeshRenderer _) && !TryGetComponent(out LODGroup _))
            {
                Debug.LogError("FloraInstanceRenderer must be attached to a GameObject with either a MeshRenderer or LODGroup component.", this);
                return;
            }

            // Never register while authoring a prefab (stage) or when inspecting a prefab asset.
            if (isInPrefabStage || PrefabUtility.IsPartOfPrefabAsset(gameObject))
                canRegister = false;

            if (SystemSettingsResolver.DisableInstanceRenderersInEditMode)
                canRegister = EditorApplication.isPlayingOrWillChangePlaymode;
#endif

            if (canRegister)
            {
                FloraSystem.GetOrCreate().RegisterInstanceRenderer(this);
                FloraSystem.WasCreated += OnSystemWasCreated;
                FloraSystem.WillBeDestroyed += OnSystemWillBeDestroyed;
            }
        }

        private void OnDisable()
        {
            FloraSystem.WillBeDestroyed -= OnSystemWillBeDestroyed;
            FloraSystem.WasCreated -= OnSystemWasCreated;
            FloraSystem.Instance?.UnregisterInstanceRenderer(this);
            m_InstanceHandle = FloraInstanceHandle.Null;
        }

        private void OnSystemWasCreated(FloraSystem system)
        {
            system.RegisterInstanceRenderer(this);
        }

        private void OnSystemWillBeDestroyed(FloraSystem system)
        {
            m_InstanceHandle = FloraInstanceHandle.Null;
        }


#if UNITY_EDITOR
        private void Reset()
        {
            EnsurePrefabSet();
        }

        private void OnValidate()
        {
            // Keep the serialized prefab correct while editing.
            if (!Application.isPlaying)
                EnsurePrefabSet();
        }

        private void EnsurePrefabSet()
        {
            if (Application.isPlaying)
                return;

            if (TryResolvePrefabReference(out var resolved))
            {
                var oldPath = m_Prefab ? AssetDatabase.GetAssetPath(m_Prefab) : null;
                var newPath = resolved ? AssetDatabase.GetAssetPath(resolved) : null;

                if (oldPath != newPath)
                {
                    m_Prefab = resolved;
                    EditorUtility.SetDirty(this);
                }
            }
            else if (m_Prefab == null)
            {
                // No prefab connection (e.g., a purely scene-made object). Keep null and do not spam errors.
                // Users can set Prefab manually for runtime-instantiated objects.
            }
        }

        private bool TryResolvePrefabReference(out GameObject prefab)
        {
            prefab = null;

            // Prefab Stage
            var stage = PrefabStageUtility.GetPrefabStage(gameObject);
            if (stage != null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    // Nested instance inside Prefab Mode → nearest instance asset path
                    var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
                    if (!string.IsNullOrEmpty(path))
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        return prefab != null;
                    }

                    return false;
                }

                // Not a nested instance → this object belongs to the stage's main prefab
                var stagePath = stage.assetPath;
                if (!string.IsNullOrEmpty(stagePath))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stagePath);
                    return prefab != null;
                }

                return false;
            }

            // Project-window prefab asset
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                var path = AssetDatabase.GetAssetPath(gameObject);
                if (!string.IsNullOrEmpty(path))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    return prefab != null;
                }
                return false;
            }

            // Scene instance of a prefab: reference the source prefab
            if (gameObject.scene.IsValid() && PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
                if (!string.IsNullOrEmpty(path))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    return prefab != null;
                }
            }

            // Not a prefab asset/stage/instance
            return false;
        }
#endif
    }
}
