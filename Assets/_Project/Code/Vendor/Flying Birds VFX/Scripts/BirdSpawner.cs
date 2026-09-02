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


        // Start is called before the first frame update
        void Start()
        {

            transform.localRotation = Quaternion.Euler(0, 0, 0);

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

                clonebird = Instantiate(ObjectToSpawn, rndBirdPosition, transform.rotation) as GameObject;
                birdSizeRandomiser = Random.Range(birdSizeMin, birdSizeMax);
                clonebird.transform.localScale = new Vector3(birdSizeRandomiser, birdSizeRandomiser, birdSizeRandomiser);

                birdCount++;
                if (birdCount == birdNumberMax) { ObjectToSpawn.SetActive(false); }

            }

        }
    }

}
