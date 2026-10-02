using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DDT.Samples
{
	public class DrawObjectCollidersExample : MonoBehaviour
	{
		public GameObject m_Cube;

		private Vector3		m_CubeInitialLocation;
		private float		m_GridSize = 4.0f;
		private Vector3		m_Position;

		private float		m_CubeSpawnTime = 2.0f;
		private float		m_CubeSpawnTimeCounter = 0.0f;

		void Start()
		{
			m_CubeInitialLocation = m_Cube.transform.position;
		}
        void Update()
		{
			// Draw grid
			DrawDebugTools.DrawGrid(transform.position, m_GridSize, 1.0f, 0.0f);

			// Draw shape
			m_Position = transform.position + new Vector3(0.0f, 0.0f, 0.0f);
			DrawDebugTools.DrawObjectColliders(gameObject, Color.green);

			// Spawn cubes
			m_CubeSpawnTimeCounter += Time.deltaTime;
			if (m_CubeSpawnTimeCounter > m_CubeSpawnTime)
			{
				GameObject.Instantiate(m_Cube, m_CubeInitialLocation + new Vector3(Random.value * 0.5f, 2.0f, Random.value * 0.5f), Quaternion.identity, transform);
				m_CubeSpawnTimeCounter = 0.0f;
			}

			// Draw 3d label
			m_Position = transform.position + new Vector3(0.0f, 0.0f, -m_GridSize / 2.0f - 0.6f);
			DrawDebugTools.DrawString3D(m_Position, Quaternion.Euler(-90.0f, 180.0f, 0.0f), "OBJECT COLLIDERS", TextAnchor.LowerCenter, Color.white, 2.0f);
		}
	}
}
