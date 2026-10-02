using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DDT.Samples
{
    public class DrawBillboardExample : MonoBehaviour
    {
        public DebugBillboard m_DebugBillboard;

        private float		m_GridSize = 4.0f;

        void Start()
        {
            DrawDebugTools.AddBillboardToDrawList(m_DebugBillboard);
        }

        void Update()
        {
            // Draw grid
            DrawDebugTools.DrawGrid(transform.position, m_GridSize, 1.0f, 0.0f);

            // Set billboard position
            m_DebugBillboard.Position = transform.position + new Vector3(0.0f, 1.0f, 0.0f);

            // Draw 3d label
            Vector3 TextPos = transform.position + new Vector3(0.0f, 0.0f, -m_GridSize / 2.0f - 0.6f);
            DrawDebugTools.DrawString3D(TextPos, Quaternion.Euler(-90.0f, 180.0f, 0.0f), "BILLBOARD", TextAnchor.MiddleCenter, Color.white, 2.0f);
        }
    }
}
