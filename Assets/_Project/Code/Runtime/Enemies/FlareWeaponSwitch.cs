using UnityEngine;

public class FlareWeaponSwitch : StateMachineBehaviour
{
    GameObject weapon;
    GameObject flare;

    void Cache(Animator animator)
    {
        if (weapon != null || flare != null) return;
        foreach (var t in animator.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "DBShotgun") weapon = t.gameObject;
            else if (t.name == "FlareGun") flare = t.gameObject;
        }
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        Cache(animator);
        if (weapon != null) weapon.SetActive(false);
        if (flare != null) flare.SetActive(true);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        Cache(animator);
        if (weapon != null) weapon.SetActive(true);
        if (flare != null) flare.SetActive(false);
    }
}
