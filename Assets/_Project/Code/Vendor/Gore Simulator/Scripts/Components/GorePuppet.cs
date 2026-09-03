// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    public class GorePuppet : MonoBehaviour, IGoreObject
    {
        
        [Tooltip("Sets this bone inactive on execution and activates it on character reset.")]
        public bool setInactive = true;
        public GoreBone goreBone;

        private void Start()
        {
            if(setInactive && goreBone != null)
            {
                goreBone.goreSimulator.OnCharacterReset += SetActive;
            }
        }

        private void OnDestroy()
        {
            if(setInactive && goreBone != null)
            {
                goreBone.goreSimulator.OnCharacterReset -= SetActive;
            }
        }

        void SetActive()
        {
            gameObject.SetActive(true);
        }

        /********************************************************************************************************************************/

        /// <summary>
        ///     Executes a ragdoll cut with the given position and force.
        ///     The force is applied in the direction from the calculated world center of mass of the object towards the provided position.
        /// </summary>
        /// <param name="position">The position at which the ragdoll cut is executed.</param>
        /// <param name="force">The amount of force to apply to the ragdoll cut.</param>
        public void ExecuteRagdollCut(Vector3 position, float force)
        {
            goreBone.ExecuteRagdollCut(position, force);
            if(setInactive) gameObject.SetActive(false);
        }

        /// <summary>
        ///     Executes a Ragdoll Cut with force applied in world space.
        /// </summary>
        public void ExecuteRagdollCut(Vector3 position, Vector3 force)
        {
            goreBone.ExecuteRagdollCut(position, force);
            if(setInactive) gameObject.SetActive(false);
        }

        /* IGoreObject *****************************************************************************************************************/
        public void ExecuteCut(Vector3 position)
        {
            goreBone.ExecuteCut(position);
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteCut(Vector3 position, Vector3 force)
        {
            goreBone.ExecuteCut(position, force);
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteCut(Vector3 position, out GameObject detachedObject)
        {
            goreBone.ExecuteCut(position, out detachedObject);
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteCut(Vector3 position, Vector3 force, out GameObject detachedObject)
        {
            goreBone.ExecuteCut(position, force, out detachedObject);
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteCut(string boneName, Vector3 position)
        {
            goreBone.ExecuteCut(boneName, position);
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteCut(string boneName, Vector3 position, Vector3 force)
        {
            goreBone.ExecuteCut(boneName, position, force);
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteCut(string boneName, Vector3 position, out GameObject detachedObject)
        {
            goreBone.ExecuteCut(boneName, position, out detachedObject);
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteCut(string boneName, Vector3 position, Vector3 force, out GameObject detachedObject)
        {
            goreBone.ExecuteCut(boneName, position, force, out detachedObject);
            if(setInactive) gameObject.SetActive(false);
        }

        public void SpawnCutParticles(Vector3 position, Vector3 direction)
        {
            goreBone.SpawnCutParticles(position, direction);
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteExplosion()
        {
            goreBone.ExecuteExplosion();
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteExplosion(float radialForce)
        {
            goreBone.ExecuteExplosion(radialForce);
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteExplosion(Vector3 position, float force)
        {
            goreBone.ExecuteExplosion(position, force);
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteExplosion(out List<GameObject> explosionParts)
        {
            goreBone.ExecuteExplosion(out explosionParts);
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteExplosion(float radialForce, out List<GameObject> explosionParts)
        {
            goreBone.ExecuteExplosion(radialForce, out explosionParts);
            if(setInactive) gameObject.SetActive(false);
        }

        public void ExecuteExplosion(Vector3 position, float force, out List<GameObject> explosionParts)
        {
            goreBone.ExecuteExplosion(position, force, out explosionParts);
            if(setInactive) gameObject.SetActive(false);
        }
    }
}