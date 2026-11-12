// PIMLR #STF (Save The Fans) — freeze/unfreeze the JUTPS player for cinematics.
// Same flags AiChatInteraction.PlayerControllerStop() uses, but it REMEMBERS the previous
// values and restores them, instead of forcing everything back to true (which would undo
// a music boost or a dialogue lock that was active before the cutscene).
//
// Unreal note: collapses to DisableInput(PlayerController) / EnableInput, or setting an
// "IsInCinematic" flag the Character checks. SetCinematicMode() on the PlayerController
// does movement + look + HUD hiding in one call.

using JUTPS;
using UnityEngine;

namespace PIMLR.Missions.SaveTheFans
{
    public static class PlayerControlLock
    {
        struct Snapshot
        {
            public bool blockH, blockV, blockFire, canMove, canJump, canRotate, roll, defaultInput;
        }

        static JUCharacterController locked;
        static Snapshot saved;
        static int depth;

        // Same convention as the rest of PIMLR: statics survive play sessions if domain reload is ever turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { locked = null; depth = 0; aimFrozenOn = null; aimDepth = 0; }

        public static JUCharacterController ResolvePlayer()
        {
            var gem = GameExecutionManager.Instance;
            if (gem != null && gem.playerHandler != null && gem.playerHandler.jUCharacterController != null)
                return gem.playerHandler.jUCharacterController;

            var tagged = GameObject.FindGameObjectWithTag("Player");
            return tagged ? tagged.GetComponent<JUCharacterController>() : null;
        }

        public static void Lock()
        {
            depth++;
            if (depth > 1) return;

            locked = ResolvePlayer();
            if (!locked) return;

            saved = new Snapshot
            {
                blockH = locked.BlockHorizontalInput,
                blockV = locked.BlockVerticalInput,
                blockFire = locked.BlockFireModeOnCursorVisible,
                canMove = locked.CanMove,
                canJump = locked.CanJump,
                canRotate = locked.CanRotate,
                roll = locked.EnableRoll,
                defaultInput = locked.UseDefaultControllerInput
            };

            locked.BlockHorizontalInput = true;
            locked.BlockVerticalInput = true;
            locked.CanMove = false;
            locked.CanJump = false;
            locked.CanRotate = false;
            locked.EnableRoll = false;
            locked.UseDefaultControllerInput = false;
            FreezeAim(locked);
        }

        // PIMLR #STF: the player's arms / water gun aim at JUTPS's LookAtPosition, which (when zero) follows the
        // camera's centre ray. During a fly-over the camera looks at the fan / the newest enemy, so the gun swung
        // toward it and the arms twisted depending on where the player stood. While frozen, the aim point is fixed
        // straight ahead of the player's body at chest height.
        static JUCharacterController aimFrozenOn;
        static Vector3 savedLookAt;
        static int aimDepth;

        public static void FreezeAim(JUCharacterController pc = null)
        {
            if (!pc) pc = ResolvePlayer();
            if (!pc) return;
            if (aimDepth > 0 && !aimFrozenOn) aimDepth = 0; // the frozen player was destroyed (scene reload)
            aimDepth++;
            if (aimDepth > 1) return;
            aimFrozenOn = pc;
            savedLookAt = pc.LookAtPosition;
            Vector3 fwd = pc.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            pc.LookAtPosition = pc.transform.position + Vector3.up * 1.45f + fwd.normalized * 30f;
        }

        public static void UnfreezeAim()
        {
            if (aimDepth == 0) return;
            aimDepth--;
            if (aimDepth > 0) return;
            if (aimFrozenOn) aimFrozenOn.LookAtPosition = savedLookAt; // normally zero = follow the camera again
            aimFrozenOn = null;
        }

        public static void Unlock()
        {
            if (depth == 0) return;
            depth--;
            if (depth > 0 || !locked) return;
            UnfreezeAim();

            locked.BlockHorizontalInput = saved.blockH;
            locked.BlockVerticalInput = saved.blockV;
            locked.BlockFireModeOnCursorVisible = saved.blockFire;
            locked.CanMove = saved.canMove;
            locked.CanJump = saved.canJump;
            locked.CanRotate = saved.canRotate;
            locked.EnableRoll = saved.roll;
            locked.UseDefaultControllerInput = saved.defaultInput;
            locked = null;
        }

        /// Safety valve if something interrupts a cinematic (scene reload, exception).
        public static void ForceUnlock()
        {
            if (aimDepth > 0) { aimDepth = 1; UnfreezeAim(); }
            if (depth == 0) return;
            depth = 1;
            Unlock();
        }
    }
}
