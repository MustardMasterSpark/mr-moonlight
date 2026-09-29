using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DDT.Samples
{
	public class DrawPlaneExample : MonoBehaviour
	{
		private float m_GridSize = 4.0f;
		private Vector3 m_Position;
		private Plane m_Plane;

		private void Start()
		{
			m_Plane = new Plane(new Vector3(-0.3f, 1.0f, 0.6f), 2.0f);
		}

		void Update()
		{
			// Draw shape
			m_Position = transform.position + new Vector3(0.0f, 1.0f, 0.0f);
			DrawDebugTools.DrawPlane(m_Position, m_Plane, 3.0f);

			// Draw 3d label
			m_Position = transform.position + new Vector3(0.0f, 0.0f, -m_GridSize / 2.0f - 0.6f);
			DrawDebugTools.DrawString3D(m_Position, Quaternion.Euler(-90.0f, 180.0f, 0.0f), "PLANE", TextAnchor.LowerCenter, Color.white, 2.0f);
		}
	}
}