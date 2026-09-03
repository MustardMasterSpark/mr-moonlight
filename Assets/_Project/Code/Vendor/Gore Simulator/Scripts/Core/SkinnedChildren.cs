// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    internal static class SkinnedChildren
    {

        public static List<Transform> CreateSkinnedChildren(GoreSimulator _goreSimulator)
        {
            List<Transform> detachedSkinnedChildren = new List<Transform>();
            for (int i = 0; i < _goreSimulator.skinnedChildren.Count; i++)
            {
                if(!_goreSimulator.skinnedChildren[i].enabled) continue;
                _goreSimulator.skinnedChildren[i].BakeMesh(_goreSimulator.skinnedChildrenBakedMeshes[i]);
                var detachedSkinnedChild = ObjectCreationUtility.CreateMeshObject(_goreSimulator, _goreSimulator.skinnedChildrenBakedMeshes[i],
                    _goreSimulator.gameObject.name + " - " + _goreSimulator.skinnedChildren[i].name, BoneTag.Child);
                detachedSkinnedChild.transform.SetPositionAndRotation(_goreSimulator.skinnedChildren[i].transform.position, _goreSimulator.skinnedChildren[i].transform.rotation);
                if (detachedSkinnedChild.TryGetComponent<Renderer>(out var skinnedChildRenderer))
                    skinnedChildRenderer.sharedMaterials = _goreSimulator.skinnedChildren[i].sharedMaterials;
                _goreSimulator.skinnedChildren[i].enabled = false;
                _goreSimulator.AddDetachedObject(detachedSkinnedChild);
                detachedSkinnedChild.AddComponent<DetachedChild>();
                detachedSkinnedChildren.Add(detachedSkinnedChild.transform);
            }

            return detachedSkinnedChildren;
        }
        
        public static List<Transform> CreateFixedChildren(GoreSimulator _goreSimulator)
        {
            var fixedChildren = new List<Transform>();
            for (int i = 0; i < _goreSimulator.fixedChildren.Count; i++)
            {
                if(!_goreSimulator.fixedChildren[i].gameObject.activeInHierarchy) continue;

                if (_goreSimulator.fixedChildren[i].TryGetComponent<SkinnedMeshRenderer>(out var smr))
                {
                    var bakedMesh = new Mesh();
                    smr.BakeMesh(bakedMesh);
                    var fixedChild = ObjectCreationUtility.CreateMeshObject(_goreSimulator, bakedMesh,
                        _goreSimulator.gameObject.name + " - " + _goreSimulator.fixedChildren[i].name, BoneTag.Child, false);
                    fixedChild.transform.SetPositionAndRotation(_goreSimulator.fixedChildren[i].transform.position, _goreSimulator.fixedChildren[i].transform.rotation);
                    if (fixedChild.TryGetComponent<Renderer>(out var skinnedChildRenderer))
                        skinnedChildRenderer.sharedMaterials = smr.sharedMaterials;
                    fixedChild.AddComponent<DetachedChild>();
                    fixedChildren.Add(fixedChild.transform);
                    _goreSimulator.AddDestroyableObject(fixedChild);
                }
                else
                {
                    var fixedChild = Object.Instantiate(_goreSimulator.fixedChildren[i].gameObject, _goreSimulator.fixedChildren[i].transform.position, _goreSimulator.fixedChildren[i].transform.rotation);
                    fixedChild.transform.SetParent(null);
                    fixedChild.AddComponent<DetachedChild>();
                    fixedChildren.Add(fixedChild.transform);
                    _goreSimulator.AddDestroyableObject(fixedChild);    
                }
                
                _goreSimulator.fixedChildren[i].gameObject.SetActive(false);
            }

            return fixedChildren;
        }
        
        public static List<Transform> CreateSkinnedChildrenForBone(GoreSimulator _goreSimulator, Transform bone)
        {
            var detachedSkinnedChildren = new List<Transform>();
            for (int i = 0; i < _goreSimulator.skinnedChildren.Count; i++)
            {
                if(!_goreSimulator.skinnedChildren[i].enabled) continue;
                if(!_goreSimulator.skinnedChildren[i].rootBone.IsChildOf(bone) && 
                   !bone.IsChildOf(_goreSimulator.skinnedChildren[i].rootBone)) continue;
                
                _goreSimulator.skinnedChildren[i].BakeMesh(_goreSimulator.skinnedChildrenBakedMeshes[i]);
                var detachedSkinnedChild = ObjectCreationUtility.CreateMeshObject(_goreSimulator, _goreSimulator.skinnedChildrenBakedMeshes[i],
                    _goreSimulator.gameObject.name + " - " + _goreSimulator.skinnedChildren[i].name, BoneTag.Child);
                detachedSkinnedChild.transform.SetPositionAndRotation(_goreSimulator.skinnedChildren[i].transform.position, _goreSimulator.skinnedChildren[i].transform.rotation);
                if (detachedSkinnedChild.TryGetComponent<Renderer>(out var skinnedChildRenderer))
                    skinnedChildRenderer.materials = _goreSimulator.skinnedChildren[i].materials;
                _goreSimulator.skinnedChildren[i].enabled = false;
                _goreSimulator.AddDetachedObject(detachedSkinnedChild);
                detachedSkinnedChild.AddComponent<DetachedChild>();
                detachedSkinnedChildren.Add(detachedSkinnedChild.transform);
            }

            return detachedSkinnedChildren;
        }
        
        public static List<Transform> CreateFixedChildrenForBone(GoreSimulator _goreSimulator, Transform bone)
        {
            var fixedChildren = new List<Transform>();
            for (int i = 0; i < _goreSimulator.fixedChildren.Count; i++)
            {
                var fixedChild = _goreSimulator.fixedChildren[i];
                if(!fixedChild.gameObject.activeInHierarchy) continue;
                if(!fixedChild.IsChildOf(bone)) continue;
                
                if (_goreSimulator.fixedChildren[i].TryGetComponent<SkinnedMeshRenderer>(out var smr))
                {
                    var bakedMesh = new Mesh();
                    smr.BakeMesh(bakedMesh);
                    var fixedChildNew = ObjectCreationUtility.CreateMeshObject(_goreSimulator, bakedMesh,
                        _goreSimulator.gameObject.name + " - " + _goreSimulator.fixedChildren[i].name, BoneTag.Child, false);
                    fixedChildNew.transform.SetPositionAndRotation(_goreSimulator.fixedChildren[i].transform.position, _goreSimulator.fixedChildren[i].transform.rotation);
                    if (fixedChildNew.TryGetComponent<Renderer>(out var skinnedChildRenderer))
                        skinnedChildRenderer.sharedMaterials = smr.sharedMaterials;
                    fixedChildNew.AddComponent<DetachedChild>();
                    fixedChildren.Add(fixedChildNew.transform);
                    _goreSimulator.AddDestroyableObject(fixedChildNew);
                }
                else
                {
                    var detachedFixedChild = Object.Instantiate(fixedChild.gameObject, fixedChild.transform.position, fixedChild.transform.rotation);
                    detachedFixedChild.AddComponent<DetachedChild>();
                    _goreSimulator.AddDestroyableObject(detachedFixedChild);
                    fixedChildren.Add(detachedFixedChild.transform);
                }
            }

            return fixedChildren;
        }
    }
}