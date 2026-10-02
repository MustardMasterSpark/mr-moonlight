using UnityEngine;
using UnityEngine.AI;

namespace DDT
{
    public class AgentPathVisualizerComp : MonoBehaviour
    {
        private const float PathLift = 0.05f;
        private const float DestinationPoleHeight = 1.0f;

        private static readonly Color LabelColor = new Color(0.949f, 0.961f, 0.969f, 1.0f);

        private NavMeshAgent m_NavMeshAgent;

        private void Start()
        {
            m_NavMeshAgent = GetComponent<NavMeshAgent>();
        }

        private void Update()
        {
            if (m_NavMeshAgent == null || !m_NavMeshAgent.hasPath)
                return;

            DDTSettings Settings = DrawDebugTools.Instance != null ? DrawDebugTools.Instance.m_DDTSettings : null;
            if (Settings == null || !Settings.m_EnableAgentPathVisualization)
                return;

            // NavMeshAgent.path and its corners allocate on every read
            Vector3[] Corners = m_NavMeshAgent.path.corners;
            if (Corners.Length < 2)
                return;

            bool IsPartial = m_NavMeshAgent.pathStatus != NavMeshPathStatus.PathComplete;
            Color PathColor = IsPartial ? Settings.m_PartialPathColor : Settings.m_PathLineColor;
            Color CornerColor = Settings.m_PathPointColor;
            float CornerRadius = Settings.m_SphereRadius;
            Vector3 Lift = Vector3.up * PathLift;

            // Draws the path, with a ring on each corner between the agent and its destination
            float Remaining = 0.0f;
            for (int i = 1; i < Corners.Length; i++)
            {
                Remaining += Vector3.Distance(Corners[i - 1], Corners[i]);
                DrawDebugTools.DrawLine(Corners[i - 1] + Lift, Corners[i] + Lift, PathColor);
                if (i < Corners.Length - 1)
                    DrawDebugTools.DrawCircle(Corners[i] + Lift, CornerRadius, 16, CornerColor);
            }

            // Draws the agent footprint at its radius and its velocity
            Vector3 AgentPos = Corners[0] + Lift;
            DrawDebugTools.DrawCircle(AgentPos, m_NavMeshAgent.radius, 28, PathColor);
            Vector3 Velocity = m_NavMeshAgent.velocity;
            if (Velocity.sqrMagnitude > 0.01f)
                DrawDebugTools.DrawDirectionalArrow(AgentPos, AgentPos + Velocity * 0.5f, 0.2f, CornerColor);

            // Marks the destination and labels the remaining distance
            Vector3 End = Corners[Corners.Length - 1] + Lift;
            DrawDebugTools.DrawCircle(End, CornerRadius * 1.5f, 20, PathColor);
            DrawDebugTools.DrawCircle(End, CornerRadius * 3.0f, 28, PathColor);
            DrawDebugTools.DrawLine(End, End + Vector3.up * DestinationPoleHeight, PathColor);

            string Label = Remaining.ToString("F1") + (IsPartial ? " m, partial" : " m");
            DrawDebugTools.DrawString3D(End + Vector3.up * (DestinationPoleHeight + 0.1f), Label,
                TextAnchor.LowerCenter, IsPartial ? PathColor : LabelColor);
        }
    }
}
