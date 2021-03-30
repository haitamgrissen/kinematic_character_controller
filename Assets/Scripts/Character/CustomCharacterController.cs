using System;
using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;

public enum CharacterStates
{
    Default,
    Dashing,
}

public struct PlayerCharacterInputstruct
{
    public float MoveAxisForward;
    public float MoveAxisRight;
    public Quaternion CameraRotation;
    public bool JumpDown;
    public bool JumpUp;
    public bool DashingDown;
}

public class CustomCharacterController : MonoBehaviour, ICharacterController
{
    public KinematicCharacterMotor Motor;

    [Header("Stable Movement")] 
    public float MaxStableMoveSpeed = 10f;
    public float StableMovementSharpness = 15;
    public float OrientationSharpness = 10;

    [Header("Air Movement")] 
    public float MaxAirMoveSpeed = 10f;
    public float AirAccelerationSpeed = 5f;
    public float Drag = 0.1f;

    [Header("Jumping")] 
    public bool AllowJumpingWhenSliding = false;
    public bool AllowDoubleJump = false;
    public bool AllowWallJump = false;
    public float MaxJumpHeight = 4;
    public float MinJumpHeight = 1;
    public float TimeToJumpApex = .4f;
    public float JumpPreGroundingGraceTime = 0f;
    public float JumpPostGroundingGraceTime = 0f;

    [Header("Dashing")]
    public float ChargeSpeed = 15f;
    public float MaxChargeTime = 1.5f;
    public float StoppedTime = 1f;
    
    [Header("Misc")] 
    public bool RotationObstruction;
    public Vector3 Gravity = new Vector3(0, -30f, 0);
    public Transform MeshRoot;

    public CharacterStates CurrentCharacterState { get; private set; }

    private float _jumpVelocity;
    private float _minJumpVelocity;
    private Vector3 _moveInputVector;
    private Vector3 _lookInputVector;
    private bool _jumpRequested = false;
    private bool _jumpConsumed = false;
    private bool _jumpedThisFrame = false;
    private bool _jumpReleased = false;
    private float _timeSinceJumpRequested = Mathf.Infinity;
    private float _timeSinceLastAbleToJump = 0f;
    private bool _doubleJumpConsumed = false;
    private bool _canWallJump = false;
    private Vector3 _wallJumpNormal;
    
    private Vector3 _currentChargeVelocity;
    private bool _isStopped;
    private bool _mustStopVelocity = false;
    private float _timeSinceStartedCharge = 0;
    private float _timeSinceStopped = 0;

