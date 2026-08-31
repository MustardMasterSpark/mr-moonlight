using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace Burntwax.Guns.Modifiers {
    public class DamageModifier : AbstractValueModifier<float> {
        public override void Apply(GunScriptableObject Gun) {
            try {
                MinMaxCurve damageCurve = GetAttribute<MinMaxCurve>(Gun, out object targetObjcet, out FieldInfo field);
            
                switch(damageCurve.mode) {
                    case ParticleSystemCurveMode.TwoConstants:
                        damageCurve.constantMin *= Amount;
                        damageCurve.constantMax *= Amount;
                        break;
                    case ParticleSystemCurveMode.TwoCurves:
                        damageCurve.curveMultiplier *= Amount;
                        break;
                    case ParticleSystemCurveMode.Curve:
                        damageCurve.curveMultiplier *= Amount;
                        break;
                    case ParticleSystemCurveMode.Constant:
                        damageCurve.constant *= Amount;
                        break;
                }
                field.SetValue(targetObjcet, damageCurve);
            }
            catch(InvalidPathSpecifiedException ) {}
        }
    }
}
    
