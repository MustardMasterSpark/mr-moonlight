using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DDT.Samples
{
	public class DrawCameraExample : MonoBehaviour
	{
		private float m_GridSize = 4.0f;
		private Vector3 m_Position;

		void Update()
		{
			// Draw grid
			DrawDebugTools.DrawGrid(transform.position, m_GridSize, 1.0f, 0.0f);

			// Draw camera
			DrawDebugTools.DrawCamera(transform.GetChild(0).GetComponent<Camera>(), Color.cyan);

			// Draw 3d label
			m_Position = transform.position + new Vector3(0.0f, 0.0f, -m_GridSize / 2.0f - 0.6f);
			DrawDebugTools.DrawString3D(m_Position, Quaternion.Euler(-90.0f, 180.0f, 0.0f), "CAMERA", TextAnchor.LowerCenter, Color.white, 2.0f);

            DrawDebugTools.Log("Time: "+Time.realtimeSinceStartup, 0.6f);

            if (SampleInput.KKeyDown())
            {
	            DrawDebugTools.Log("KeyDown: "+Time.realtimeSinceStartup, 10.8f);
            }
		}
	}
}
