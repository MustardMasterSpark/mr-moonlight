namespace Burntwax
{
    public abstract class GunBaseState
    {

        private bool isRootState = false;
        private GunStateMachine ctx;
        private GunStateFactory factory;
        private GunBaseState currentSubState;
        private GunBaseState currentSuperState;

        protected bool IsRootState { set { isRootState = value; } }
        protected GunStateMachine Ctx { get { return ctx; } }
        protected GunStateFactory Factory { get { return factory; } }


        public GunBaseState(GunStateMachine currentContext, GunStateFactory gunStateFactory)
        {
            ctx = currentContext;
            factory = gunStateFactory;
        }


        public GunBaseState CurrentSubstate()
        {
            return currentSubState;
        }

        public GunBaseState CurrentSuperstate()
        {
            return currentSuperState;
        }
        public abstract void EnterState();

        public abstract void UpdateState();

        public abstract void ExitState();

        public abstract void CheckSwitchStates();

        public abstract void InitializeSubState();

        public void UpdateStates()
        {
            UpdateState();
            if (currentSubState != null)
            {
                currentSubState.UpdateStates();
            }
        }

        protected void SwitchState(GunBaseState newState)
        {
            // current state exists state
            ExitState();

            // new state enters state
            newState.EnterState();

            if (isRootState)
            {
                // switch current state of context
                ctx.CurrentState = newState;
            }
            else if (currentSuperState != null)
            {
                // set the current super state's substate to the new state
                currentSuperState.SetSubState(newState);
            }

        }

        protected void SetSuperState(GunBaseState newSuperState)
        {
            currentSuperState = newSuperState;
        }

        protected void SetSubState(GunBaseState newSubState)
        {
            currentSubState = newSubState;
            newSubState.SetSuperState(this);
        }

       
    }
}