// PIMLR #STF: look settings for the Save The Fans UI (placeholder until the new UI design lands).
// Live copy: Assets/_Game/Resources/PIMLR/STFStyle.asset (loaded at runtime, so no scene references are needed).

using TMPro;
using UnityEngine;

namespace PIMLR.Missions.SaveTheFans
{
    [CreateAssetMenu(menuName = "PIMLR/Save The Fans Style", fileName = "STFStyle")]
    public class SaveTheFansStyle : ScriptableObject
    {
        [Tooltip("Font for every Save The Fans text: HUD, fan health bars, wave banners.")]
        public TMP_FontAsset font;

        [Header("Wave-clear banner (letterbox, like WASHED / HATER FREE, no slow motion)")]
        public Color bannerColor = new Color(0.2f, 0.8f, 0.3f); // the fan square on the threat meter
        [Tooltip("Word size relative to HATER FREE.")]
        [Range(0.3f, 1f)] public float bannerSize = 0.6f;
        [Min(0f)] public float bannerHoldSeconds = 1.2f;

        static SaveTheFansStyle cached;
        public static SaveTheFansStyle Current
        {
            get
            {
                if (!cached) cached = Resources.Load<SaveTheFansStyle>("PIMLR/STFStyle");
                return cached;
            }
        }

        public static TMP_FontAsset Font => Current ? Current.font : null;
    }
}
