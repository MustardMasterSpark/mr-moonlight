// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEngine;

namespace MA.Flora
{
    /// <summary>
    /// The script mediates the specific deinitialization order allowing to destroy the system after all scripts are disabled.
    /// It is private and hidden, but has executionOrder: 10000 in the meta file
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    internal class FloraSystemInitializationProxy : MonoBehaviour
    {
        public bool IsActive;

        public void OnEnable()
        {
            if (!IsActive)
                return;

            IsActive = false;
            DestroyImmediate(gameObject);
        }

        public void OnDisable()
        {
            if (IsActive)
                FloraSystem.DomainUnloadOrPlayModeChangeShutdown();
        }
    }
}
