namespace Burntwax
{
    public class GunAimState : GunBaseState
    {

        public GunAimState(GunStateMachine currentContext, GunStateFactory gunStateFactory)
        : base(currentContext, gunStateFactory)
        {
            IsRootState = true;

        }

        public override void EnterState()
        {
            Ctx.anim.SetBool(Ctx.aimHash, true);
            Ctx.fullyEquipped = true;
            InitializeSubState();
        }

        public override void UpdateState()
        {
            CheckSwitchStates();

        }

        public override void ExitState()
        {
            Ctx.anim.SetBool(Ctx.aimHash, false);
        }

        public override void CheckSwitchStates()
        {
            if (InputPrioritySorter.Instance.SprintIsPriority())
            {
                SwitchState(Factory.Stow());
            }
            else if (!InputManager.Instance.aimIsPressed && Ctx.CurrentState.CurrentSubstate() != Ctx.states.Shoot())
            {
                SwitchState(Factory.Rest());
            }
            else if (Ctx.AutoReload() || Ctx.ManualReload())
            {
                SwitchState(Factory.Reload());
            }
        }


        public override void InitializeSubState()
        {
            SetSubState(Factory.NoShoot());
        }

    }
}