using Sandbox;
using Sandbox.Diagnostics;
using System;

public sealed class CameraController : Component {
    [Property] private PlayerMovement player;
    [Property] private GameObject head;
    [Property] private float distance = 150f;

    private bool isFirstPerson => distance == 0f;
    private Vector3 currentOffset = Vector3.Zero;
    private CameraComponent camera;
    private ModelRenderer bodyRenderer;

    protected override void OnAwake() {
        camera = Components.Get<CameraComponent>();
    }

    protected override void OnStart() {
        // Led efter spillere
        foreach (PlayerMovement p in Scene.GetAllComponents<PlayerMovement>()) {
            if (p.Network.IsOwner) { // er spilleren den lokale?
                player = p; break;
            }
        }
        if (player == null) {
            Log.Warning("No client found for camera!");
            return; // Hvis nej, return
        } else {
            head = player.head;
            bodyRenderer = player.Components.GetInChildren<SkinnedModelRenderer>();
            player.headRotation = head.Transform.Rotation;
        }
    }

    protected override void OnUpdate() {
        Angles eyeAngles = head.Transform.Rotation.Angles();
        eyeAngles.pitch += Input.MouseDelta.y * 0.1f;
        eyeAngles.yaw -= Input.MouseDelta.x * 0.1f;
        eyeAngles.roll = 0f;
        eyeAngles.pitch = eyeAngles.pitch.Clamp(-89.9f, 89.9f);
        head.Transform.Rotation = eyeAngles.ToRotation();
        player.headRotation = head.Transform.Rotation;

        Vector3 targetOffset = Vector3.Zero;
        if (player.isCrouching) {
            targetOffset += Vector3.Down * 26.5f;
        }
        currentOffset = Vector3.Lerp(currentOffset, targetOffset, Time.Delta * 10f);

        if (camera != null) {
            Vector3 position = head.Transform.Position + currentOffset;
            if (!isFirstPerson) {
                Vector3 forward = eyeAngles.ToRotation().Forward;
                SceneTraceResult trace = Scene.Trace.Ray(position, position - (forward * distance))
                    .WithoutTags("player", "trigger")
                    .Run();

                if (trace.Hit) {
                    position = trace.HitPosition + trace.Normal;
                } else {
                    position = trace.EndPosition;
                }

                bodyRenderer.RenderType = ModelRenderer.ShadowRenderType.On;
            } else {
                bodyRenderer.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
            }

            camera.Transform.Position = position;
            camera.Transform.Rotation = eyeAngles.ToRotation();
        }
    }
}
