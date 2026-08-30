namespace Burntwax
{
    public abstract class PlayerBaseState
    {

        private bool isRootState = false;
        private PlayerStateMachine ctx;
        private PlayerStateFactory factory;
        private PlayerBaseState currentSubState;
        private PlayerBaseState currentSuperState;

        protected bool IsRootState { set { isRootState = value; } }
        protected PlayerStateMachine Ctx { get { return ctx; } }
        protected PlayerStateFactory Factory { get { return factory; } }


        public PlayerBaseState(PlayerStateMachine currentContext, PlayerStateFactory playerStateFactory)
        {
            ctx = currentContext;
            factory = playerStateFactory;
        }


        public PlayerBaseState CurrentSubstate()
        {
            return currentSubState;
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

        protected void SwitchState(PlayerBaseState newState)
        {

            PlayerBaseState currentSubState = ctx.CurrentState.currentSubState;
            // current state exists state
            ExitState();

            newState.EnterState();

            if (isRootState)
            {
                // switch current state of context
                ctx.CurrentState = newState;
                ctx.CurrentState.SetSubState(currentSubState);
            }
            else if (currentSuperState != null)
            {
                // set the current super state's substate to the new state
                currentSuperState.SetSubState(newState);
            }

        }

        protected void SetSuperState(PlayerBaseState newSuperState)
        {
            currentSuperState = newSuperState;
        }

        protected void SetSubState(PlayerBaseState newSubState)
        {
            currentSubState = newSubState;
            newSubState.SetSuperState(this);
        }
    }

}