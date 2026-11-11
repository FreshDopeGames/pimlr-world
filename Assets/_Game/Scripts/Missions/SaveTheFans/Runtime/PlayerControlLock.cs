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
        static void ResetStatics() { locked = null; depth = 0; }

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
        }

        public static void Unlock()
        {
            if (depth == 0) return;
            depth--;
            if (depth > 0 || !locked) return;

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
            if (depth == 0) return;
            depth = 1;
            Unlock();
        }
    }
}
