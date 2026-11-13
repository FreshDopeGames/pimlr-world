// PIMLR #GREN: shared throw-animation helper for the water-balloon bombers (GrenadeThrower) and the player
// (PimlrPlayerGrenades). Both use the JUTPS AnimatorTPS Controller, which now has two throw states on the Base Layer:
//   "Throw"        -> Throw Object.fbx (big overhand lunge, speed 1.3)   ThrowVariant != 1
//   "Throw Goalie" -> Goalie Throw.fbx (quicker sidearm/overarm, 1.15)   ThrowVariant == 1
// Both states carry the tag "Throw", so code checks the tag, not the state name.
// Root motion is baked into the pose on both clips (import settings), so the character never translates; foot sliding
// from locomotion is stopped by PimlrThrowLock while a throw plays.
//
// Release timing and hand position were measured by sampling each clip on the MasculineAnimationAvatar rig (the
// moment the grip point's speed peaks): see ReleaseNormalized / ReleaseLocal. If either clip is re-imported with
// different root settings, re-measure (the measuring snippet is in the project doc claude/throw-animations-2026-11.md).
//
// Unreal note: two Anim Montages (same slot, full body) with a "Release" Anim Notify at these times; the grip is a
// hand socket ("balloon_grip") on hand_r.

using UnityEngine;

public enum PimlrThrowVariant { ThrowObject = 0, GoalieThrow = 1 }

public enum PimlrThrowVariantMode
{
    [Tooltip("Goalie Throw for short/medium throws, Throw Object (the big lunge) for long ones.")] ByDistance,
    Random,
    ThrowObjectOnly,
    GoalieThrowOnly
}

public static class PimlrThrowAnimation
{
    public const string Trigger = "Throw";
    public const string VariantParam = "ThrowVariant";
    public const string StateTag = "Throw";
    public const string GripName = "PIMLR_BalloonGrip";

    static readonly int TriggerHash = Animator.StringToHash(Trigger);
    static readonly int VariantHash = Animator.StringToHash(VariantParam);
    static readonly int TagHash = Animator.StringToHash(StateTag);

    /// <summary>Normalized clip time at which the balloon leaves the hand (grip speed peak).</summary>
    public static float ReleaseNormalized(PimlrThrowVariant v) => v == PimlrThrowVariant.GoalieThrow ? 0.17f : 0.275f;

    /// <summary>Grip position at release, in the thrower's root space (metres at scale 1). Used to predict the arc.</summary>
    public static Vector3 ReleaseLocal(PimlrThrowVariant v) =>
        v == PimlrThrowVariant.GoalieThrow ? new Vector3(0.56f, 1.71f, 0.56f) : new Vector3(0.51f, 1.88f, 1.15f);

    /// <summary>Distance share of max range above which ByDistance picks the long (Throw Object) animation.</summary>
    public const float LongThrowShare = 0.55f;

    public static PimlrThrowVariant Pick(PimlrThrowVariantMode mode, float distance, float maxRange)
    {
        switch (mode)
        {
            case PimlrThrowVariantMode.Random: return Random.value < 0.5f ? PimlrThrowVariant.ThrowObject : PimlrThrowVariant.GoalieThrow;
            case PimlrThrowVariantMode.ThrowObjectOnly: return PimlrThrowVariant.ThrowObject;
            case PimlrThrowVariantMode.GoalieThrowOnly: return PimlrThrowVariant.GoalieThrow;
            default: return distance >= maxRange * LongThrowShare ? PimlrThrowVariant.ThrowObject : PimlrThrowVariant.GoalieThrow;
        }
    }

    public static bool CanPlay(Animator a)
    {
        if (!a || !a.isActiveAndEnabled || !a.runtimeAnimatorController) return false;
        foreach (var p in a.parameters)
            if (p.nameHash == TriggerHash && p.type == AnimatorControllerParameterType.Trigger) return true;
        return false;
    }

    public static void Play(Animator a, PimlrThrowVariant v)
    {
        foreach (var p in a.parameters)
            if (p.nameHash == VariantHash) { a.SetInteger(VariantHash, (int)v); break; }
        a.ResetTrigger(TriggerHash);
        a.SetTrigger(TriggerHash);
    }

    public static void ClearTrigger(Animator a) { if (a) a.ResetTrigger(TriggerHash); }

    /// <summary>Progress (0-1) of the throw state the Base Layer is in or blending into. False when not throwing.</summary>
    public static bool TryGetProgress(Animator a, out float progress)
    {
        progress = 0f;
        if (!a) return false;
        if (a.IsInTransition(0))
        {
            var next = a.GetNextAnimatorStateInfo(0);
            if (next.tagHash == TagHash) { progress = next.normalizedTime; return true; }
            return false; // blending out of a throw counts as done
        }
        var cur = a.GetCurrentAnimatorStateInfo(0);
        if (cur.tagHash == TagHash) { progress = cur.normalizedTime; return true; }
        return false;
    }

    /// <summary>
    /// A transform in the right palm where a held balloon sits (created once under the RightHand bone): 60% of the
    /// way from the wrist to the middle knuckle, pushed out of the palm by roughly the balloon's radius.
    /// Falls back to <paramref name="fallback"/> on a non-humanoid rig.
    /// </summary>
    public static Transform Grip(Animator a, Transform fallback = null, float balloonRadius = 0.055f)
    {
        Transform hand = a && a.isHuman ? a.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (!hand) return fallback;
        var existing = hand.Find(GripName);
        if (existing) return existing;

        Transform mid = a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        Transform idx = a.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Transform lit = a.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        Vector3 local = Vector3.zero;
        if (mid && idx && lit)
        {
            Vector3 knuckle = hand.InverseTransformPoint(mid.position);
            Vector3 across = hand.InverseTransformPoint(idx.position) - hand.InverseTransformPoint(lit.position);
            Vector3 palm = -Vector3.Cross(across, knuckle).normalized; // palm-side normal (right hand)
            local = knuckle * 0.6f + palm * balloonRadius / Mathf.Max(0.0001f, hand.lossyScale.x);
        }
        var grip = new GameObject(GripName).transform;
        grip.SetParent(hand, false);
        grip.localPosition = local;
        grip.localRotation = Quaternion.identity;
        return grip;
    }

    /// <summary>Launch velocity that carries a body from <paramref name="from"/> to <paramref name="to"/> in time t under gravity g.</summary>
    public static Vector3 SolveVelocity(Vector3 from, Vector3 to, float t, Vector3 g) => (to - from) / t - 0.5f * g * t;

    public static float FlightTime(Vector3 from, Vector3 to, float horizontalSpeed, float minT, float maxT)
    {
        Vector3 d = to - from; d.y = 0f;
        return Mathf.Clamp(d.magnitude / Mathf.Max(1f, horizontalSpeed), minT, maxT);
    }
}
