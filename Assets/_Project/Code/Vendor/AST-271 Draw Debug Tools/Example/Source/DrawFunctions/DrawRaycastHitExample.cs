using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DDT.Samples
{
	public class DrawRaycastHitExample : MonoBehaviour
	{
		public GameObject m_Sphere_1;
		public GameObject m_Sphere_2;
		public Transform m_RaycastOrigin;

		private float		m_GridSize = 4.0f;
		private Vector3		m_Position;

		private float		m_MoveSpeed = 1.0f;
		private float		m_MoveLength = 4.0f;

        void Update()
		{
			// Move spheres
			Vector3 S1LocaPos = new Vector3(m_Sphere_1.transform.localPosition.x, m_Sphere_1.transform.localPosition.y, Mathf.Sin(Time.timeSinceLevelLoad * m_MoveSpeed) * m_MoveLength);
			Vector3 S2LocaPos = new Vector3(m_Sphere_2.transform.localPosition.x, m_Sphere_2.transform.localPosition.y, Mathf.Cos(Time.timeSinceLevelLoad * m_MoveSpeed * 0.6f) * m_MoveLength);
			m_Sphere_1.transform.localPosition = S1LocaPos;
			m_Sphere_2.transform.localPosition = S2LocaPos;

			// Draw grid
			DrawDebugTools.DrawGrid(transform.position, m_GridSize, 1.0f, 0.0f);

			// Draw raycasthit struct infos
			m_Position = transform.position + new Vector3(0.0f, 0.0f, 0.0f);


			RaycastHit Hit;
			Physics.Raycast(m_RaycastOrigin.position, m_RaycastOrigin.forward, out Hit, 10.0f);
			DrawDebugTools.DrawRaycastHit(m_RaycastOrigin.position, m_RaycastOrigin.forward, 10.0f, Hit, 0.0f);

			// Draw 3d label
			m_Position = transform.position + new Vector3(0.0f, 0.0f, -m_GridSize / 2.0f - 0.6f);
			DrawDebugTools.DrawString3D(m_Position, Quaternion.Euler(-90.0f, 180.0f, 0.0f), "RaycastHit", TextAnchor.LowerCenter, Color.white, 2.0f);
		}
	}
}
