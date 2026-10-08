using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Drops the selected props onto the terrain and tilts them to the slope, keeping their yaw.
    ///
    /// Unity's own Ctrl+Shift surface drag does nothing on terrain in this project, hence this tool
    /// (Carlos, 2026-10-05). Open it from Tools > Moonlight > Prop Terrain Snap, or press
    /// Ctrl+Shift+T with props selected (not G: that opens AMD Adrenalin on Carlos's machine).
    ///
    /// HOW IT PLACES
    /// -------------
    /// Pivots on props are not reliably at the base, so the snap uses the bottom-centre of the
    /// prop's renderer bounds (measured in the prop's own space, so it follows the tilt). That point
    /// is put on the terrain surface, then pushed down by Sink along the prop's up axis so the base
    /// buries slightly instead of leaving a gap on uneven ground.
    ///
    /// Blend 1 = base flush with the slope. Lower values tilt less: steps and walls often read
    /// better at 0.5-0.7 because full slope makes them look like they are sliding downhill.
    /// </summary>
    public sealed class PropTerrainSnap : EditorWindow
    {
        private const string BlendKey = "MrMoonlight.PropSnap.Blend";
        private const string SinkKey = "MrMoonlight.PropSnap.Sink";
        private const string TiltKey = "MrMoonlight.PropSnap.Tilt";

        private float _blend;
        private float _sink;
        private bool _tilt;

        [MenuItem("Tools/Moonlight/Prop Terrain Snap")]
        private static void Open() => GetWindow<PropTerrainSnap>("Prop Snap");

        [MenuItem("Tools/Moonlight/Snap Selection To Terrain %#t")]
        private static void SnapMenu() => SnapSelection(
            EditorPrefs.GetFloat(BlendKey, 1f), EditorPrefs.GetFloat(SinkKey, 0.05f), EditorPrefs.GetBool(TiltKey, true));

        private void OnEnable()
        {
            _blend = EditorPrefs.GetFloat(BlendKey, 1f);
            _sink = EditorPrefs.GetFloat(SinkKey, 0.05f);
            _tilt = EditorPrefs.GetBool(TiltKey, true);
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _tilt = EditorGUILayout.Toggle("Tilt to slope", _tilt);
            _blend = EditorGUILayout.Slider("Slope blend", _blend, 0f, 1f);
            _sink = EditorGUILayout.Slider("Sink into ground (m)", _sink, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetFloat(BlendKey, _blend);
                EditorPrefs.SetFloat(SinkKey, _sink);
                EditorPrefs.SetBool(TiltKey, _tilt);
            }

            EditorGUILayout.HelpBox("Select props in the scene, then Snap (or Ctrl+Shift+T). Undo works.", MessageType.None);
            if (GUILayout.Button("Snap selection to terrain", GUILayout.Height(28)))
                SnapSelection(_blend, _sink, _tilt);
        }

        private static void SnapSelection(float blend, float sink, bool tilt)
        {
            int done = 0, missed = 0;
            foreach (var t in Selection.transforms)
            {
                if (!SnapOne(t, blend, sink, tilt)) { missed++; continue; }
                done++;
            }
            Debug.Log($"[PropTerrainSnap] snapped {done}, skipped {missed} (no terrain under it or no renderers).");
        }

        private static bool SnapOne(Transform root, float blend, float sink, bool tilt)
        {
            if (!TryGetLocalBase(root, out var localBase)) return false;

            Undo.RecordObject(root, "Snap prop to terrain");

            if (!TryGroundAt(root.TransformPoint(localBase), out var hit, out var normal)) return false;

            if (tilt)
            {
                var targetUp = Vector3.Slerp(Vector3.up, normal, blend).normalized;
                root.rotation = Quaternion.FromToRotation(root.up, targetUp) * root.rotation;
            }

            // Re-measure after the tilt: the base point moved with the rotation.
            if (!TryGroundAt(root.TransformPoint(localBase), out hit, out _)) return false;
            root.position += hit - root.TransformPoint(localBase) - root.up * sink;
            return true;
        }

        // Bottom-centre of all renderers, in the root's local space.
        private static bool TryGetLocalBase(Transform root, out Vector3 localBase)
        {
            localBase = default;
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;

            var bounds = new Bounds();
            bool first = true;
            foreach (var r in renderers)
            {
                var lb = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = lb.center + Vector3.Scale(lb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = root.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }

            localBase = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return true;
        }

        private static bool TryGroundAt(Vector3 world, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = Vector3.up;
            foreach (var terrain in Terrain.activeTerrains)
            {
                var origin = terrain.transform.position;
                var size = terrain.terrainData.size;
                if (world.x < origin.x || world.z < origin.z || world.x > origin.x + size.x || world.z > origin.z + size.z)
                    continue;

                point = new Vector3(world.x, terrain.SampleHeight(world) + origin.y, world.z);
                normal = terrain.terrainData.GetInterpolatedNormal(
                    (world.x - origin.x) / size.x, (world.z - origin.z) / size.z);
                return true;
            }
            return false;
        }
    }
}
