using UnityEngine;
using UnityEngine.Events;

namespace Burntwax
{
    public class Interactable : MonoBehaviour
    {
        public bool continuous;
        public UnityEvent onInteract;
        //public Sprite interactIcon;
        public int ID;
        // Start is called before the first frame update
        void Start()
        {
            ID = Random.Range(0, 999999);
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}