using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MrMoonlight.Runtime
{
    /// <summary>
    /// Scene-view convenience: lets Carlos flip HAZE fog and the CRT post effect off while staging
    /// or blocking out movement, without asking for it each time. Drop the "Scene Effects Toggle"
    /// prefab into a scene, point it at the Volume that carries both overrides (currently
    /// "HAZE Global Fog"), and use the checkboxes or the right-click context menu.
    ///
    /// Looks components up by type NAME rather than a compile-time reference on purpose: HAZE and
    /// Retro Shaders Pro live in different assemblies (Haze.Runtime.asmdef vs. Retro Shaders Pro's
    /// scripts, which have no asmdef and land in Assembly-CSharp), and VolumeComponent.active is
    /// public on the base type in UnityEngine.Rendering, so no reference to either assembly is
    /// needed at all. Also means this script keeps working if either package's namespace changes.
    ///
    /// Toggling here edits the shared Volume PROFILE ASSET's active flags directly (the same fields
    /// the "Active" checkbox next to each override edits) - not a scene-instance-only override - so
    /// it persists between sessions the same way manual Inspector edits would. Use "Restore Ship
    /// Defaults" before a real build/screenshot if you've been leaving effects off while staging.
    ///
    /// FOG HAS TWO SOURCES, NOT ONE. Turning off "Fog Enabled" only silenced the global Volume
    /// override (VP_HazeGlobalFog's HazeGlobalFogVolumeComponent) - the "HAZE Explorable Area Fog"
    /// GameObject carries a separate HazeDensityVolume (a local density box covering the playable
    /// slice per Docs/pc-build-target.md §7), which contributes fog independently of the global
    /// override and was still rendering after the first version of this toggle shipped. Fog Enabled
    /// now disables every HazeDensityVolume behaviour found in the scene too, found the same
    /// by-type-name way for the same assembly reasons as above.
    /// </summary>
    [ExecuteAlways]
    public class SceneEffectsToggle : MonoBehaviour
    {
        [SerializeField] private Volume targetVolume;

        [Header("Toggle while staging - unchecking hides the effect immediately, even outside Play Mode")]
        [SerializeField] private bool fogEnabled = true;
        [SerializeField] private bool crtEnabled = true;

        private const string HazeGlobalComponentTypeName = "HazeGlobalFogVolumeComponent";
        private const string HazeDensityVolumeTypeName = "HazeDensityVolume";
        private const string CrtComponentTypeName = "CRTSettings";

        private bool _applying;

        public bool FogEnabled
        {
            get => fogEnabled;
            set { fogEnabled = value; Apply(); }
        }

        public bool CrtEnabled
        {
            get => crtEnabled;
            set { crtEnabled = value; Apply(); }
        }

        private void OnEnable() => SyncFromProfile();

        private void OnValidate()
        {
            if (_applying) return;
            Apply();
        }

        private VolumeComponent FindVolumeComponent(string typeName)
        {
            var profile = targetVolume != null ? targetVolume.sharedProfile : null;
            if (profile == null) return null;

            foreach (var c in profile.components)
            {
                if (c != null && c.GetType().Name == typeName)
                    return c;
            }
            return null;
        }

        /// <summary>Every local density-volume behaviour in the open scene(s) - not just the one Haze happens to ship with today.</summary>
        private List<Behaviour> FindDensityVolumes()
        {
            var result = new List<Behaviour>();
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mb in all)
            {
                if (mb != null && mb.GetType().Name == HazeDensityVolumeTypeName)
                    result.Add(mb);
            }
            return result;
        }

        /// <summary>Reads the profile's current active flags into the inspector fields (call after assigning targetVolume, or if the profile/density volumes were edited directly).</summary>
        [ContextMenu("Sync From Profile")]
        public void SyncFromProfile()
        {
            var haze = FindVolumeComponent(HazeGlobalComponentTypeName);
            var crt = FindVolumeComponent(CrtComponentTypeName);

            _applying = true;
            if (haze != null) fogEnabled = haze.active;
            if (crt != null) crtEnabled = crt.active;
            _applying = false;
        }

        private void Apply()
        {
            var haze = FindVolumeComponent(HazeGlobalComponentTypeName);
            var crt = FindVolumeComponent(CrtComponentTypeName);

            if (haze != null) haze.active = fogEnabled;
            if (crt != null) crt.active = crtEnabled;

            foreach (var density in FindDensityVolumes())
                density.enabled = fogEnabled;

#if UNITY_EDITOR
            if (targetVolume != null && targetVolume.sharedProfile != null)
                EditorUtility.SetDirty(targetVolume.sharedProfile);
            foreach (var density in FindDensityVolumes())
                EditorUtility.SetDirty(density);
#endif
        }

        [ContextMenu("Disable All Effects")]
        public void DisableAllEffects()
        {
            fogEnabled = false;
            crtEnabled = false;
            Apply();
        }

        [ContextMenu("Restore Ship Defaults (Fog + CRT On)")]
        public void RestoreShipDefaults()
        {
            fogEnabled = true;
            crtEnabled = true;
            Apply();
        }
    }
}
