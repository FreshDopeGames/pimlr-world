using UnityEngine;
using UnityEngine.UI;

/// <summary>PIMLR: one swatch in the character-create color grid. Wired at runtime by PimlrCharacterCreateController.</summary>
public class PimlrColorChip : MonoBehaviour
{
    public int paletteIndex;
    public Image swatch;
    public Image highlight;   // 10px ring shown only when selected
    public Button button;

    public void Apply()
    {
        if (swatch) swatch.color = PimlrPalette.Get(paletteIndex);
    }

    public void SetSelected(bool selected)
    {
        if (highlight) highlight.enabled = selected;
    }
}
