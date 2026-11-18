// PIMLR (Nov 18): the player looks like the artist picked on Select Your Artist.
//
// How: the scene's Player (Thurz's rig + every JUTPS / PIMLR component) is left exactly as it is - its body meshes are
// just hidden. The chosen artist's model (the roster entry's Display Prefab, scripts stripped) is placed on the Player
// and, every frame after all animation, IK and procedural motion, copies Thurz's pose through Unity's humanoid
// retargeting (HumanPoseHandler). So movement, aiming, the water gun and balloons in hand, rolling, ragdoll, hit boxes,
// footsteps and every animation keep working unchanged, for any Humanoid artist model.
//
//   - Applied on every scene load (Living Room, YannicksWorld) and the moment a new artist is picked.
//   - Artists with no Display Prefab (Thurz today) = the scene's own player model, nothing swapped.
//   - Items held in the hands (water gun, balloons) stay on Thurz's hidden hand bones. For artists close to his build that
//     lines up; if an artist's hands sit visibly off the gun, give them a different body proportion fix later.
//   - Fit Check colours only apply to Thurz's own model.
//
// Requirements for an artist model: Humanoid rig (Avatar set to Humanoid), authored at the same 1x scale as Thurz.
// Per-artist size: the roster's Display Scale.
//
// Unreal note: the same trick is SetLeaderPoseComponent / a Copy Pose From Mesh (or IK Retargeter "Retarget Pose From
// Mesh") AnimBP node on the artist's SkeletalMeshComponent, following the hidden gameplay mesh. Or swap the
// SkeletalMesh outright when all artists share one skeleton.

using JUTPS;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(32000)] // after JUTPS, IK, head look and every PIMLR LateUpdate that poses the rig
[DisallowMultipleComponent]
public class PimlrArtistAvatar : MonoBehaviour
{
    public const string VisualName = "PIMLR Artist Avatar";

    Animator sourceAnim;
    GameObject visual;
    Animator visualAnim;
    HumanPoseHandler fromPose, toPose;
    HumanPose pose;
    string wornId;
    AnimatorCullingMode cullingBefore;
    readonly System.Collections.Generic.List<Renderer> hidden = new System.Collections.Generic.List<Renderer>();

    /// <summary>Id of the artist the player is wearing ("" = the scene's own model).</summary>
    public string WornId => wornId ?? "";

    // ------------------------------------------------------------------ install
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        PimlrArtists.SelectionChanged -= OnSelectionChanged;
        PimlrArtists.SelectionChanged += OnSelectionChanged;
        ApplyToPlayer();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => ApplyToPlayer();
    static void OnSelectionChanged(string id) => ApplyToPlayer();

