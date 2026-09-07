// Dynamic Radial Masks <https://u3d.as/1w0H>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using UnityEngine;


namespace AmazingAssets.DynamicRadialMasks
{
    [AddComponentMenu("Amazing Assets/Dynamic Radial Masks/DRM On Collision")]
    public class DRMOnCollision : MonoBehaviour
    {
        public DRMLiveObjectsPool[] DRMLiveObjectsPools;

        public DRMLiveObject DRMLiveObject = new DRMLiveObject();


        void OnCollisionEnter(Collision collision)
        {
            if (DRMLiveObjectsPools != null && DRMLiveObjectsPools.Length != 0)
            {
                if (collision != null && collision.contacts != null && collision.contacts.Length > 0)
                {
                    int poolIndex = DRMLiveObjectsPools.Length == 1 ? 0 : Random.Range(0, DRMLiveObjectsPools.Length);
                    if (DRMLiveObjectsPools[poolIndex] != null)
                    {
                        DRMLiveObjectsPools[poolIndex].AddItem(collision.contacts[0].point, DRMLiveObject);    //Add item 
                    }
                }
            }  
        }  
         
         
        private void Reset()
        {
            DRMLiveObject.Reset();
        }
    }
}
