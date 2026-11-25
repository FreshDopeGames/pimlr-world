// #UI: shared lifecycle for every Themed* component, so they all behave the same:
// - OnEnable: subscribe (remove-then-add, so a double OnEnable can't double-subscribe) and apply once.
// - OnDisable / OnDestroy: unsubscribe. Pooling, scene unloads and Destroy never leave stale handlers.
// - OnValidate (Editor): never touches graphics directly; schedules one deferred apply for the next editor tick.
// - Prefab ASSETS in the Project window are never written to by the live re-skin (only scene objects and
//   objects open in Prefab Mode), so browsing assets can't silently modify prefabs.
// - No theme asset: nothing happens (the theme warns once); the object keeps its authored look.
//
// Unreal note: equivalent to a base UUserWidget that binds to the style subsystem's OnStyleChanged in
// NativeConstruct and unbinds in NativeDestruct.

using UnityEngine;

namespace PIMLR.UI
{
    public abstract class ThemedBehaviour : MonoBehaviour
    {
        [Tooltip("Optional: use this theme instead of the global one (showcase A/B, special screens).")]
        public PimlrUITheme themeOverride;

        protected PimlrUITheme Theme => themeOverride ? themeOverride : PimlrUITheme.Current;

        bool subscribed;
#if UNITY_EDITOR
        bool applyQueued;
#endif

        protected virtual void OnEnable()
        {
            Subscribe();
            Apply();
        }

        protected virtual void OnDisable() => Unsubscribe();

        protected virtual void OnDestroy() => Unsubscribe();

        void Subscribe()
        {
            PimlrUITheme.Changed -= Apply;
            PimlrUITheme.Changed += Apply;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed) return;
            PimlrUITheme.Changed -= Apply;
            subscribed = false;
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (applyQueued) return;
            applyQueued = true;
            PimlrEditorDefer.NextTick(() =>
            {
                applyQueued = false;
                if (this && isActiveAndEnabled) Apply();
            });
        }
#endif

        /// <summary>Re-skin from the current theme. Safe to call any time; does nothing without a theme.</summary>
        public void Apply()
        {
            if (!this) return;
#if UNITY_EDITOR
            // Persistent prefab assets (not in a scene, not in Prefab Mode) are left untouched.
            if (!gameObject.scene.IsValid()) return;
#endif
            var theme = Theme;
            if (!theme) return;
            ApplyTheme(theme);
        }

        protected abstract void ApplyTheme(PimlrUITheme theme);

        // Assign only on change, so opening a scene doesn't dirty it or churn prefab overrides.
        protected static bool Set<T>(ref T field, T value)
        {
            if (Equals(field, value)) return false;
            field = value;
            return true;
        }

        protected static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.002f && Mathf.Abs(a.g - b.g) < 0.002f && Mathf.Abs(a.b - b.b) < 0.002f && Mathf.Abs(a.a - b.a) < 0.002f;
    }
}
