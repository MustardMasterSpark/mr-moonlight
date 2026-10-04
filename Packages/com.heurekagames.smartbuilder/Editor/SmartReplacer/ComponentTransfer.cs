using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HeurekaGames.SmartReplacer
{
    public class ComponentTransfer
    {
        public Object Source;
        public Object Target;

        public ComponentTransfer(Object source, Object target)
        {
            this.Source = source;
            this.Target = target;
        }
    }
}
