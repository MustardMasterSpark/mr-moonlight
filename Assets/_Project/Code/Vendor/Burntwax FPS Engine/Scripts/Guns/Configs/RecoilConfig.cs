using System;
using Burntwax.Guns;
using UnityEngine;

namespace Burntwax
{
    [CreateAssetMenu(fileName = "Recoil Config", menuName = "Guns/Recoil Config", order = 2)]
    public class RecoilConfig : ScriptableObject, ICloneable
    {

        public float VerticalRecoil;

        public object Clone()
        {
            RecoilConfig config = CreateInstance<RecoilConfig>();
            Utilities.CopyValues(this, config);
            return config;
        }
    }
}
