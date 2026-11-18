using UnityEngine;
using JUTPS.CharacterBrain;

/// Lifts the visible body a little while prone so the mesh doesn't sink into the floor.
/// Visual only: moves the Hips bone after animation; colliders/gameplay are untouched.
/// JUTPS runs the Animator on the physics tick (AnimatorUpdateMode.Fixed), so on render
/// frames without a physics step the Animator does NOT rewrite the bones. We therefore
/// remember what we added last frame and remove it first if the pose wasn't refreshed,
/// otherwise the lift would stack up every frame (the "jumping" bug).
[DefaultExecutionOrder(10000)]
public class PimlrProneVisualOffset : MonoBehaviour
{
    public float proneLift = 0.06f;
    public float crouchLift = 0f;
    public float blendSpeed = 6f;

    JUCharacterBrain controller;
    Transform hips;
    float current;
    Vector3 lastSetLocalPos;     // hips.localPosition right after we applied the lift
    Vector3 lastAppliedLocal;    // the local-space delta we added
    bool applied;

    void Awake()
    {
        controller = GetComponent<JUCharacterBrain>();
        var anim = GetComponent<Animator>();
        if (anim != null && anim.isHuman) hips = anim.GetBoneTransform(HumanBodyBones.Hips);
    }

    void OnDisable() { RemoveIfStale(); applied = false; }

    void RemoveIfStale()
    {
        // Animator didn't overwrite the hips since our last write -> undo our previous offset.
        if (applied && hips != null && (hips.localPosition - lastSetLocalPos).sqrMagnitude < 1e-12f)
            hips.localPosition -= lastAppliedLocal;
        applied = false;
    }

    void LateUpdate()
    {
        if (controller == null || hips == null) return;
        RemoveIfStale();

        float target = 0f;
        if (!controller.IsRagdolled && !controller.IsDead)
        {
            if (controller.IsProne) target = proneLift;
            else if (controller.IsCrouched) target = crouchLift;
        }
        current = Mathf.MoveTowards(current, target, blendSpeed * Mathf.Max(proneLift, 0.01f) * Time.deltaTime);
        if (current <= 0.0001f) return;

        Vector3 worldDelta = transform.up * (current * transform.lossyScale.y);
        Vector3 localDelta = hips.parent != null ? hips.parent.InverseTransformVector(worldDelta) : worldDelta;
        hips.localPosition += localDelta;
        lastAppliedLocal = localDelta;
        lastSetLocalPos = hips.localPosition;
        applied = true;
    }
}