    /// <summary>Dress the scene's player as the selected artist (or back to the default model).</summary>
    public static void ApplyToPlayer()
    {
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (!go.GetComponent<JUCharacterController>() || !go.GetComponent<Animator>()) continue;
            var a = go.GetComponent<PimlrArtistAvatar>();
            var artist = PimlrArtists.Selected;
            if (!a)
            {
                if (artist == null || !artist.displayPrefab) return; // default model: nothing to add
                a = go.AddComponent<PimlrArtistAvatar>();
            }
            a.Wear(artist);
            return;
        }
    }

    // ------------------------------------------------------------------ wear / clear
    void Wear(PimlrArtistRoster.Artist artist)
    {
        if (artist == null || !artist.displayPrefab) { Clear(); return; }
        if (wornId == artist.id && visual) return;
        Clear();

        sourceAnim = GetComponent<Animator>();
        if (!sourceAnim || !sourceAnim.avatar || !sourceAnim.avatar.isHuman)
        {
            Debug.LogWarning("PIMLR Artist: the player's Animator has no Humanoid avatar - can't wear another artist.", this);
            return;
        }

        // Copy under an inactive holder (no scripts wake up), keep only what draws.
        var holder = new GameObject("holder"); holder.SetActive(false);
        var copy = Instantiate(artist.displayPrefab, holder.transform);
        PimlrArtistSelect.StripToVisuals(copy);
        copy.name = VisualName;
        copy.transform.SetParent(transform, false);
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        // A child of the Player inherits the scene's scale (2x in the Living Room), so only the prefab's own scale here.
        copy.transform.localScale = artist.displayPrefab.transform.localScale * (artist.displayScale > 0f ? artist.displayScale : 1f);
        Destroy(holder);

        visualAnim = copy.GetComponentInChildren<Animator>(true);
        if (!visualAnim || !visualAnim.avatar || !visualAnim.avatar.isHuman)
        {
            Debug.LogWarning($"PIMLR Artist: '{artist.displayName}' needs a Humanoid Avatar on its Animator to be playable - keeping the default model.", this);
            Destroy(copy);
            return;
        }
        visualAnim.enabled = false; // posed by us, not by its own controller
        foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        copy.SetActive(true);
        visual = copy;

        fromPose = new HumanPoseHandler(sourceAnim.avatar, sourceAnim.transform);
        toPose = new HumanPoseHandler(visualAnim.avatar, visualAnim.transform);
        srcHips = sourceAnim.GetBoneTransform(HumanBodyBones.Hips);
        dstHips = visualAnim.GetBoneTransform(HumanBodyBones.Hips);
        float ls = LegLength(sourceAnim), lv = LegLength(visualAnim); // world lengths (scales included)
        legRatio = ls > 0.01f && lv > 0.01f ? Mathf.Clamp(lv / ls, 0.6f, 1.5f) : 1f;

        // Thurz's own body stays animated (it drives everything) but unseen.
        cullingBefore = sourceAnim.cullingMode;
        sourceAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        HideBaseBody();

        wornId = artist.id;
        Debug.Log($"PIMLR Artist: player is now {artist.displayName}.");
    }

    void Clear()
    {
        if (visual) Destroy(visual);
        visual = null; visualAnim = null;
        fromPose?.Dispose(); toPose?.Dispose();
        fromPose = toPose = null;
        foreach (var r in hidden) if (r) r.enabled = true;
        hidden.Clear();
        if (sourceAnim && wornId != null) sourceAnim.cullingMode = cullingBefore;
        wornId = null;
    }

    void OnDestroy() => Clear();

    // The body = skinned meshes on the Player that aren't held items or the artist model.
    void HideBaseBody()
    {
        hidden.Clear();
        foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (visual && r.transform.IsChildOf(visual.transform)) continue;
            if (r.GetComponentInParent<JUTPS.ItemSystem.Item>(true)) continue; // a skinned weapon / item: keep
            hidden.Add(r);
        }
        EnforceHidden();
    }

    void EnforceHidden()
    {
        // Other systems (Fit Check, the carousel, damage flashes) may switch renderers back on.
        foreach (var r in hidden) if (r && r.enabled) r.enabled = false;
    }

    // ------------------------------------------------------------------ pose copy
    void LateUpdate()
    {
        if (!visual || fromPose == null || toPose == null) return;
        EnforceHidden();

        // Limbs: humanoid retarget (muscle space), so any Humanoid rig takes Thurz's pose.
        var vt = visual.transform;
        vt.localPosition = Vector3.zero;
        vt.localRotation = Quaternion.identity;
        fromPose.GetHumanPose(ref pose);
        toPose.SetHumanPose(ref pose);

        // Body placement: the pose's body position/rotation come back in a different space than the model's, so anchor
        // the artist's pelvis to Thurz's instead - same facing, same spot, hip height scaled by leg length.
        if (!srcHips) return;
        Quaternion want = BodyFrame(sourceAnim), have = BodyFrame(visualAnim);
        vt.rotation = want * Quaternion.Inverse(have) * vt.rotation;
        Vector3 root = transform.position;
        Vector3 target = srcHips.position;
        target.y = root.y + (srcHips.position.y - root.y) * legRatio;
        vt.position += target - dstHips.position;

        // Hands: different arm lengths leave the retargeted hands short of (or past) the gun's grips. Thurz's hidden
        // hands are exactly on them, so pull the artist's hands onto his, bending the elbows the way his bend.
        if (HandIK)
        {
            Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        }
    }

    /// <summary>Hands follow Thurz's hands exactly (on the gun's grips). Off = pure retarget.</summary>
    public static bool HandIK = true;

    void Reach(HumanBodyBones upB, HumanBodyBones lowB, HumanBodyBones handB)
    {
        Transform up = visualAnim.GetBoneTransform(upB), low = visualAnim.GetBoneTransform(lowB), hand = visualAnim.GetBoneTransform(handB);
        Transform srcLow = sourceAnim.GetBoneTransform(lowB), srcHand = sourceAnim.GetBoneTransform(handB);
        if (!up || !low || !hand || !srcLow || !srcHand) return;
        SolveTwoBone(up, low, hand, srcHand.position, srcLow.position);
        // Match the hand's turn too (relative to how the two rigs' hands differ at rest is unknown, so follow the forearm
        // twist the retarget gave and only fix position).
    }

    // Analytic two-bone IK: put 'end' at target, elbow bending toward 'hint'.
    static void SolveTwoBone(Transform root, Transform mid, Transform end, Vector3 target, Vector3 hint)
    {
        float a = Vector3.Distance(root.position, mid.position);
        float b = Vector3.Distance(mid.position, end.position);
        Vector3 toTarget = target - root.position;
        float d = Mathf.Clamp(toTarget.magnitude, 0.001f, (a + b) * 0.999f);
        Vector3 dir = toTarget.normalized;
        float cos = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
        Vector3 bend = Vector3.ProjectOnPlane(hint - root.position, dir);
        if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(mid.position - root.position, dir);
        bend.Normalize();
        Vector3 elbow = root.position + dir * (a * cos) + bend * (a * Mathf.Sqrt(1f - cos * cos));
        root.rotation = Quaternion.FromToRotation(mid.position - root.position, elbow - root.position) * root.rotation;
        mid.rotation = Quaternion.FromToRotation(end.position - mid.position, target - mid.position) * mid.rotation;
    }

    Transform srcHips, dstHips;
    float legRatio = 1f;

    // A rig-independent frame for the pelvis: right = right hip - left hip, up = hips -> chest.
    static Quaternion BodyFrame(Animator a)
    {
        var hips = a.GetBoneTransform(HumanBodyBones.Hips);
        var l = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        var r = a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        var chest = a.GetBoneTransform(HumanBodyBones.Chest) ?? a.GetBoneTransform(HumanBodyBones.Spine);
        if (!hips || !l || !r || !chest) return hips ? hips.rotation : a.transform.rotation;
        Vector3 right = r.position - l.position;
        Vector3 up = chest.position - hips.position;
        Vector3 fwd = Vector3.Cross(right, up);
        return fwd.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fwd, up) : hips.rotation;
    }

    static float LegLength(Animator a)
    {
        var up = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        var low = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        var foot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
        if (!up || !low || !foot) return 0f;
        return Vector3.Distance(up.position, low.position) + Vector3.Distance(low.position, foot.position);
    }
}
