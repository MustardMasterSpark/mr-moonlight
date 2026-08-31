using System;
using System.Collections;
using System.Collections.Generic;
using Burntwax;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace Burntwax
{

    [CreateAssetMenu(fileName = "Gun", menuName = "Guns/Gun", order = 0)]
    public class GunScriptableObject : ScriptableObject
    {

        public int ID;

        [Header("General")]
        public GunType Type;

        [Header("Incremental Reloads (for shotgun types)")]
        public bool incrementalReload;

        [Header("Procedural Recoil (for SMG types)")]
        public bool proceduralRecoil;

        public Vector3 restingRecoilPos;

        [HideInInspector] public GameObject Model;


        [Header("Configs")]
        public ShootConfig ShootConfig;
        public AmmoConfig AmmoConfig;
        public AudioConfig AudioConfig;
        private AudioSource ShootingAudioSource;
        public DamageConfig DamageConfig;
        public TrailConfig TrailConfig;
        public CrosshairConfig CrosshairConfig;


        private MonoBehaviour ActiveMonoBehaviour;

        private ParticleSystem ShootSystem;
        private ObjectPool<TrailRenderer> TrailPool;



        public void Spawn(GameObject player, GameObject currentGun, MonoBehaviour ActiveMonoBehaviour)
        {
            this.ActiveMonoBehaviour = ActiveMonoBehaviour;
            if (TrailPool != null)
            {
                TrailPool.Dispose();
            }
            TrailPool = new ObjectPool<TrailRenderer>(CreateTrail);
            ShootSystem = currentGun.GetComponentInChildren<ParticleSystem>();
            ShootingAudioSource = player.GetComponent<AudioSource>();
            BulletImpactEffects.Instance = FindFirstObjectByType<BulletImpactEffects>();
        }

        public void Despawn(MonoBehaviour ActiveMonoBehaviour)
        {
            this.ActiveMonoBehaviour = ActiveMonoBehaviour;
            TrailPool.Dispose();
        }

        public void Shoot()
        {
            RaycastHit hit;
            List<string> surfaces = new List<string>();
            ShootSystem.Play();
            AudioConfig.PlayShootingClip(ShootingAudioSource, AmmoConfig.currentClip == 1);
            AmmoConfig.currentClip--;
            for (int i = 0; i < ShootConfig.bulletsPerShot; i++)
            {
                Vector3 shootDirection = Camera.main.transform.forward + new Vector3(Random.Range(-ShootConfig.Spread.x, ShootConfig.Spread.x), Random.Range(-ShootConfig.Spread.y, ShootConfig.Spread.y), Random.Range(-ShootConfig.Spread.z, ShootConfig.Spread.z));
                shootDirection.Normalize();



                if (Physics.Raycast(Camera.main.transform.position, shootDirection, out hit, float.MaxValue, ShootConfig.HitMask))
                {
                    ActiveMonoBehaviour.StartCoroutine(PlayTrail(surfaces, i, ShootSystem.transform.position, hit.point, hit));
                }
                else
                {
                    ActiveMonoBehaviour.StartCoroutine(PlayTrail(surfaces, i, ShootSystem.transform.position, ShootSystem.transform.position + (shootDirection * TrailConfig.MissDistance), hit));
                }
            }

        }

        public bool CanReload()
        {
            return AmmoConfig.CanReload();
        }

        public void StartReloadAudio()
        {
            AudioConfig.PlayReloadClip(ShootingAudioSource);
        }

        public void EndReload()
        {
            AmmoConfig.Reload();
        }

        public void EndIncrementalReload()
        {
            AmmoConfig.IncrementalReload();
        }

        private IEnumerator PlayTrail(List<string> surfaces, int i, Vector3 StartPoint, Vector3 EndPoint, RaycastHit Hit)
        {
            TrailRenderer instance = TrailPool.Get();
            instance.gameObject.SetActive(true);
            instance.transform.position = StartPoint;
            yield return null; // avoid position carry-over from last frame if reused

            instance.emitting = true;

            float distance = Vector3.Distance(StartPoint, EndPoint);
            float remainingDistance = distance;
            while (remainingDistance > 0)
            {
                instance.transform.position = Vector3.Lerp(
                    StartPoint,
                    EndPoint,
                    Mathf.Clamp01(1 - (remainingDistance / distance))
                );
                remainingDistance -= ShootConfig.bulletSpeed * Time.deltaTime;

                yield return null;
            }

            instance.transform.position = EndPoint;

            if (Hit.collider != null)
            {

                if (Hit.collider.GetComponent<Rigidbody>())
                {
                    Hit.collider.GetComponent<Rigidbody>().AddForce((EndPoint - StartPoint).normalized * DamageConfig.GetDamage(distance), ForceMode.Impulse);
                }
                if (Hit.collider.TryGetComponent(out Destructible destructable))
                {
                    destructable.TakeDamage(DamageConfig.GetDamage(distance) / 2);
                }
                if (Hit.collider.TryGetComponent(out Target target))
                {
                    target.Teleport();
                }

                string surfaceTag = Hit.collider.tag;
                Vector3 impactPosition = Hit.point + Hit.normal * 0.01f;
                Quaternion impactRotation = Quaternion.LookRotation(Hit.normal);
                BulletImpactEffects.Instance.ImpactVFX(surfaceTag, impactPosition, impactRotation);
                if (!surfaces.Contains(surfaceTag))
                {
                    surfaces.Add(surfaceTag);
                    BulletImpactEffects.Instance.ImpactAudio(surfaceTag, impactPosition, impactRotation);
                }



            }



            yield return new WaitForSeconds(TrailConfig.Duration);
            yield return null;
            instance.emitting = false;
            instance.gameObject.SetActive(false);
            TrailPool.Release(instance);
        }

        private TrailRenderer CreateTrail()
        {
            GameObject instance = new GameObject("Bullet Trail");
            TrailRenderer trail = instance.AddComponent<TrailRenderer>();
            trail.colorGradient = TrailConfig.Color;
            trail.material = TrailConfig.Material;
            trail.widthCurve = TrailConfig.WidthCurve;
            trail.time = TrailConfig.Duration;
            trail.minVertexDistance = TrailConfig.MinVertexDistance;
            trail.emitting = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return trail;
        }
    }
}