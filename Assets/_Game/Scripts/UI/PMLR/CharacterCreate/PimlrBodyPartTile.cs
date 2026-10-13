using UnityEngine;
using UnityEngine.UI;

/// <summary>PIMLR: one body-part tile (Hair/Shirt/Pants/Shoes) in the character-create panel.</summary>
public class PimlrBodyPartTile : MonoBehaviour
{
    [Tooltip("Index into changeColor.renderers / SelectCloth: 0 Shirt, 1 Hair, 2 Pants, 3 Shoes.")]
    public int clothIndex;
    public string displayName;
    public Image frame;       // border; accent when selected
    public Button button;

    public static readonly Color SelectedColor = new Color(0.095f, 0.745f, 0f, 1f); // SAVE-button green
    public static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.12f);

    public void SetSelected(bool selected)
    {
        if (frame) frame.color = selected ? SelectedColor : IdleColor;
    }
}
