using System;

using UnityEngine;

namespace Burntwax
{

    [CreateAssetMenu(fileName = "Ammo Config", menuName = "Guns/Ammo Config", order = 3)]
    public class AmmoConfig : ScriptableObject
    {

        public int maxAmmo;
        public int clipSize;

        public int currentAmmo;
        public int currentClip;


        public void OnEnable()
        {
            
            currentClip = clipSize;
            currentAmmo = maxAmmo;
        }

        public void Reload()
        {
            int maxReloadAmount = Mathf.Min(clipSize, currentAmmo);
            int availableBullets = clipSize - currentClip;
            int reloadAmount = Mathf.Min(maxReloadAmount, availableBullets);
            currentClip = currentClip + reloadAmount;
            currentAmmo -= reloadAmount;
        }

        public void IncrementalReload()
        {
            currentClip++;
            currentAmmo--;
        }


        public bool CanReload()
        {
            return currentClip < clipSize && currentAmmo > 0;
        }
    }

}