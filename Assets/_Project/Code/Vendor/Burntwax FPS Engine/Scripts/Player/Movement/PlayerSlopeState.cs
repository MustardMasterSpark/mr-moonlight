using System;
using UnityEngine;

namespace Burntwax
{
    public class PlayerSlopeState : PlayerBaseState
    {

        public PlayerSlopeState(PlayerStateMachine currentContext, PlayerStateFactory playerStateFactory)
        : base(currentContext, playerStateFactory)
        {
            IsRootState = true;

        }

        public override void EnterState()
        {
            // Debug.Log("SLOPE STATE");
            Ctx.MoveMultiplier = Ctx.GroundMultiplier;
            Ctx.rb.linearDamping = Ctx.GroundDrag;

            Ctx.YFlat();
            InitializeSubState();
            if (Ctx.currentState.CurrentSubstate() == Ctx.states.Crouch())
            {
                Ctx.RideHeight = Ctx.CrouchSlopeHeight;
                return;
            }
            Ctx.RideHeight = Ctx.SlopeHeight;



        }

        public override void UpdateState()
        {
            CheckSwitchStates();
        }

        public override void ExitState()
        {
            Ctx.currentAngle = 0;
            if (Ctx.currentState.CurrentSubstate() == Ctx.states.Crouch())
            {
                Ctx.RideHeight = Ctx.CrouchHeight;
                return;
            }
            Ctx.RideHeight = Ctx.NormalHeight;
        }

        public override void CheckSwitchStates()
        {
            if (InputManager.Instance.jumpIsPressed)
            {
                SwitchState(Factory.Jump());
            }
            else if (!Ctx.playerIsSloped && !Ctx.playerIsGrounded && !InputManager.Instance.crouchIsPressed)
            {
                SwitchState(Factory.Fall());
            }
            else if (!InputManager.Instance.jumpIsPressed && !Ctx.playerIsSloped && Math.Abs(Ctx.currentAngle) < 0.03f && Ctx.groundCheckHit.collider.gameObject.layer != LayerMask.NameToLayer("Variable Slope"))
            {
                SwitchState(Factory.Grounded());
            }
        }


        public override void InitializeSubState()
        {
            if (!InputManager.Instance.moveIsPressed && !InputManager.Instance.sprintIsPressed)
            {

                SetSubState(Factory.Idle());
            }
            else if (InputManager.Instance.moveIsPressed && !InputManager.Instance.sprintIsPressed)
            {

                SetSubState(Factory.Walk());
            }
            else if (InputManager.Instance.crouchIsPressed)
            {
                SwitchState(Factory.Crouch());
            }
            else
            {
                SetSubState(Factory.Sprint());
            }

        }
    }

}