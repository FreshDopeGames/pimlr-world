using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PIMLR: optional controller-navigation hints for a button, read by PimlrMenuNavigator.
/// Add it in the Inspector (or from code with PimlrNavHint.On(button)) when the automatic spatial navigation
/// needs help.
/// </summary>
[DisallowMultipleComponent]
public class PimlrNavHint : MonoBehaviour
{
    [Tooltip("Selected when its menu opens. Highest value wins if several are visible.")]
    public int defaultPriority;
    [Tooltip("B / Circle presses this button (Close / Back). Buttons named Close/Back/Cancel already work.")]
    public bool isCancel;
    [Tooltip("Never focus this with a controller (e.g. a text field nobody needs to type in).")]
    public bool skip;
    [Tooltip("Treat as a real menu button even if it sits inside the gameplay HUD (JUTPS Interface / music widget).")]
    public bool isMenu;

    [Header("Explicit links (optional - leave empty for automatic)")]
    public Selectable up;
    public Selectable down;
    public Selectable left;
    public Selectable right;

    public static PimlrNavHint On(Component c)
    {
        if (c == null) return null;
        return c.TryGetComponent(out PimlrNavHint h) ? h : c.gameObject.AddComponent<PimlrNavHint>();
    }
}
