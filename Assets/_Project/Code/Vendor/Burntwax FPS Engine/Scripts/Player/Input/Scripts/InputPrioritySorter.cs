using System.Collections.Generic;
using UnityEngine;

namespace Burntwax
{

    // Runs after InputManager, before the state machines that query it. (MRM-9)
    [DefaultExecutionOrder(-90)]
    public class InputPrioritySorter : MonoBehaviour
    {
        string sprint = "sprint";
        string aim = "aim";
        string shoot = "shoot";

        public static InputPrioritySorter Instance;


        void Awake()
        {

            Instance = this;
        }
        List<string> inputPriority = new List<string>();
        void Update()
        {
            if (InputManager.Instance.aimIsPressed && !inputPriority.Contains(aim))
            {
                // Debug.Log("Should add aim to list");
                inputPriority.Add(aim);
            }
            else if (!InputManager.Instance.aimIsPressed && inputPriority.Contains(aim))
            {
                // Debug.Log("Deleted aim from list");
                inputPriority.Remove(aim);
            }

            if (InputManager.Instance.sprintIsPressed && !inputPriority.Contains(sprint))
            {
                // Debug.Log("Should add sprint to list");
                inputPriority.Add(sprint);
            }
            else if (!InputManager.Instance.sprintIsPressed && inputPriority.Contains(sprint))
            {
                // Debug.Log("Deleted sprint from list");
                inputPriority.Remove(sprint);
            }
            if (InputManager.Instance.shootIsPressed && !inputPriority.Contains(shoot))
            {
                // Debug.Log("Should add shoot to list");
                inputPriority.Add(shoot);
            }
            else if (!InputManager.Instance.shootIsPressed && inputPriority.Contains(shoot))
            {
                // Debug.Log("Deleted shoot from list");
                inputPriority.Remove(shoot);
            }



        }

        public bool SprintIsPriority()
        {
            return inputPriority.IndexOf(sprint) == inputPriority.Count - 1 && inputPriority.Count >= 1;
        }

        public bool AimIsPriority()
        {
            return inputPriority.IndexOf(aim) == inputPriority.Count - 1 && inputPriority.Count >= 1;
        }

        public bool ShootIsPriority()
        {
            return inputPriority.IndexOf(shoot) == inputPriority.Count - 1 && inputPriority.Count >= 1;
        }

    }

}