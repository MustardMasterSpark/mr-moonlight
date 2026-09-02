using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace FlyingBirds
{

    public class BirdMovement : MonoBehaviour
    {

        public BirdSpawner bs;

        private float speed = 20f;
        private float birdSizeRandomiser;
        private Vector3 rndOffsetRotationCentre;
        private Vector3 newPosition;
        private Animator animator;
        private float animOffset;
        private float animSpeed;
        private bool flapCheck = false;
        private bool heightCheck = false;
        private Animator birdAnim;
        private float birdHeight;

        void Start()
        {

            // Set random speed for the bird
           speed = Random.Range(bs.birdSpeedMin, bs.birdSpeedMax);

            // Offset birds flight path by moving it away from the rotation centre
            rndOffsetRotationCentre = new Vector3(bs.rotationCentre.x + Random.Range(-bs.birdPathCirlceRandomOffset, bs.birdPathCirlceRandomOffset), bs.rotationCentre.y + Random.Range(-bs.birdPathCirlceRandomOffset, bs.birdPathCirlceRandomOffset), bs.rotationCentre.z + Random.Range(-bs.birdPathCirlceRandomOffset, bs.birdPathCirlceRandomOffset));        

            // Random start postion around the path's circle
            transform.RotateAround(bs.rotationCentre, Vector3.up, Random.Range(0, 360f));

            birdAnim = GetComponent<Animator>();
            birdAnim.SetFloat("animOffset", Random.Range(0f, 1f));
            birdAnim.SetFloat("animSpeed", Random.Range(1f, 4f));
            birdAnim.SetBool("flap", false);

            birdHeight = rndOffsetRotationCentre.y;
            
        }


        void Update()
        {

            transform.RotateAround(rndOffsetRotationCentre, Vector3.up, speed * Time.deltaTime);

            if (flapCheck == false)
            {
               
                 StartCoroutine("StartFlapping");                        

            }

            if (heightCheck == false)
            {

                 StartCoroutine("ChangeHeight");

            }

        }


        IEnumerator StartFlapping()
        {

            flapCheck = true;

            yield return new WaitForSeconds(Random.Range(bs.birdGlideAnimMin, bs.birdGlideAnimMax));

            birdAnim.SetBool("flap", true);
            birdAnim.SetFloat("animSpeed", Random.Range(2f, 6f));

            yield return new WaitForSeconds(Random.Range(bs.birdFlapAnimMin, bs.birdFlapAnimMax));

            birdAnim.SetFloat("animOffset", Random.Range(0f, 1f));
            birdAnim.SetBool("flap", false);
            birdAnim.SetFloat("animSpeed", Random.Range(2f, 4f));

            flapCheck = false;

        }


        IEnumerator ChangeHeight()
        {

            heightCheck = true;

            yield return new WaitForSeconds(Random.Range(1f, 8f));

            // Time in seconds for the bird to fly to new height
            float time = Random.Range(2f, 4f);

            Vector3 startPosition;
            Vector3 targetPosition;
            startPosition = transform.position;
            targetPosition = transform.position;

            Vector3 temp;
            bool heightWithinBounds = false;
            float heightChange;

            heightChange = Random.Range(-bs.birdFlightHeightChangeMax, bs.birdFlightHeightChangeMax);

            if (Mathf.Abs(heightChange + birdHeight) < (rndOffsetRotationCentre.y + bs.birdHeightChangeBounds))
            {
                heightWithinBounds = true;
                birdHeight += heightChange;
                targetPosition.y = birdHeight;
            }    

            float elapsedTime = 0;

            while (elapsedTime < time)
            {
                temp = Vector3.Lerp(startPosition, targetPosition, (elapsedTime / time));
                transform.position = new Vector3(transform.position.x, temp.y, transform.position.z);
                elapsedTime += Time.deltaTime;     

                yield return null;
            }

            heightCheck = false;

        }

    }

}