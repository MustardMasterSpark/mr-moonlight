using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DDT.Samples
{
    public class PlayerController : MonoBehaviour
    {
        // Variables ///////////////////
        private NavMeshAgent m_NavMeshAgent;

        // Functions ///////////////////
        void Start()
        {
            m_NavMeshAgent = GetComponent<NavMeshAgent>();
        }

        void Update()
        {
            if (SampleInput.LeftMouseButtonDown())
            {
                Ray MouseRay = Camera.main.ScreenPointToRay(SampleInput.MousePosition);
                int GroundLayer = LayerMask.GetMask("Default");
                RaycastHit HitResult;
                if (Physics.Raycast(MouseRay, out HitResult, Mathf.Infinity, GroundLayer))
                {
                    // Sample position and get nearest valid position on the navigation mesh
                    NavMeshHit NavMeshHitResult;
                    bool IsValidPositionFound = NavMesh.SamplePosition(HitResult.point, out NavMeshHitResult, 3.0f, NavMesh.AllAreas); // Include all nav mesh area
                    if (IsValidPositionFound)
                    {
                        //DrawDebugTools.DrawSphere(NavMeshHitResult.position, 0.15f, 8, Color.green, 2.3f);

                        // Set agent to move to this position
                        if (m_NavMeshAgent == null)
                        {
                            Debug.LogError("Nav Mesh Agent component is null");
                        }
                        else
                        {
                            m_NavMeshAgent.SetDestination(NavMeshHitResult.position);
                        }
                    }
                }
            }
        }
    }
}
