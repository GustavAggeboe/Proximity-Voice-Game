using Sandbox;
using Sandbox.Citizen;
using System;

public sealed class PlayerMovement : Component {
    [Property] private readonly float groundControl = 4.0f;
    [Property] private readonly float airControl = .1f;
    [Property] private readonly float maxForce = 10f;
    [Property] private readonly float speed = 125f;
    [Property] private readonly float runSpeed = 215f;
    [Property] private readonly float crouchSpeed = 90f;
    [Property] private readonly float jumpForce = 200f;
    [Property] private readonly float standHeight = 69.91f;
    [Property] private readonly float crouchHeight = 43.5f;

    public GameObject head { get; private set; }
    private GameObject body;

    private Vector3 targetVelocity;
    private CharacterController characterController;
    private CitizenAnimationHelper animationHelper;

    [Sync] public Rotation headRotation { get; set; }
    [Sync] public bool isCrouching { get; private set; }
    [Sync] public bool isSprinting { get; private set; }
    [Sync] public string clothingJson { get; private set; }

    protected override void OnAwake() {
        body = GameObject.Children.Find(x => x.Tags.Has("body"));
        head = GameObject.Children.Find(x => x.Tags.Has("head"));
        characterController = Components.Get<CharacterController>();
        animationHelper = Components.GetInChildrenOrSelf<CitizenAnimationHelper>();
    }

    protected override void OnStart() {
        if (!Network.Active) {
            Log.Warning($"No owner of playermovement.");
        }
        InitializeClothing();
    }

    protected override void OnUpdate() {
        if (!IsProxy) {
            HandleStates();
        }
        UpdateAnimation();
        UpdateRotation();
    }

    protected override void OnFixedUpdate() {
        if (IsProxy)
            return;
        UpdateTargetVelocity();
        Move();
    }

    #region Owner
    private void UpdateTargetVelocity() {
        targetVelocity = Input.AnalogMove * headRotation;
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

    private void HandleStates() {
        isCrouching = Input.Pressed("Crouch") ? !isCrouching : isCrouching;
        isSprinting = Input.Pressed("Run") ? !isSprinting : isSprinting;
        if (!Input.Down("Forward") || characterController.Velocity.Length <= 110f) {
            isSprinting = false;
        }
        if (Input.Pressed("Jump")) {
            Jump();
            isCrouching = false;
        }
    }

    private void Jump() {
        if (!characterController.IsOnGround) { return; }
        characterController.Punch(Vector3.Up * jumpForce);
        animationHelper?.TriggerJump();
    }
    #endregion

    private void UpdateRotation() {
        if (body == null)
            return;
        Angles targetAngle = new Angles(0, headRotation.Yaw(), 0).ToRotation();
        float rotationSpeed = characterController.Velocity.Length > 0.1f ? Time.Delta * 10f : Time.Delta * 5f;
        body.Transform.Rotation = Rotation.Lerp(body.Transform.Rotation, targetAngle, rotationSpeed);
        float rotateDifference = body.Transform.Rotation.Distance(targetAngle);
        if (rotateDifference > 1f) {
            animationHelper.FootShuffle = 10f;
        }
    }

    private void UpdateAnimation() {
        if (animationHelper == null) { return; }

        animationHelper.WithWishVelocity(targetVelocity);
        animationHelper.WithVelocity(characterController.Velocity);
        animationHelper.AimAngle = headRotation;
        animationHelper.IsGrounded = characterController.IsOnGround;
        animationHelper.WithLook(headRotation.Forward, 1f, 0.75f, 0.5f);
        animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Run;
        animationHelper.DuckLevel = isCrouching ? 1f : 0f;

        characterController.Height = isCrouching ? crouchHeight : standHeight;
    }

    private void InitializeClothing() {
        if (!IsProxy) {
            clothingJson = ClothingContainer.CreateFromLocalUser().Serialize();
        }
        if (clothingJson != null) {
            if (Components.TryGet<SkinnedModelRenderer>(out var model, FindMode.EverythingInSelfAndChildren)) {
                ClothingContainer clothing = ClothingContainer.CreateFromJson(clothingJson);
                clothing.Apply(model);
            }
            Log.Info($"Applied clothing to {Connection.Local.Name}");
        }
    }
}
