using System;
using Burntwax.Guns;
using UnityEngine;

namespace Burntwax
{

    [CreateAssetMenu(fileName = "Crosshair Config", menuName = "Guns/Crosshair Config", order = 6)]
    public class CrosshairConfig : ScriptableObject, ICloneable
    {
        public Sprite icon;

        public Vector2 dimensions;

        public object Clone()
        {
            CrosshairConfig config = CreateInstance<CrosshairConfig>();
            Utilities.CopyValues(this, config);
            return config;
        }
    }

}