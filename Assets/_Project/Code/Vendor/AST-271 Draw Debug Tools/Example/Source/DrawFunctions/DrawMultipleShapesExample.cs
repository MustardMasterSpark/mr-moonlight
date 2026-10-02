using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DDT.Samples
{
	public class DrawMultipleShapesExample : MonoBehaviour
	{
		private float m_GridSize = 6.0f;
		private Vector3 m_Position;
		private float m_Time = 0.0f;
		private float m_DrawSphereGhostTime = 0.2f;
		private float m_DrawSphereTrailTimeCounter = 0.0f;
		private Vector3 m_DrawSphereTrailLastPos;

		void Update()
		{
			// Draw grid
			DrawDebugTools.DrawGrid(transform.position, m_GridSize, 1.0f, 0.0f);

			// Draw box
			DrawDebugTools.DrawBox(transform.position + new Vector3(0.0f, m_GridSize/4.0f, 0.0f), Quaternion.identity, new Vector3(m_GridSize, m_GridSize/2.0f, m_GridSize), Color.cyan);

			// Increate time
			m_Time += Time.deltaTime;

			m_Position = transform.position + new Vector3(Mathf.Sin(m_Time) * 2.0f, 2.0f + Mathf.Sin(m_Time * 3.0f), Mathf.Cos(m_Time) * 2.0f);
			DrawDebugTools.DrawSphere(m_Position, 0.4f, 4, Color.green);

			// Draw sphere trail
			m_DrawSphereTrailTimeCounter += Time.deltaTime;
			if (m_DrawSphereTrailTimeCounter > m_DrawSphereGhostTime)
			{
				DrawDebugTools.DrawSphere(m_Position, 0.2f, 4, Color.yellow, 1.0f);
				DrawDebugTools.DrawLine(m_Position, m_DrawSphereTrailLastPos, Color.red, 1.0f);
				m_DrawSphereTrailTimeCounter = 0.0f;
				m_DrawSphereTrailLastPos = m_Position;
			}

			// Draw sphere arroes
			DrawDebugTools.DrawDirectionalArrow(transform.position, m_Position, 0.1f, Color.magenta);
			DrawDebugTools.DrawDirectionalArrow(transform.position, Vector3.ProjectOnPlane(m_Position, Vector3.up), 0.1f, Color.magenta);

			// Draw distance
			DrawDebugTools.DrawDistance(m_Position, Vector3.ProjectOnPlane(m_Position, Vector3.up), Color.blue);

			// Draw float graph
			DrawDebugTools.DrawFloatGraph("Sin Value", Mathf.Sin(m_Time * 3.0f));
			DrawDebugTools.DrawFloatGraph("Mouse Pos X", SampleInput.MousePosition.x, Color.red);
			DrawDebugTools.DrawFloatGraph("Mouse Pos Y", SampleInput.MousePosition.y, Color.green);

			// Log delta time
			DrawDebugTools.Log("Delta time = " + Time.deltaTime, Color.white);
		}
	}
}
