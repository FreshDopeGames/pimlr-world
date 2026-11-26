using PIMLR.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>PIMLR: one body-part tile (Hair/Shirt/Pants/Shoes) in the character-create panel.</summary>
public class PimlrBodyPartTile : MonoBehaviour, ISelectHandler
{
    [Tooltip("Index into changeColor.renderers / SelectCloth: 0 Shirt, 1 Hair, 2 Pants, 3 Shoes.")]
    public int clothIndex;
    public string displayName;
    public Image frame;       // border; accent when selected
    public Button button;

    // #UI (Slice 2): themed tabs show the active part with a variant swap + lime bar instead of tinting the frame.
    [Tooltip("Optional: shown only on the active part (themed tabs).")]
    public GameObject activeBar;

    public static readonly Color SelectedColor = new Color(0.095f, 0.745f, 0f, 1f); // SAVE-button green
    public static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.12f);

    /// <summary>#UI: themed tabs switch part as soon as the cursor lands on them (controller/keyboard), so the palette
    /// below always edits the part the cursor came from. Switching parts never changes colors.</summary>
    public System.Action<int> focused;
    public void OnSelect(BaseEventData e) { if (activeBar) focused?.Invoke(clothIndex); }

    public void SetSelected(bool selected)
    {
        // #UI: themed tab path
        if (TryGetComponent(out ThemedButton themed))
        {
            var v = selected ? ButtonVariant.Secondary : ButtonVariant.Tertiary;
            if (themed.variant != v) { themed.variant = v; themed.Apply(); }
            if (activeBar) activeBar.SetActive(selected);
            return;
        }
        if (frame) frame.color = selected ? SelectedColor : IdleColor;
    }
}
