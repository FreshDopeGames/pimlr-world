// PIMLR #GREN: holds a JUTPS character still (no translation, no locomotion slide) while it throws or aims a water
// balloon. Movement input is zeroed after every script has written it (execution order 200) and the Rigidbody's
// horizontal velocity is cleared after JUTPS's own FixedUpdate, so neither the AI (PimlrBomberAI / PatrolAI) nor the
// player's stick can walk the character while the throw clip plays. Turning on the spot is still allowed.
// Several callers can hold it at once (throw + aim): it releases when the last one lets go.
//
// Unreal note: on the Character, set MaxWalkSpeed to 0 (or DisableMovement()) for the montage's duration and
// StopMovementImmediately() on start.

using System.Collections.Generic;
using JUTPS;
using UnityEngine;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public class PimlrThrowLock : MonoBehaviour
{
    readonly HashSet<object> holders = new HashSet<object>();
    JUCharacterController character;
    Rigidbody body;
    bool savedBlockH, savedBlockV, blocking;

    public bool Locked => holders.Count > 0;

    public static PimlrThrowLock For(Component c)
    {
        if (!c) return null;
        return c.TryGetComponent(out PimlrThrowLock l) ? l : c.gameObject.AddComponent<PimlrThrowLock>();
    }

    void Awake()
    {
        character = GetComponent<JUCharacterController>();
        body = GetComponent<Rigidbody>();
    }

    public void Hold(object who)
    {
        if (who == null) return;
        holders.Add(who);
        if (!blocking && character)
        {
            blocking = true;
            savedBlockH = character.BlockHorizontalInput;
            savedBlockV = character.BlockVerticalInput;
        }
        Freeze();
    }

    public void Release(object who)
    {
        holders.Remove(who);
        if (holders.Count == 0) Restore();
    }

    public void ReleaseAll() { holders.Clear(); Restore(); }

    void OnDisable() => ReleaseAll();

    void Restore()
    {
        if (!blocking || !character) { blocking = false; return; }
        blocking = false;
        character.BlockHorizontalInput = savedBlockH;
        character.BlockVerticalInput = savedBlockV;
    }

    void Update() { if (Locked) Freeze(); }

    void Freeze()
    {
        if (!character) return;
        character.BlockHorizontalInput = true;
        character.BlockVerticalInput = true;
        character._Move(0f, 0f, false);
        character.IsMoving = false;
        character.VelocityMultiplier = 0f;
    }

    void FixedUpdate()
    {
        if (!Locked || !body || body.isKinematic) return;
        if (character && (character.IsDriving || character.IsDead || character.IsRagdolled)) return;
        Vector3 v = body.linearVelocity;
        body.linearVelocity = new Vector3(0f, v.y, 0f);
    }
}
