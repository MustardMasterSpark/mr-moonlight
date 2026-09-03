// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Tools;
using PampelGames.Shared.Utility;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace PampelGames.GoreSimulator
{
    [Serializable]
    public class SubModuleParticleEffects : SubModuleBase
    {
        public override string ModuleName()
        {
            return "Spawn Effects";
        }

        public override string ModuleInfo()
        {
            return "Object spawner utilizing an automated, shared pool.\n\n" +
                   "GameObjects with a component inheriting from 'PGIExecutable' are automatically executed upon spawning.";
        }

        public override int imageIndex()
        {
            return 7;
        }

        public override void ModuleAdded(Type type)
        {
            base.ModuleAdded(type);
            addedType = type.Name;
            particleClasses ??= new List<ParticleWrapperClass>();
            particleClasses.Add(new ParticleWrapperClass());
#if UNITY_EDITOR
            if (type == typeof(GoreModuleCut))
                particleClasses[^1].particle = _goreSimulator._defaultReferences.cutParticle;
            else if (type == typeof(GoreModuleExplosion)) particleClasses[^1].particle = _goreSimulator._defaultReferences.explosionParticle;
#endif
        }

        public override bool CompatibleRagdoll()
        {
            return false;
        }

        /********************************************************************************************************************************/

        public List<ParticleWrapperClass> particleClasses = new();
        public string addedType;
        
        internal readonly List<GameObject> activeSystems = new();
        
        [Serializable]
        public class ParticleWrapperClass
        {
            public Enums.SpawnType spawnType = Enums.SpawnType.ParticleSystem;
            public GameObject effect;
            public ParticleSystem particle;
            public bool autoDespawn = true;
            public float autoDespawnTimer = 5f;
            public Enums.ParticlePositionExpl positionExpl = Enums.ParticlePositionExpl.Center;
            public Vector3 positionOffset = Vector3.zero;
            public int maxPosition = 5;
            public bool setParentExplosionParts = true;
            public Enums.ParticleRotationExpl rotationExpl = Enums.ParticleRotationExpl.Method;
            public Enums.ParticleRotationCut rotationCut = Enums.ParticleRotationCut.CutDirection;
            public Enums.ParticleSetParent setParent = Enums.ParticleSetParent.Character;
            public BoneTag boneTag = BoneTag.All;
        }
        
        

        /********************************************************************************************************************************/

        public override void Initialize()
        {
#if UNITY_EDITOR
            for (int i = 0; i < particleClasses.Count; i++)
            {
                if(particleClasses[i].spawnType == Enums.SpawnType.ParticleSystem && particleClasses[i].particle == null)
                {
                    Debug.LogError("Particle System missing on Gore Simulator: " + _goreSimulator.gameObject.name);
                    return;
                }
                if(particleClasses[i].spawnType == Enums.SpawnType.Gameobject && particleClasses[i].effect == null)
                {
                    Debug.LogError("Effect missing on Gore Simulator: " + _goreSimulator.gameObject.name);
                    return;
                }
            }
#endif

            base.Initialize();

            if(_goreSimulator._globalSettings.poolEffectsActive && Application.isPlaying)
            {
                for (var i = 0; i < particleClasses.Count; i++)
                {
                    if (particleClasses[i].spawnType == Enums.SpawnType.ParticleSystem)
                    {
                        InitializePool(particleClasses[i].particle.gameObject, _goreSimulator._globalSettings.effectsPreload);
                    }
                    else
                    {
                        InitializePool(particleClasses[i].effect, _goreSimulator._globalSettings.effectsPreload);
                    }
                }
            }
            
        }
        
        
        /********************************************************************************************************************************/

        public override void ExecuteModuleCut(SubModuleClass subModuleClass)
        {
            var cutPosition = subModuleClass.cutPosition;

            if (cutPosition == Vector3.zero) return;
            if(subModuleClass.subModuleObjectClasses.Count == 0) return;

            
            for (var i = 0; i < particleClasses.Count; i++)
            {
                if (particleClasses[i].boneTag != BoneTag.All)
                {
                    if (subModuleClass.subModuleObjectClasses[0].boneTag != particleClasses[i].boneTag) continue;
                }
                
                var rotation = GetRotationCut(particleClasses[i], subModuleClass);
                var rotationInversed = Quaternion.Euler(-rotation.eulerAngles);

                if (particleClasses[i].rotationCut == Enums.ParticleRotationCut.CutDirection)
                {
                    if(subModuleClass.multiCut) continue;

                    if (particleClasses[i].setParent == Enums.ParticleSetParent.None)
                    {
                        var pooledObj = GetSpawnObject(i, cutPosition, rotation);
                        ProcessSpawnedObject(pooledObj, i);
                    }
                    else if (particleClasses[i].setParent == Enums.ParticleSetParent.Character)
                    {
                        var pooledObj = GetSpawnObject(i, cutPosition, rotation);
                        pooledObj.transform.SetParent(subModuleClass.centerBone);  
                        ProcessSpawnedObject(pooledObj, i);
                    }
                    else if (particleClasses[i].setParent == Enums.ParticleSetParent.DetachedPart)
                    {
                        var pooledObj = GetSpawnObject(i, cutPosition, rotationInversed);
                        pooledObj.transform.SetParent(subModuleClass.subRagdoll
                            ? ((SkinnedMeshRenderer) subModuleClass.subModuleObjectClasses[0].renderer).rootBone
                            : subModuleClass.parent.transform);
                        ProcessSpawnedObject(pooledObj, i);
                    }
                    else if (particleClasses[i].setParent == Enums.ParticleSetParent.Both)
                    {
                        var pooledObj = GetSpawnObject(i, cutPosition, rotation);
                        pooledObj.transform.SetParent(subModuleClass.centerBone);  
                        ProcessSpawnedObject(pooledObj, i);
                        
                        var pooledObj2 = GetSpawnObject(i, cutPosition, rotationInversed);
                        pooledObj2.transform.SetParent(subModuleClass.subRagdoll
                            ? ((SkinnedMeshRenderer) subModuleClass.subModuleObjectClasses[0].renderer).rootBone
                            : subModuleClass.parent.transform);
                        ProcessSpawnedObject(pooledObj2, i);
                    }
                }
                else
                {
                    var pooledObj = GetSpawnObject(i, cutPosition, rotation);
                    ProcessSpawnedObject(pooledObj, i);
                }
            }
        }

        public override void ExecuteModuleExplosion(SubModuleClass subModuleClass)
        {
            if (!Application.isPlaying) return;
            if(subModuleClass.subModuleObjectClasses.Count == 0) return;
            
            for (var i = 0; i < particleClasses.Count; i++)
                if (particleClasses[i].positionExpl == Enums.ParticlePositionExpl.Center)
                {
                    var rotation = GetRotationExplosion(particleClasses[i], subModuleClass.subModuleObjectClasses[0]);
                    var pooledObj = GetSpawnObject(i, subModuleClass.centerPosition, rotation);
                    if (particleClasses[i].positionOffset != Vector3.zero)
                        pooledObj.transform.position += _goreSimulator.smr.transform.TransformDirection(particleClasses[i].positionOffset);
                    ProcessSpawnedObject(pooledObj, i);
                }
                else if (particleClasses[i].positionExpl == Enums.ParticlePositionExpl.Method)
                {
                    var rotation = GetRotationExplosion(particleClasses[i], subModuleClass.subModuleObjectClasses[0]);
                    var pooledObj = GetSpawnObject(i, subModuleClass.position, rotation);
                    ProcessSpawnedObject(pooledObj, i);
                }
                else
                {
                    int maxAmount = particleClasses[i].maxPosition;
                    int stepSize = Math.Max(1, subModuleClass.subModuleObjectClasses.Count / maxAmount);

                    for (int j = 0; j < maxAmount && j * stepSize < subModuleClass.subModuleObjectClasses.Count; j++)
                    {
                        var subModuleObjClass = subModuleClass.subModuleObjectClasses[j * stepSize];
                        var rotation = GetRotationExplosion(particleClasses[i], subModuleObjClass);

                        var pooledObj = GetSpawnObject(i, subModuleObjClass.centerPosition, rotation);
                        
                        if (particleClasses[i].setParentExplosionParts)
                        {
                            pooledObj.transform.SetParent(subModuleObjClass.obj.transform);
                        }
                        ProcessSpawnedObject(pooledObj, i);
                    }
                }
        }

        /********************************************************************************************************************************/
        
        private GameObject GetSpawnObject(int index, Vector3 position, Quaternion rotation)
        {
            GameObject pooledObj;
            if(_goreSimulator._globalSettings.poolEffectsActive && Application.isPlaying)
            {
                if (particleClasses[index].spawnType == Enums.SpawnType.ParticleSystem)
                    pooledObj = PGPool.Get(particleClasses[index].particle.gameObject);
                else
                    pooledObj = PGPool.Get(particleClasses[index].effect);
            }
            else
            {
                if (particleClasses[index].spawnType == Enums.SpawnType.ParticleSystem)
                {
                    pooledObj = Object.Instantiate(particleClasses[index].particle.gameObject);
                }
                else
                    pooledObj = Object.Instantiate(particleClasses[index].effect);
            }
            
            pooledObj.transform.SetPositionAndRotation(position, rotation);
            activeSystems.Add(pooledObj);

            return pooledObj;
        }
        private void ProcessSpawnedObject(GameObject obj, int particleClassesIndex)
        {
            var particleWrapperClass = particleClasses[particleClassesIndex];
            
            if (particleWrapperClass.spawnType == Enums.SpawnType.ParticleSystem)
            {
                if(particleWrapperClass.autoDespawn)
                {
                    if (!obj.TryGetComponent<PGPoolableParticles>(out var pgPoolableParticles))
                    {
                        pgPoolableParticles = obj.AddComponent<PGPoolableParticles>();
                        if (pgPoolableParticles.TryGetComponent<ParticleSystem>(out var particleSystem))
                            pgPoolableParticles._particleSystem = particleSystem;
                    }

                    pgPoolableParticles.poolActive = _goreSimulator._globalSettings.poolEffectsActive;
                    pgPoolableParticles._particleSystem.Play();
                }
                else
                {
                    if (obj.TryGetComponent<ParticleSystem>(out var particleSystem))
                        particleSystem.Play();
                }
            }
            else
            {
                if (obj.TryGetComponent<PGIExecutable>(out var pgiExecutable))
                {
                    pgiExecutable.Execute();
                }
                
                if (particleWrapperClass.autoDespawn)
                {
                    PGScheduler.ScheduleTime(obj.GetComponent<MonoBehaviour>(), particleWrapperClass.autoDespawnTimer, () =>
                    {
                        var settings = _goreSimulator._globalSettings;
                        if (activeSystems.Contains(obj)) activeSystems.Remove(obj);
                        Pool.Release(settings.poolEffectsActive, _goreSimulator.excludedComponentTypes, obj, false);
                    });
                }
            }
        }

        /********************************************************************************************************************************/
        
        public override void ExecuteModuleRagdoll(List<GoreBone> goreBones)
        {
        }

        public override void Reset()
        {
            base.Reset();
            ReleaseParticles();
            
            
        }

        internal void ReleaseParticles()
        {
            if (!_goreSimulator.gameObject.scene.isLoaded) return;
            
            var settings = _goreSimulator._globalSettings;
            
            for (var i = activeSystems.Count - 1; i >= 0; i--)
            {
                Pool.Release(settings.poolEffectsActive, _goreSimulator.excludedComponentTypes, activeSystems[i], true);
            }
            
            activeSystems.Clear();
        }

        /********************************************************************************************************************************/

        private Quaternion GetRotationCut(ParticleWrapperClass particleClass, SubModuleClass subModuleClass)
        {
            if (particleClass.rotationCut == Enums.ParticleRotationCut.CutDirection)
            {
                if (subModuleClass.cutDirection == Vector3.zero) return Quaternion.identity; 
                return Quaternion.LookRotation(subModuleClass.cutDirection);
            }
            if (particleClass.rotationCut == Enums.ParticleRotationCut.Method)
            {
                if (subModuleClass.force == Vector3.zero) return Quaternion.identity; 
                return Quaternion.LookRotation(subModuleClass.force);
            }           
            if (particleClass.rotationCut == Enums.ParticleRotationCut.Default) return Quaternion.identity;
            return MathUtility.GetRandomRotation();
        }
        
        private Quaternion GetRotationExplosion(ParticleWrapperClass particleClass, SubModuleObjectClass subModuleObjectClass)
        {
            if (particleClass.rotationExpl == Enums.ParticleRotationExpl.Method)
            {
                if (subModuleObjectClass.force == Vector3.zero) return Quaternion.identity; 
                return Quaternion.LookRotation(subModuleObjectClass.force);
            }
            if (particleClass.rotationExpl == Enums.ParticleRotationExpl.Default)
            {
                return Quaternion.identity;
            }
            if (particleClass.rotationExpl == Enums.ParticleRotationExpl.Forward)
            {
                return Quaternion.LookRotation(_goreSimulator.smr.transform.forward);
            }
            
            return MathUtility.GetRandomRotation();
        }


        /********************************************************************************************************************************/
        // Pool

        private static GameObject[] InitializePool(GameObject prefab, int preloadAmount)
        {
            var pool = PGPool.TryGetExistingPool(prefab) ?? new ObjectPool<GameObject>(
                () => CreateSetup(prefab),
                GetSetup,
                ReleaseSetup,
                DestroySetup);
            
            return PGPool.Preload(prefab, pool, preloadAmount);
        }

        private static GameObject CreateSetup(GameObject prefab)
        {
            var obj = Object.Instantiate(prefab);
            return obj;
        }

        private static void GetSetup(GameObject obj)
        {
#if UNITY_EDITOR
            if (obj == null)
            {
                DebugHandler.EmptyPooledObject();
            }
            if (SO_GlobalSettings.Instance.hidePooledObjects) obj.hideFlags = HideFlags.None;
#endif
            obj.SetActive(true);
        }

        private static void ReleaseSetup(GameObject obj)
        {
#if UNITY_EDITOR
            if (obj == null)
            {
                DebugHandler.EmptyPooledObject();
            }
            if (SO_GlobalSettings.Instance.hidePooledObjects) obj.hideFlags = HideFlags.HideInHierarchy;
#endif
            obj.transform.SetParent(null);
            obj.SetActive(false);
            Object.DontDestroyOnLoad(obj);
        }

        private static void DestroySetup(GameObject obj)
        {
            if(Application.isPlaying) Object.Destroy(obj);
        }
    }
}