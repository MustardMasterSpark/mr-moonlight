using UnityEngine;

namespace DDT
{
    [CreateAssetMenu(menuName = "DrawDebugTools/DDTSettings")]
    public class DDTSettings : ScriptableObject
    {
        [Header("Debug Camera")]
        [Tooltip("Freeze time while the debug camera is open")]
        public bool m_IsDebugCamFreezeTime = true;

        [Tooltip("Color of the shape marking your game camera")]
        public Color m_MainCamShapeColor = Color.red;

        [Tooltip("How much the speed up and slow down buttons change the time scale")]
        public float m_TimeControlStep = 0.1f;

        [Tooltip("Color of the surface normal drawn under the debug camera crosshair")]
        public Color m_DebugNormalVectColor = Color.red;

        [Header("Debug Camera Controls")]
        [Tooltip("Key that toggles the debug camera on and off")]
        public KeyCode m_ToggleDebugCameraKey = KeyCode.F9;

        [Tooltip("Key that moves the debug camera up")]
        public KeyCode m_DebugCamMoveUpKey = KeyCode.E;

        [Tooltip("Key that moves the debug camera down")]
        public KeyCode m_DebugCamMoveDownKey = KeyCode.Q;

        [Tooltip("Lowest and highest movement speed the mouse wheel can set")]
        public Vector2 m_DebugCamSpeedMultiplierRange = new Vector2(0.01f, 10.0f);

        [Header("Rendering")]
        [Tooltip("Layer index the debug shapes are drawn on. A camera whose culling mask "
                 + "excludes this layer will not show them.")]
        [Range(0, 31)]
        public int m_DrawLayer = 0;

        [Header("Float Graphs")]
        [Tooltip("How many samples a float graph keeps and plots")]
        [Range(8, 512)]
        public int m_FloatGraphSamplesCount = 100;

        [Header("Agent Path")]
        [Tooltip("Draw agent paths. Applies to every agent.")]
        public bool m_EnableAgentPathVisualization = true;
        [Tooltip("Radius of the ring on each path corner. The destination rings scale with it.")]
        public float m_SphereRadius = 0.2f;
        [Tooltip("Color of a path that reaches its destination")]
        public Color m_PathLineColor = new Color(0.0f, 0.792f, 0.298f, 1.0f);
        [Tooltip("Color of a path that stops short of its destination")]
        public Color m_PartialPathColor = new Color(1.0f, 0.72f, 0.15f, 1.0f);
        [Tooltip("Color of the path corner rings and the velocity arrow")]
        public Color m_PathPointColor = new Color(0.071f, 0.722f, 0.788f, 1.0f);
    }
}
