using System;
using Burntwax;
using UnityEngine;


namespace Burntwax
{
    [CreateAssetMenu(fileName = "Shoot Config", menuName = "Guns/Shoot Config", order = 2)]
    public class ShootConfig : ScriptableObject
    {
        public bool isHitscan = false;
        public Bullet bulletPrefab;
        public LayerMask HitMask;
        public Vector3 Spread = new Vector3(0.1f, 0.1f, 0.1f);

        public int bulletsPerShot = 1;
        public float bulletSpeed;
    }

}