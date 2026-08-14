using UnityEngine;

namespace AdvancedHorrorFPS
{
    public class LightScript : MonoBehaviour
    {
        public GameObject Light;
        public AudioSource AudioSource;
        private float lastInteractingTime = 0;
        public bool lightIsOnWhenStart = false;

        private void Start()
        {
            if(lightIsOnWhenStart)
            {
                Light.SetActive(!Light.activeSelf);
            }
        }

        public void Interact()
        {
            if(Time.time > lastInteractingTime + 0.25f)
            {
                lastInteractingTime = Time.time;
                Light.SetActive(!Light.activeSelf);
                if (Light.activeSelf) AudioSource.Play();
            }
        }
    }
}
