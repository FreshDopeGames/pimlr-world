// #UI: one place that decides which layout profile a screen should use.
//   Orientation: Portrait when the screen is narrower than 1.2 : 1 (phones held upright, tall browser windows),
//                otherwise Landscape (16:9, 21:9, 4:3, short browser windows, phones on their side).
//   Touch:       real touch devices (mobile platforms / touch-capable browsers on phones), or forced by the
//                aspect check / an option in Settings later. Touch layouts use >= 44 pt tap targets.
// Screens poll Signature() in LateUpdate and re-lay out only when it changes (window resize, rotation).
//
// Unreal note: maps to a UMG "Platform/Orientation" switch (e.g. a widget switcher driven by the viewport's
// aspect + UPlatformMisc::SupportsTouchInput), or CommonUI input-type changes for touch.

using UnityEngine;

namespace PIMLR.UI
{
    public enum LayoutOrientation { Landscape, Portrait }

    public static class PimlrResponsive
    {
        public const float PortraitBelowAspect = 1.2f;

        /// <summary>Editor/testing override: null = detect, true/false = force touch layouts.</summary>
        public static bool? ForceTouch;

        public static LayoutOrientation Orientation =>
            Screen.height > 0 && (float)Screen.width / Screen.height < PortraitBelowAspect ? LayoutOrientation.Portrait : LayoutOrientation.Landscape;

        public static bool Touch
        {
            get
            {
                if (ForceTouch.HasValue) return ForceTouch.Value;
                if (Application.isMobilePlatform) return true;
#if UNITY_WEBGL && !UNITY_EDITOR
                // Touch-capable browser on a phone-sized screen (avoids switching touchscreen laptops).
                return Input.touchSupported && Mathf.Min(Screen.width, Screen.height) / Mathf.Max(1f, Screen.dpi > 0 ? Screen.dpi : 160f) < 4.5f;
#else
                return false;
#endif
            }
        }

        /// <summary>Changes whenever a re-layout is needed.</summary>
        public static int Signature()
        {
            unchecked { return Screen.width * 73856093 ^ Screen.height * 19349663 ^ (Touch ? 83492791 : 0); }
        }

        /// <summary>Minimum comfortable tap target in screen pixels: 44 pt, using the phone's short side as the point scale
        /// (375-pt-wide reference). The aspect check uses the same rule.</summary>
        public static float MinTouchPx => 44f * Mathf.Min(Screen.width, Screen.height) / 375f;

        /// <summary>Scale that keeps 1080p-designed UI at the same physical size relative to the screen's short side.</summary>
        public static float ShortSideScale => Mathf.Min(Screen.width, Screen.height) / 1080f;
    }
}
