namespace Burntwax
{
    public class GunRestState : GunBaseState
    {

        public GunRestState(GunStateMachine currentContext, GunStateFactory gunStateFactory)
        : base(currentContext, gunStateFactory)
        {
            IsRootState = true;

        }

        public override void EnterState()
        {
            Ctx.isReloading = false;
            Ctx.isIncrementalReloading = false;
            Ctx.isShooting = false;

            InitializeSubState();
        }

        public override void UpdateState()
        {
            CheckSwitchStates();

        }

        public override void ExitState()
        {
        }

        public override void CheckSwitchStates()
        {
            if (InputPrioritySorter.Instance.AimIsPriority() && Ctx.CurrentState.CurrentSubstate() != Ctx.states.Shoot())
            {
                SwitchState(Factory.Aim());
            }
            else if (InputPrioritySorter.Instance.SprintIsPriority() && Ctx.stateMachine.currentState.CurrentSubstate() != Ctx.stateMachine.states.Idle() && Ctx.CurrentState.CurrentSubstate() != Ctx.states.Shoot())
            {
                SwitchState(Factory.Stow());
            }
            else if ((Ctx.AutoReload() || Ctx.ManualReload()) && Ctx.fullyEquipped)
            {
                Ctx.fullyEquipped = false;
                Ctx.anim.SetBool(Ctx.proceduralShootHash, false);
                SwitchState(Factory.Reload());
            }

        }


        public override void InitializeSubState()
        {
            SetSubState(Factory.NoShoot());


        }

    }
}