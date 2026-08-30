using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Burntwax.Guns
{
    public class Utilities
    {
        public static void CopyValues<T>(T Base, T Copy)
        {
            Type type = Base.GetType();
            foreach (FieldInfo field in type.GetFields())
            {
                field.SetValue(Copy, field.GetValue(Base));
            }
        }

    }
}