    private void Awake()
    {
        Gravity.y = -(2 * MaxJumpHeight) / (Mathf.Pow(TimeToJumpApex, 2));
        _jumpVelocity = Mathf.Abs(Gravity.y) * TimeToJumpApex;
        _minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(Gravity.y) * MinJumpHeight);

    }

    private void Start()
    {
        // Assign to motor
        Motor.CharacterController = this;
        TransitionToState(CharacterStates.Default);
    }
    
    

    //handling mouvement states transition and enter/exit callbacks
    public void TransitionToState(CharacterStates newState)
    {
        CharacterStates tmpInitialState = CurrentCharacterState;
        OnStateExit(tmpInitialState, newState);
        CurrentCharacterState = newState;
        OnStateEnter(newState, tmpInitialState);
    }

    //event when entering a state
    public void OnStateEnter(CharacterStates state, CharacterStates fromState)
    {
        switch (state)
        {
            case CharacterStates.Default:
            {
                break;
            }
            case CharacterStates.Dashing:
            {
                _currentChargeVelocity = Motor.CharacterForward * ChargeSpeed;
                _isStopped = false;
                _timeSinceStartedCharge = 0f;
                _timeSinceStopped = 0f;
                break;
            }
        }
    }

    //event when exiting a state
    public void OnStateExit(CharacterStates state, CharacterStates toState)
    {
        switch (state)
        {
            case CharacterStates.Default:
            {
                break;
            }
        }
    }



    /// This is called every frame by MyPlayer in order to tell the character what its inputs are
        public void SetInputs(ref PlayerCharacterInputstruct inputs)
        {
            if (inputs.DashingDown)
            {
                TransitionToState(CharacterStates.Dashing);
            }
            // Clamp input
            Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

            // Calculate camera direction and rotation on the character plane
            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
            if (cameraPlanarDirection.sqrMagnitude == 0f)
            {
                cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
            }
            Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);

            switch (CurrentCharacterState)
            {
                case CharacterStates.Default:
                {
                    // Move and look inputs
                    _moveInputVector = cameraPlanarRotation * moveInputVector;
                    _lookInputVector = cameraPlanarDirection;
            
                    //jumping input
                    if (inputs.JumpDown)
                    {
                        _timeSinceJumpRequested = 0;
                        _jumpRequested = true;
                    }
                    else if (inputs.JumpUp)
                    {
                        _jumpReleased = true;
                    }
                    break;
                }
            }
           
            
        }


        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is called before the character begins its movement update
        public void BeforeCharacterUpdate(float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterStates.Default:
                {
                    break;
                }
                case CharacterStates.Dashing:
                {
                    // Update times
                    _timeSinceStartedCharge += deltaTime;
                    if (_isStopped)
                    {
                        _timeSinceStopped += deltaTime;
                    }

                    break;
                }
            }
        }


        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its rotation should be right now. 
        /// This is the ONLY place where you should set the character's rotation
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterStates.Default:
                {
                    if (_lookInputVector != Vector3.zero && OrientationSharpness > 0f)
                    {
                        // Smoothly interpolate from current to target look direction
                        Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector,
                            1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

                        // Set the current rotation (which will be used by the KinematicCharacterMotor)
                        currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
                        //
                        //currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
                    }
                    break;
                }
            }
           
        }

        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its velocity should be right now. 
        /// This is the ONLY place where you can set the character's velocity
        public void UpdateVelocity(ref Vector3 currentVelocity,
            float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterStates.Default:
                {

                    currentVelocity = HandleMovement(currentVelocity, deltaTime);
                    /*
                    Vector3 targetMovementVelocity = Vector3.zero;
                    if (Motor.GroundingStatus.IsStableOnGround)
                    {
                        // Reorient source velocity on current ground slope (this is because we don't want our smoothing to cause any velocity losses in slope changes)
                        currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity,
                                              Motor.GroundingStatus.GroundNormal) *
                                          currentVelocity.magnitude;

                        // Calculate target velocity
                        Vector3 inputRight = Vector3.Cross(_moveInputVector,
                            Motor.CharacterUp);
                        Vector3 reorientedInput = Vector3.Cross(Motor.GroundingStatus.GroundNormal,
                                                          inputRight)
                                                      .normalized *
                                                  _moveInputVector.magnitude;
                        targetMovementVelocity = reorientedInput * MaxStableMoveSpeed;

                        // Smooth movement Velocity
                        currentVelocity = Vector3.Lerp(currentVelocity,
                            targetMovementVelocity,
                            1 - Mathf.Exp(-StableMovementSharpness * deltaTime));
                    }
                    else
                    {
                        // Add move input
                        if (_moveInputVector.sqrMagnitude > 0f)
                        {
                            targetMovementVelocity = _moveInputVector * MaxAirMoveSpeed;

                            // Prevent climbing on un-stable slopes with air movement
                            if (Motor.GroundingStatus.FoundAnyGround)
                            {
                                Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(Motor.CharacterUp,
                                            Motor.GroundingStatus.GroundNormal),
                                        Motor.CharacterUp)
                                    .normalized;
                                targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity,
                                    perpenticularObstructionNormal);
                            }

                            Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity,
                                Gravity);
                            currentVelocity += velocityDiff * AirAccelerationSpeed * deltaTime;
                        }

                        // Gravity
                        currentVelocity += Gravity * deltaTime;

                        // Drag
                        currentVelocity *= (1f / (1f + (Drag * deltaTime)));

                    }*/



                    //handle jump
                    currentVelocity = HandleJump(currentVelocity,
                        deltaTime);
                    /*
                    {
                        _jumpedThisFrame = false;
                        _timeSinceJumpRequested += deltaTime;
                        if (_jumpRequested)
                        {
                            //handle double jump
                            if (AllowDoubleJump)
                            {
                                if (_jumpConsumed && !_doubleJumpConsumed && (AllowJumpingWhenSliding
                                    ? !Motor.GroundingStatus.FoundAnyGround
                                    : !Motor.GroundingStatus.IsStableOnGround))
                                {
                                    Motor.ForceUnground(0.1f);
        
                                    //add jump velocity
                                    currentVelocity += (Motor.CharacterUp * _jumpVelocity) -
                                                       Vector3.Project(currentVelocity, Motor.CharacterUp);
                                    //reset jump states
                                    _doubleJumpConsumed = true;
                                    _jumpedThisFrame = true;
                                    _jumpRequested = false;
        
                                }
                            }
        
                            //see if we are allowed to jump
                            if (_canWallJump || !_jumpConsumed &&
                                ((AllowJumpingWhenSliding
                                     ? Motor.GroundingStatus.FoundAnyGround
                                     : Motor.GroundingStatus.IsStableOnGround)
                                 || _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime))
                            {
                                //calculate jump Direction before jumping
                                Vector3 jumpDirection = Motor.CharacterUp;
                                if (_canWallJump)
                                {
                                    jumpDirection = _wallJumpNormal;
                                }
                                else if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                                {
                                    jumpDirection = Motor.GroundingStatus.GroundNormal;
                                }
        
                                // Makes the character skip ground probing/snapping on its next update. 
                                Motor.ForceUnground(0.1f);
        
                                //handle jump
                                currentVelocity += (jumpDirection * _jumpVelocity) -
                                                   Vector3.Project(currentVelocity, Motor.CharacterUp);
        
                                //reset jump status
                                _jumpConsumed = true;
                                _jumpRequested = false;
                                _jumpedThisFrame = true;
                            }
                        }
        
                        //hansle jump release cariaton
                        if (_jumpReleased)
                        {
                            //avoid acceleraton during jump
                            if (currentVelocity.y > _minJumpVelocity)
                                currentVelocity.y = _minJumpVelocity;
                            _jumpReleased = false;
                        }
                        _canWallJump = false;
                    }*/
                    break;
                }
                case CharacterStates.Dashing:
                {
                    // If we have stopped and need to cancel velocity, do it here
                    if (_mustStopVelocity)
                    {
                        currentVelocity = Vector3.zero;
                        _mustStopVelocity = false;
                    }

                    if (_isStopped)
                    {
                        // When stopped, do no velocity handling except gravity
                        currentVelocity += Gravity * deltaTime;
                    }
                    else
                    {
                        // When charging, velocity is always constant
                        currentVelocity = _currentChargeVelocity;
                    }
                    break;
                }

            }

        }


        private Vector3 HandleMovement(Vector3 currentVelocity, float deltaTime)
        {
            Vector3 targetMovementVelocity = Vector3.zero;
                    if (Motor.GroundingStatus.IsStableOnGround)
                    {
                        // Reorient source velocity on current ground slope (this is because we don't want our smoothing to cause any velocity losses in slope changes)
                        currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity,
                                              Motor.GroundingStatus.GroundNormal) *
                                          currentVelocity.magnitude;

                        // Calculate target velocity
                        Vector3 inputRight = Vector3.Cross(_moveInputVector,
                            Motor.CharacterUp);
                        Vector3 reorientedInput = Vector3.Cross(Motor.GroundingStatus.GroundNormal,
                                                          inputRight)
                                                      .normalized *
                                                  _moveInputVector.magnitude;
                        targetMovementVelocity = reorientedInput * MaxStableMoveSpeed;

                        // Smooth movement Velocity
                        currentVelocity = Vector3.Lerp(currentVelocity,
                            targetMovementVelocity,
                            1 - Mathf.Exp(-StableMovementSharpness * deltaTime));
                    }
                    else
                    {
                        // Add move input
                        if (_moveInputVector.sqrMagnitude > 0f)
                        {
                            targetMovementVelocity = _moveInputVector * MaxAirMoveSpeed;

                            // Prevent climbing on un-stable slopes with air movement
                            if (Motor.GroundingStatus.FoundAnyGround)
                            {
                                Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(Motor.CharacterUp,
                                            Motor.GroundingStatus.GroundNormal),
                                        Motor.CharacterUp)
                                    .normalized;
                                targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity,
                                    perpenticularObstructionNormal);
                            }

                            Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity,
                                Gravity);
                            currentVelocity += velocityDiff * AirAccelerationSpeed * deltaTime;
                        }

                        // Gravity
                        currentVelocity += Gravity * deltaTime;

                        // Drag
                        currentVelocity *= (1f / (1f + (Drag * deltaTime)));
                        
                        
                    }
                    return currentVelocity;

        }

        private Vector3 HandleJump( Vector3 currentVelocity, float deltaTime)
        {
             //handle jump
            {
                _jumpedThisFrame = false;
                _timeSinceJumpRequested += deltaTime;
                if (_jumpRequested)
                {
                    //handle double jump
                    if (AllowDoubleJump)
                    {
                        if (_jumpConsumed && !_doubleJumpConsumed && (AllowJumpingWhenSliding
                            ? !Motor.GroundingStatus.FoundAnyGround
                            : !Motor.GroundingStatus.IsStableOnGround))
                        {
                            Motor.ForceUnground(0.1f);

                            //add jump velocity
                             currentVelocity += (Motor.CharacterUp * _jumpVelocity) -
                                               Vector3.Project(currentVelocity, Motor.CharacterUp);
                            //reset jump states
                            _doubleJumpConsumed = true;
                            _jumpedThisFrame = true;
                            _jumpRequested = false;

                        }
                    }

                    //see if we are allowed to jump
                    if (_canWallJump || !_jumpConsumed &&
                        ((AllowJumpingWhenSliding
                             ? Motor.GroundingStatus.FoundAnyGround
                             : Motor.GroundingStatus.IsStableOnGround)
                         || _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime))
                    {
                        //calculate jump Direction before jumping
                        Vector3 jumpDirection = Motor.CharacterUp;
                        if (_canWallJump)
                        {
                            jumpDirection = _wallJumpNormal;
                        }
                        else if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                        {
                            jumpDirection = Motor.GroundingStatus.GroundNormal;
                        }

                        // Makes the character skip ground probing/snapping on its next update. 
                        Motor.ForceUnground(0.1f);

                        //handle jump
                        currentVelocity += (jumpDirection * _jumpVelocity) -
                                           Vector3.Project(currentVelocity, Motor.CharacterUp);

                        //reset jump status
                        _jumpConsumed = true;
                        _jumpRequested = false;
                        _jumpedThisFrame = true;
                    }
                }

                //hansle jump release cariaton
                if (_jumpReleased)
                {
                    //avoid acceleraton during jump
                    if (currentVelocity.y > _minJumpVelocity)
                    {
                        currentVelocity.y = _minJumpVelocity;
                        
                    }
                    _jumpReleased = false;
                    
                }
                _canWallJump = false;
            }
            return currentVelocity;
        }

        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is called after the character has finished its movement update
        public void AfterCharacterUpdate(float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterStates.Default:
                {

                    // handle jump related values
                    {
                        //pre gorund grace
                        if (_jumpRequested && _timeSinceJumpRequested > JumpPreGroundingGraceTime)
                            _jumpRequested = false;

                        //handel jumping while sliding
                        if (AllowJumpingWhenSliding
                            ? Motor.GroundingStatus.FoundAnyGround
                            : Motor.GroundingStatus.IsStableOnGround)
                        {
                            //if we are on ground surface reset values
                            if (!_jumpedThisFrame)
                            {
                                _doubleJumpConsumed = false;
                                _jumpConsumed = false;
                            }


                            _timeSinceLastAbleToJump = 0f;
                        }
                        else
                        {
                            //keep track of time since we were able to jump (for grace period) 
                            _timeSinceLastAbleToJump += deltaTime;
                        }
                    }

                    break;
                }

                case CharacterStates.Dashing:
                {
                    // Detect being stopped by elapsed time
                    if (!_isStopped && _timeSinceStartedCharge > MaxChargeTime)
                    {
                        _mustStopVelocity = true;
                        _isStopped = true;
                    }

                    // Detect end of stopping phase and transition back to default movement state
                    if (_timeSinceStopped > StoppedTime)
                    {
                        TransitionToState(CharacterStates.Default);
                    }

                    break;
                }
            }
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {

            return true;
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            switch (CurrentCharacterState)
            {
                case CharacterStates.Default:
                {
                    if (AllowWallJump && !Motor.GroundingStatus.IsStableOnGround && !hitStabilityReport.IsStable)
                    {
                        _canWallJump = true;
                        _wallJumpNormal = hitNormal;
                    }
                    break;
                }
                case CharacterStates.Dashing:
                {
                    // Detect being stopped by obstructions
                    if (!_isStopped && !hitStabilityReport.IsStable && Vector3.Dot(-hitNormal, _currentChargeVelocity.normalized) > 0.5f)
                    {
                        _mustStopVelocity = true;
                        _isStopped = true;
                    }
                    break;
                }
            }
            
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            //comparing current and last ground check to see if we are leaving ground or landing on ground
            if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
            {
                //landed Debug.Log("landed");
            }
            else if(!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
            {
                //left ground Debug.Log("left gorund");
            }
        }

        public void AddVelocity(Vector3 velocity)
        {
        }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }
}