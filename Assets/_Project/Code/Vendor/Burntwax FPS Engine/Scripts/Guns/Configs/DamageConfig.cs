using System;
using Burntwax.Guns;
using UnityEngine;
using static UnityEngine.ParticleSystem;
using Random = UnityEngine.Random;

namespace Burntwax
{

    [CreateAssetMenu(fileName = "Damage Config", menuName = "Guns/Damage Config", order = 1)]
    public class DamageConfig : ScriptableObject, ICloneable
    {
        public MinMaxCurve damageCurve;

        private void Reset()
        {
            damageCurve.mode = ParticleSystemCurveMode.Curve;
        }

        public int GetDamage(float distance = 0)
        {
            return Mathf.CeilToInt(damageCurve.Evaluate(distance, Random.value));
        }

        public object Clone()
        {
            DamageConfig config = CreateInstance<DamageConfig>();
            Utilities.CopyValues(this, config);
            return config;
        }
    }
}
