using UnityEngine;
using UnityEngine.SceneManagement;

namespace Burntwax
{
    [RequireComponent(typeof(Collider))]
    public class SceneTeleporter : MonoBehaviour
    {
        public int sceneIndexToLoad;


        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerHealth>())
            {
                SceneManager.LoadSceneAsync(sceneIndexToLoad);
            }
        }
    }
}