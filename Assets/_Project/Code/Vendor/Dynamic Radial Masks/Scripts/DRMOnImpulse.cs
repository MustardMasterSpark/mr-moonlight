// Dynamic Radial Masks <https://u3d.as/1w0H>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using UnityEngine;


namespace AmazingAssets.DynamicRadialMasks
{
    [AddComponentMenu("Amazing Assets/Dynamic Radial Masks/DRM On Impulse")]
    public class DRMOnImpulse : MonoBehaviour
    {
        public DRMLiveObjectsPool DRMLiveObjectsPool;

        public float impulseFrequency = 1;
        float deltaTime;


        public DRMLiveObject DRMLiveObject;


        private void Start()
        {
            deltaTime = impulseFrequency;
        }

        void Update()
        {
            deltaTime += Time.deltaTime;
            if (deltaTime > impulseFrequency)
            {
                deltaTime = 0;

                DRMLiveObjectsPool.AddItem(transform.position, DRMLiveObject);
            }
        }
    }
}
