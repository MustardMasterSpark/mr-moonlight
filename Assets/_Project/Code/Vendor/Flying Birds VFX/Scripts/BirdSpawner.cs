using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace FlyingBirds
{

    public class BirdSpawner : MonoBehaviour
    {



        public int birdNumberMax = 5;
        public float birdSpeedMin = 10;
        public float birdSpeedMax = 30;
        public float birdSizeMin = 0.3f;
        public float birdSizeMax = 1f;
        public float birdGlideAnimMin;
        public float birdGlideAnimMax;
        public float birdFlapAnimMin;
        public float birdFlapAnimMax;
        public float birdPathCirlceRandomOffset = 1f;
        public float birdRadiusMin = 2f;
        public float birdRadiusMax = 4f;
        public float birdVerticalOffsetStart = 1.5f;
        public float birdFlightHeightChangeMax = 1f;
        public float birdHeightChangeBounds = 1.5f;

        public GameObject ObjectToSpawn;
        [Header("Leave following at 0")]
        public int birdCount = 0;
        private GameObject clonebird;
        private float birdSizeRandomiser;
        public Vector3 rotationCentre;

        // ---------------------------------------------------------------------------------
        // MR. MOONLIGHT EDIT — MRM-11, 2026-09-02. Hierarchy hygiene, no behaviour change.
        //
        // The vendor spawned every bird straight into the scene root, so a flock dropped a
        // row of loose "Bird_Flap_Mesh(Clone)" objects in among the terrain, the player and
        // the event director. They now go into a container under the spawner instead.
        //
        // Behaviourally neutral, and it has to stay that way: the Instantiate overload used
        // below takes a world position and rotation, so each bird appears exactly where it
        // did before; the spawner has identity rotation and unit scale and never moves; and
        // BirdMovement drives transform.position and RotateAround, both world-space. If any
        // of those three stops being true, re-check this.
        //
        // To revert: delete _birdContainer and EnsureContainer(), and drop the last argument
        // from the Instantiate call in Update().
        // ---------------------------------------------------------------------------------
        private const string BirdContainerName = "Birds";

        private Transform _birdContainer;

        private void EnsureContainer()
        {
            if (_birdContainer != null) return;

            Transform existing = transform.Find(BirdContainerName);
            if (existing != null)
            {
                _birdContainer = existing;
                return;
            }

            var container = new GameObject(BirdContainerName);
            container.transform.SetParent(transform, false);
            _birdContainer = container.transform;
        }


        // Start is called before the first frame update
        void Start()
        {

            transform.localRotation = Quaternion.Euler(0, 0, 0);

            EnsureContainer();

            birdCount = 0;

            if (birdHeightChangeBounds < birdFlightHeightChangeMax)
            {
                birdHeightChangeBounds = birdFlightHeightChangeMax;
            }

            rotationCentre = transform.position;

            if (birdFlightHeightChangeMax < birdVerticalOffsetStart)
            {

                birdFlightHeightChangeMax = birdVerticalOffsetStart;
            }

        }

        // Update is called once per frame
        void Update()
        {

            Vector3 rndBirdPosition;

            rndBirdPosition = new Vector3(rotationCentre.x + Random.Range(birdRadiusMin, birdRadiusMax), rotationCentre.y + Random.Range(birdVerticalOffsetStart, -birdVerticalOffsetStart), rotationCentre.z);

            if (birdCount < birdNumberMax)
            {

                // MR. MOONLIGHT EDIT (MRM-11): parented — see EnsureContainer above.
                EnsureContainer();
                clonebird = Instantiate(ObjectToSpawn, rndBirdPosition, transform.rotation, _birdContainer) as GameObject;
                birdSizeRandomiser = Random.Range(birdSizeMin, birdSizeMax);
                clonebird.transform.localScale = new Vector3(birdSizeRandomiser, birdSizeRandomiser, birdSizeRandomiser);

                birdCount++;
                if (birdCount == birdNumberMax) { ObjectToSpawn.SetActive(false); }

            }

        }
    }

}
