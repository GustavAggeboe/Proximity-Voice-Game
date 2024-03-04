using Sandbox;
using Sandbox.Citizen;

public sealed class PlayerMovement : Component {
    [Property] private float groundControl = 4.0f;
    [Property] private float airControl = .1f;
    [Property] private float maxForce = 10f;
    [Property] private float speed = 125f;
    [Property] private float runSpeed = 215f;
    [Property] private float crouchSpeed = 90f;
    [Property] private float jumpForce = 200f;
    [Property] private float standHeight = 69.91f;
    [Property] private float crouchHeight = 43.5f;

    [Property] public GameObject head { get; private set; }
    [Property] private GameObject body;

    private Vector3 targetVelocity;
    public bool isCrouching { get; private set; }
    private bool isSprinting;
    private CharacterController characterController;
    private CitizenAnimationHelper animationHelper;

    [Property] private float velocity;

    protected override void OnAwake() {
        body = GameObject.Children.Single(x => x.Tags.Has("body"));
        head = GameObject.Children.Single(x => x.Tags.Has("head"));
        characterController = Components.Get<CharacterController>();
        animationHelper = Components.GetInChildrenOrSelf<CitizenAnimationHelper>();
    }

    protected override void OnUpdate() {
        if (IsProxy)
            return;

        velocity = characterController.Velocity.Length;
        HandleStates();
        UpdateAnimation();
        Rotate();
    }

    protected override void OnFixedUpdate() {
        if (IsProxy)
            return;

        UpdateTargetVelocity();
        Move();
    }

    private void UpdateTargetVelocity() {
        targetVelocity = Input.AnalogMove * head.Transform.Rotation;
        targetVelocity = targetVelocity.WithZ(0);
        targetVelocity = targetVelocity.IsNearZeroLength ? 0 : targetVelocity.Normal;
        targetVelocity *= isCrouching ? crouchSpeed : isSprinting ? runSpeed : speed;
    }

    private void Move() {
        Vector3 gravity = Scene.PhysicsWorld.Gravity;
        if (characterController.IsOnGround) {
            // Friction / acceleration
            characterController.Velocity = characterController.Velocity.WithZ(0);
            characterController.Accelerate(targetVelocity);
            characterController.ApplyFriction(groundControl);
        } else {
            // Air control / gravity
            characterController.Velocity += gravity * Time.Delta * 0.5f;
            characterController.Accelerate(targetVelocity.ClampLength(maxForce));
            characterController.ApplyFriction(airControl);
        }

        characterController.Move();

        if (!characterController.IsOnGround) {
            characterController.Velocity += gravity * Time.Delta * 0.5f;
        } else {
            characterController.Velocity = characterController.Velocity.WithZ(0);
        }
    }

    private void Rotate() {
        if (body == null)
            return;

        Angles targetAngle = new Angles(0, head.Transform.Rotation.Yaw(), 0).ToRotation();
        float rotationSpeed = characterController.Velocity.Length > 0.1f ? Time.Delta * 10f : Time.Delta * 5f;
        body.Transform.Rotation = Rotation.Lerp(body.Transform.Rotation, targetAngle, rotationSpeed);

        float rotateDifference = body.Transform.Rotation.Distance(targetAngle);
        if (rotateDifference > 1f) {
            animationHelper.FootShuffle = 10f;
        }

    }

    private void Jump() {
        if (!characterController.IsOnGround) { return; }

        characterController.Punch(Vector3.Up * jumpForce);
        animationHelper?.TriggerJump();
    }

    private void UpdateAnimation() {
        if (animationHelper == null) { return; }

        animationHelper.WithWishVelocity(targetVelocity);
        animationHelper.WithVelocity(characterController.Velocity);
        animationHelper.AimAngle = head.Transform.Rotation;
        animationHelper.IsGrounded = characterController.IsOnGround;
        animationHelper.WithLook(head.Transform.Rotation.Forward, 1f, 0.75f, 0.5f);
        animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Run;
        animationHelper.DuckLevel = isCrouching ? 1f : 0f;
    }

    private void HandleStates() {
        isCrouching = Input.Pressed("Crouch") ? !isCrouching : isCrouching;
        isSprinting = Input.Pressed("Run") ? !isSprinting : isSprinting;
        if (!Input.Down("Forward") || velocity <= 110f) {
            isSprinting = false;
        }

        if (Input.Pressed("Jump")) {
            Jump();
            isCrouching = false;
        }

        characterController.Height = isCrouching ? crouchHeight : standHeight;
    }
}
