// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MA.Flora
{
    /// <summary>
    /// A bridge component that registers a <see cref="Terrain"/> with the <see cref="FloraSystem"/>.
    /// </summary>
    /// <remarks>
    /// In most cases, you do not need to manually add or configure this component; setting <see cref="FloraSceneSettings.AutoRegisterTerrains"/>
    /// to true and enabling <see cref="FloraSceneSettings.EnableTerrainFoliage"/> is sufficient to manage the component.
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain))]
    [AddComponentMenu("Flora/Flora Terrain Provider")]
    [Icon("Packages/com.ma.flora/Editor/EditorResources/Icons/TerrainProvider Icon.png")]
    [HelpURL("https://flora.magneticarcade.com/scripts/terrain-provider")]
    [MovedFrom(true, "MA.Flora", "MA.Flora", "InstancedTerrainFoliage")]
    public sealed class FloraTerrainProvider : MonoBehaviour
    {
        /// <summary>
        /// Invoked whenever <see cref="OnTerrainChanged"/> is called (e.g., when the terrain's heightmap is modified).
        /// Provides the affected <see cref="Terrain"/> and the <see cref="TerrainChangedFlags"/> indicating what was altered.
        /// </summary>
        public static event Action<Terrain, TerrainChangedFlags> TerrainDataChanged;

        /// <summary>
        /// The underlying <see cref="UnityEngine.Terrain"/> managed by this provider.
        /// </summary>
        public Terrain Terrain
        {
            get
            {
                if (m_Terrain == null)
                    TryGetComponent(out m_Terrain);

                return m_Terrain;
            }
        }

        /// <summary>
        /// Shorthand property for <see cref="Terrain.terrainData"/>,
        /// providing direct access to the <see cref="TerrainData"/> that this provider manages.
        /// </summary>
        public TerrainData TerrainData
        {
            get
            {
                var terrain = Terrain;
                if (terrain == null)
                    return null;

                return terrain.terrainData;
            }
        }

        private Terrain m_Terrain;

        private void OnEnable()
        {
            if (!gameObject.scene.IsValid())
                return; // Prefab stage

            TryGetComponent(out m_Terrain);
            FloraSystem.GetOrCreate().RegisterTerrain(m_Terrain);
            FloraSystem.WasCreated += OnSystemWasCreated;
        }

        private void OnDisable()
        {
            FloraSystem.WasCreated -= OnSystemWasCreated;
            FloraSystem.Instance?.UnregisterTerrain(m_Terrain);
        }

        private void OnSystemWasCreated(FloraSystem system)
        {
            system.RegisterTerrain(m_Terrain);
        }

        private void OnTerrainChanged(TerrainChangedFlags changedFlags)
        {
            TerrainDataChanged?.Invoke(m_Terrain, changedFlags);
            FloraSystem.Instance?.SetTerrainChanged(m_Terrain, changedFlags);
        }
    }
}
