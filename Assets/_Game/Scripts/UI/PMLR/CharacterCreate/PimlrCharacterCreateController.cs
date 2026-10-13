using TMPro;
using UnityEngine;

/// <summary>
/// PIMLR: drives the rebuilt character-create panel (body-part tiles + 12-color grid).
/// Talks to the scene's changeColor component on GO_ScriptChangeColor, the same one the legacy
/// ContentChanger used, so saving (PlayerPrefs per renderer) and YannicksWorld apply are unchanged.
/// UI is built by the editor menu PIMLR > UI > Build Character Create Panel.
/// </summary>
public class PimlrCharacterCreateController : MonoBehaviour
{
    public TextMeshProUGUI partLabel;
    public TextMeshProUGUI colorNameLabel;
    [Tooltip("Body part selected when the panel opens the first time (0 Shirt, 1 Hair, 2 Pants, 3 Shoes).")]
    public int defaultClothIndex = 0;

    PimlrBodyPartTile[] parts;
    PimlrColorChip[] chips;
    changeColor target;
    int currentCloth = -1;

    void Awake()
    {
        parts = GetComponentsInChildren<PimlrBodyPartTile>(true);
        chips = GetComponentsInChildren<PimlrColorChip>(true);

        foreach (var p in parts)
        {
            var part = p;
            if (part.button) part.button.onClick.AddListener(() => SelectPart(part.clothIndex));
        }

        foreach (var c in chips)
        {
            var chip = c;
            chip.Apply();
            if (chip.button) chip.button.onClick.AddListener(() => SelectColor(chip.paletteIndex));
        }
    }

    void OnEnable()
    {
        ResolveTarget();
        if (target) target.ApplySavedColors(); // keep the preview in sync with what's saved
        SelectPart(currentCloth >= 0 ? currentCloth : defaultClothIndex);
    }

    void ResolveTarget()
    {
        if (target) return;

        // Prefer the exact changeColor (not the Customization_Applier subclass) on GO_ScriptChangeColor.
        var go = GameObject.Find("GO_ScriptChangeColor");
        if (go)
        {
            foreach (var c in go.GetComponents<changeColor>())
                if (c.GetType() == typeof(changeColor)) { target = c; break; }
            if (!target) target = go.GetComponent<changeColor>();
        }

        if (!target)
        {
            foreach (var c in FindObjectsByType<changeColor>(FindObjectsSortMode.None))
                if (c.GetType() == typeof(changeColor)) { target = c; break; }
        }

        if (!target)
            Debug.LogWarning("[PimlrCharacterCreateController] No changeColor found in the scene; colors won't apply.", this);
    }

    public void SelectPart(int clothIndex)
    {
        currentCloth = clothIndex;
        if (target) target.SelectCloth(clothIndex);

        string partName = "";
        foreach (var p in parts)
        {
            bool on = p.clothIndex == clothIndex;
            p.SetSelected(on);
            if (on) partName = p.displayName;
        }
        if (partLabel) partLabel.text = partName;

        RefreshColorSelection(SavedColorIndex(clothIndex));
    }

    public void SelectColor(int paletteIndex)
    {
        if (!PimlrPalette.IsValid(paletteIndex)) return;
        if (target && currentCloth >= 0) target.SetColor(paletteIndex);
        RefreshColorSelection(paletteIndex);
    }

    // Same lookup the scene uses to colour the player on load, so the highlighted chip always matches the character.
    int SavedColorIndex(int clothIndex) => target ? target.GetSavedColorIndex(clothIndex) : -1;

    void RefreshColorSelection(int paletteIndex)
    {
        foreach (var c in chips) c.SetSelected(c.paletteIndex == paletteIndex);
        if (colorNameLabel) colorNameLabel.text = PimlrPalette.NameOf(paletteIndex);
    }
}
