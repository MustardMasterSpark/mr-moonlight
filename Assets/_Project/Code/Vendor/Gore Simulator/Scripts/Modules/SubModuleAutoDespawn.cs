// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using PampelGames.Shared.Tools;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    [Serializable]
    public class SubModuleAutoDespawn : SubModuleBase
    {
        public override string ModuleName()
        {
            return "Auto Despawn";
        }

        public override string ModuleInfo()
        {
            return "Additional despawn logic for cut mesh parts.";
        }

        public override int imageIndex()
        {
            return 9;
        }


        public override bool CompatibleRagdoll()
        {
            return false;
        }

        public override void ModuleAdded(Type type)
        {
            if (type == typeof(GoreModuleExplosion)) allowShrink = false;
        }

        public float despawnTimer = 5f;
        public bool shrink;
        public float startShrinking = 0.75f;
        public bool move;
        public Vector3 moveVector = Vector3.down;
        public float startMoving = 0.75f;

        public bool allowShrink = true;

        /********************************************************************************************************************************/

        public override void ExecuteModuleCut(SubModuleClass subModuleClass)
        {
            for (var i = 0; i < subModuleClass.children.Count; i++)
            {
                var childObj = subModuleClass.children[i].gameObject;
                if (!childObj.TryGetComponent<DetachedChild>(out var detachedChild)) continue;
                detachedChild.detached = true;
                PGScheduler.ScheduleTime(childObj.GetComponent<MonoBehaviour>(), despawnTimer, () => SetInactive(detachedChild));
                _goreSimulator.AddDetachedChild(detachedChild.gameObject);
            }
        }

        public override void ExecuteModuleExplosion(SubModuleClass subModuleClass)
        {
            if (!Application.isPlaying) return;
            for (var i = 0; i < subModuleClass.children.Count; i++)
            {
                var childObj = subModuleClass.children[i].gameObject;
                if (!childObj.TryGetComponent<DetachedChild>(out var detachedChild)) continue;
                detachedChild.detached = true;
                if (childObj.activeInHierarchy)
                    PGScheduler.ScheduleTime(childObj.GetComponent<MonoBehaviour>(), despawnTimer, () => SetInactive(detachedChild));
                _goreSimulator.AddDetachedChild(detachedChild.gameObject);
            }
        }

        public override void FinalizeExecution(List<GameObject> poolableObjects, List<GameObject> destroyableObjects)
        {
            base.FinalizeExecution(poolableObjects, destroyableObjects);

            if (shrink && startShrinking < 1)
            {
                if (destroyableObjects.Count == 0)
                    foreach (var poolableObject in poolableObjects)
                    {
                        var shrinkObject = poolableObject.AddComponent<ShrinkObject>();
                        PGScheduler.ScheduleTime(poolableObject.GetComponent<MonoBehaviour>(), despawnTimer * startShrinking, () =>
                        {
                            if (shrinkObject == null) return;
                            shrinkObject.StartShrinking(despawnTimer - despawnTimer * startShrinking);
                        });
                    }
                else
                    foreach (var destroyableObject in destroyableObjects)
                    {
                        var shrinkObject = destroyableObject.AddComponent<ShrinkObject>();
                        PGScheduler.ScheduleTime(destroyableObject.GetComponent<MonoBehaviour>(), despawnTimer * startShrinking, () =>
                        {
                            if (shrinkObject == null) return;
                            shrinkObject.StartShrinking(despawnTimer - despawnTimer * startShrinking);
                        });
                    }
            }

            if (move && startMoving < 1)
            {
                if (destroyableObjects.Count == 0)
                    foreach (var poolableObject in poolableObjects)
                    {
                        if(!poolableObject.TryGetComponent<MoveObject>(out var moveObject))
                            moveObject = poolableObject.AddComponent<MoveObject>();
                        moveObject.ResetValues();
                        PGScheduler.ScheduleTime(poolableObject.GetComponent<MonoBehaviour>(), despawnTimer * startShrinking, () =>
                        {
                            if (moveObject == null) return;
                            moveObject.StartMoving(despawnTimer - despawnTimer * startShrinking, moveVector);
                        });
                    }
                else
                    foreach (var destroyableObject in destroyableObjects)
                    {
                        if(!destroyableObject.TryGetComponent<MoveObject>(out var moveObject))
                            moveObject = destroyableObject.AddComponent<MoveObject>();
                        moveObject.ResetValues();
                        PGScheduler.ScheduleTime(destroyableObject.GetComponent<MonoBehaviour>(), despawnTimer * startShrinking, () =>
                        {
                            if (moveObject == null) return;
                            moveObject.StartMoving(despawnTimer - despawnTimer * startShrinking, moveVector);
                        });
                    }
            }

            if (destroyableObjects.Count > 0)
                foreach (var destroyableObject in destroyableObjects)
                {
                    var mono = destroyableObject.GetComponent<MonoBehaviour>();
                    PGScheduler.ScheduleTime(mono, despawnTimer, () => { DespawnDestroyableObject(destroyableObject); });
                }

            if (poolableObjects.Count > 0)
                foreach (var poolableObject in poolableObjects)
                {
                    var mono = poolableObject.GetComponent<MonoBehaviour>();
                    PGScheduler.ScheduleTime(mono, despawnTimer, () => { DespawnPoolableObject(poolableObject); });
                }
        }

        /********************************************************************************************************************************/

        private void SetInactive(DetachedChild detachedChild)
        {
            if (detachedChild.detached) detachedChild.gameObject.SetActive(false);
        }

        private void DespawnPoolableObject(GameObject poolableObject)
        {
            var settings = _goreSimulator._globalSettings;
            Pool.Release(settings.poolCutActive, _goreSimulator.excludedComponentTypes, poolableObject, false);
        }

        private void DespawnDestroyableObject(GameObject destroyableObject)
        {
            var settings = _goreSimulator._globalSettings;
            Pool.Release(false, _goreSimulator.excludedComponentTypes, destroyableObject, false);
        }

        /********************************************************************************************************************************/

        public override void ExecuteModuleRagdoll(List<GoreBone> goreBones)
        {
        }

        public override void Reset()
        {
        }

        /********************************************************************************************************************************/
    }


    public class ShrinkObject : MonoBehaviour
    {
        private float _shrinkTime;
        private bool started;
        private Vector3 originalScale;
        private float elapsedTime;


        public void StartShrinking(float shrinkTime)
        {
            if (gameObject.TryGetComponent<MeshPart>(out var meshPart)) return;

            if (gameObject.TryGetComponent<Collider>(out var _collider)) Destroy(_collider);
            if (gameObject.TryGetComponent<Rigidbody>(out var _rigid)) Destroy(_rigid);

            _shrinkTime = shrinkTime;
            originalScale = transform.localScale;
            started = true;
        }

        private void Update()
        {
            if (!started) return;
            if (!(elapsedTime < _shrinkTime)) return;
            elapsedTime += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, Vector3.one * 0.01f, elapsedTime / _shrinkTime);
        }
    }

    public class MoveObject : MonoBehaviour
    {
        private float _moveTime;
        private bool started;
        private Vector3 originalPosition;
        private Vector3 deltaPosition;
        private float elapsedTime;

        public void ResetValues()
        {
            _moveTime = 0;
            started = false;
            elapsedTime = 0;
            originalPosition = Vector3.zero;
            deltaPosition = Vector3.zero;
        }

        public void StartMoving(float moveTime, Vector3 _deltaPosition)
        {
            if (!gameObject.TryGetComponent<MeshPart>(out var meshPart))
            {
                if (gameObject.TryGetComponent<Collider>(out var _collider)) Destroy(_collider);
                if (gameObject.TryGetComponent<Rigidbody>(out var _rigid)) Destroy(_rigid);
            }

            _moveTime = moveTime;
            originalPosition = transform.position;
            deltaPosition = originalPosition + _deltaPosition;
            started = true;
        }

        private void Update()
        {
            if (!started) return;
            if (!(elapsedTime < _moveTime)) return;
            elapsedTime += Time.deltaTime;
            transform.position = Vector3.Lerp(originalPosition, deltaPosition, elapsedTime / _moveTime);
        }
    }
}