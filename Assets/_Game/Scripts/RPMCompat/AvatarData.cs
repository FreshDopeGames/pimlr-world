using UnityEngine;

namespace ReadyPlayerMe.Core
{
    // PIMLR compat stub: replaces the AvatarData component from the removed Ready Player Me Core package.
    // Keeps the original script GUID so existing character prefabs don't show "Missing Script".
    // Pure data; nothing in PIMLR reads it. Safe to remove from prefabs later.
    public class AvatarData : MonoBehaviour
    {
        public string AvatarId;
    }
}
