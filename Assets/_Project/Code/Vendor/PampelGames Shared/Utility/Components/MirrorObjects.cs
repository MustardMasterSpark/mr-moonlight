// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PampelGames.Shared.Utility
{
    [AddComponentMenu("Pampel Games/Shared/Mirror Objects")]
    [PGEditorAuto]
    public class MirrorObjects : MonoBehaviour
    {
        public PGEnums.Axis axis = PGEnums.Axis.X;
        public bool duplicate = true;
        public string suffixOld = "_R";
        public string suffixNew = "_L";

        public List<GameObject> targets = new();

        [PGButtonMethod(nameof(Mirror))] public string mirror;

        public void Mirror()
        {
            foreach (var target in targets)
            {
                if (!target) continue;

                var _obj = target;
                if (duplicate)
                {
#if UNITY_EDITOR
                    var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(target);

                    if (prefabSource)
                        _obj = (GameObject) PrefabUtility.InstantiatePrefab(prefabSource, target.transform.parent);
                    else
                        _obj = Instantiate(target, target.transform.parent);

                    Undo.RegisterCreatedObjectUndo(_obj, "Mirror Object");
#else
                    _obj = Instantiate(target, target.transform.parent);
#endif
                    if (!string.IsNullOrEmpty(suffixOld)) _obj.name = target.name.Replace(suffixOld, suffixNew);
                }
                else
                {
#if UNITY_EDITOR
                    Undo.RecordObject(_obj.transform, "Mirror Object");
#endif
                }

                var pos = target.transform.localPosition;
                var rot = target.transform.localRotation;
                var scale = target.transform.localScale;

                Vector3 forward = rot * Vector3.forward;
                Vector3 up = rot * Vector3.up;

                switch (axis)
                {
                    case PGEnums.Axis.X:
                        pos.x *= -1;
                        forward.x *= -1;
                        up.x *= -1;
                        break;

                    case PGEnums.Axis.Y:
                        pos.y *= -1;
                        forward.y *= -1;
                        up.y *= -1;
                        break;

                    case PGEnums.Axis.Z:
                        pos.z *= -1;
                        forward.z *= -1;
                        up.z *= -1;
                        break;
                }

                var mirroredRot = Quaternion.LookRotation(forward, up);

                _obj.transform.localPosition = pos;
                _obj.transform.localRotation = mirroredRot;
                _obj.transform.localScale = scale;
            }
        }
    }
}