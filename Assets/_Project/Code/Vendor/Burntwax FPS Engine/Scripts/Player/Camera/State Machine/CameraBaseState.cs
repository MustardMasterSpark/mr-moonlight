namespace Burntwax
{
    public abstract class CameraBaseState
    {

        private bool isRootState = false;
        private CamStateMachine ctx;
        private CameraStateFactory factory;
        private CameraBaseState currentSubState;
        private CameraBaseState currentSuperState;

        protected bool IsRootState { set { isRootState = value; } }
        protected CamStateMachine Ctx { get { return ctx; } }
        protected CameraStateFactory Factory { get { return factory; } }


        public CameraBaseState(CamStateMachine currentContext, CameraStateFactory cameraStateFactory)
        {
            ctx = currentContext;
            factory = cameraStateFactory;
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

        protected void SwitchState(CameraBaseState newState)
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

        protected void SetSuperState(CameraBaseState newSuperState)
        {
            currentSuperState = newSuperState;
        }

        protected void SetSubState(CameraBaseState newSubState)
        {
            currentSubState = newSubState;
            newSubState.SetSuperState(this);
        }
    }

}