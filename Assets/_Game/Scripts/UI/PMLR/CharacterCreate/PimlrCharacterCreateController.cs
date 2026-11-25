using DG.Tweening;
using PIMLR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    // #UI (Slice 2): optional themed layout. When swatchGrid is set, the compact palette replaces the chip grid;
    // colors still go through SelectColor -> changeColor.SetColor (saved to PlayerPrefs immediately, unchanged).
    [Header("Themed layout (#UI, optional)")]
    public PimlrSwatchGrid swatchGrid;
    [Tooltip("The panel's Save / Done button: the palette's bottom row moves down to it.")]
    public Selectable doneButton;
    [Tooltip("Brief 'SAVED' confirmation after each pick (colors save instantly).")]
    public TMP_Text savedFeedback;

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
            part.focused = idx => { if (idx != currentCloth) SelectPart(idx); }; // #UI: themed tabs only (see PimlrBodyPartTile)
        }

        if (swatchGrid)
        {
            swatchGrid.Build(PimlrPalette.Colors, PimlrPalette.Names, PimlrPalette.DisplayOrder);
            swatchGrid.exitDown = doneButton;
            swatchGrid.Picked += idx => { SelectColor(idx); ShowSaved(idx); };
        }
        if (savedFeedback) savedFeedback.alpha = 0f;

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
        currentPartName = partName;

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
        if (swatchGrid) { swatchGrid.SetEquipped(paletteIndex); LinkGrid(); }
    }

    // #UI: cursor flow. Active tab <-> palette top row; every tab's down lands on the EQUIPPED color;
    // palette bottom row <-> done button. Written for both gamepad (PimlrNavHint) and keyboard (Navigation).
    string currentPartName = "";
    void LinkGrid()
    {
        PimlrBodyPartTile active = null;
        foreach (var p in parts) if (p.clothIndex == currentCloth && p.isActiveAndEnabled && p.button) { active = p; break; }
        swatchGrid.exitUp = active ? active.button : null;
        swatchGrid.exitDown = doneButton;
        swatchGrid.Relink();
        foreach (var p in parts) if (p.isActiveAndEnabled && p.button) swatchGrid.LinkEntry(p.button, Vector2Int.down);

        // Tab row: left/right along the row, holding at the ends (no jumping to other panels).
        var row = new System.Collections.Generic.List<PimlrBodyPartTile>();
        foreach (var p in parts) if (p.isActiveAndEnabled && p.button) row.Add(p);
        row.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        for (int i = 0; i < row.Count; i++)
        {
            var b = row[i].button;
            var left = i > 0 ? row[i - 1].button : b;
            var right = i < row.Count - 1 ? row[i + 1].button : b;
            var h = PimlrNavHint.On(b); h.left = left; h.right = right;
            var n = b.navigation; n.mode = Navigation.Mode.Explicit; n.selectOnLeft = left; n.selectOnRight = right; b.navigation = n;
        }
        if (doneButton) swatchGrid.LinkEntry(doneButton, Vector2Int.up);
    }

    Tween savedTween;
    void ShowSaved(int paletteIndex)
    {
        if (!savedFeedback) return;
        savedFeedback.text = $"{currentPartName} · {PimlrPalette.NameOf(paletteIndex)} · SAVED";
        if (savedTween != null && savedTween.IsActive()) savedTween.Kill();
        savedFeedback.alpha = 1f;
        savedTween = DOTween.To(() => savedFeedback.alpha, a => savedFeedback.alpha = a, 0f, 0.4f).SetDelay(1.4f).SetUpdate(true);
    }

    void OnDisable()
    {
        if (savedTween != null && savedTween.IsActive()) savedTween.Kill();
        if (savedFeedback) savedFeedback.alpha = 0f;
    }
}
