// #UI: paints a uGUI Image from the PIMLR UI theme (color token + optional sprite slot, 9-slice aware).
// Add it to any existing Image: gameplay scripts, buttons and events are untouched.
// Empty theme sprite slot = the Image keeps its own sprite (sprites are optional; nothing breaks).
//
// Unreal note: a UImage inside a base widget whose brush/tint are read from UPimlrUIStyle in PreConstruct.

using UnityEngine;
using UnityEngine.UI;

namespace PIMLR.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("PIMLR/UI/Themed Image")]
    public class ThemedImage : ThemedBehaviour
    {
        public ColorToken color = ColorToken.SurfaceBase;
        [Range(0f, 1f)] public float alpha = 1f;

        [Tooltip("Pattern slot only: use the theme's patternOpacity instead of Alpha, so all pattern layers change together.")]
        public bool followPatternOpacity = true;

        [Tooltip("Leave the Image's color alone (sprite only).")]
        public bool keepColor;

        [Tooltip("None = keep the Image's current sprite.")]
        public SpriteSlot sprite = SpriteSlot.None;

        [Tooltip("Sliced when the sprite has 9-slice borders, Tiled for pattern slots. Off = keep the Image's type (Filled bars, etc.).")]
        public bool autoImageType = true;

        Image image;

        protected override void ApplyTheme(PimlrUITheme theme)
        {
            if (!image) image = GetComponent<Image>();
            if (!image) return;

            if (!keepColor)
            {
                float a = sprite == SpriteSlot.Pattern && followPatternOpacity ? theme.patternOpacity : alpha;
                var c = theme.Color(color, a);
                if (!Same(image.color, c)) image.color = c;
            }

            var s = theme.Sprite(sprite);
            if (!s) return;
            if (image.sprite != s) image.sprite = s;
            if (!autoImageType) return;
            var t = (sprite == SpriteSlot.Pattern || sprite == SpriteSlot.Halftone) ? Image.Type.Tiled
                  : s.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            if (image.type != t) image.type = t;
        }
    }
}
