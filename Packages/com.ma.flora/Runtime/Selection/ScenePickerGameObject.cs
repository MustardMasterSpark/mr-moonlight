// Copyright © Magnetic Arcade. All Rights Reserved.

#if UNITY_EDITOR
using UnityEngine;
using Object = UnityEngine.Object;

namespace MA.Flora
{
    [ExecuteAlways]
    internal class ScenePickerGameObject : MonoBehaviour
    {
        public static ScenePickerGameObject Instance { get; private set; }

        public Object Picked { get; set; }

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                DestroyImmediate(this);
                return;
            }

            Instance = this;
            gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
#endif
