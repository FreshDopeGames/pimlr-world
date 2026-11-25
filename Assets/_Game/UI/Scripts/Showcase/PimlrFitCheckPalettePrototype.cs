// #UI: PROTOTYPE ONLY (UI_FitCheckPalette scene). Demonstrates the compact Fit Check palette and its cursor flow
// with the real 12-color PimlrPalette (read-only). Equipped colors here are sample data and are never saved;
// the Fit Check slice will feed PimlrSwatchGrid from PimlrCharacterCreateController / changeColor instead.
//
// Cursor flow:
//   Tabs (HAIR SHIRT PANTS SHOES): left/right along the row, stop at the ends. Down -> the EQUIPPED chip.
//   Grid: see PimlrSwatchGrid. Top row up -> the active tab. Bottom row down -> SAVE FIT.
//   SAVE FIT: up -> equipped chip, right -> BACK. BACK: left -> SAVE FIT, up -> equipped chip.

using PIMLR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PimlrFitCheckPalettePrototype : MonoBehaviour
{
    public Button[] tabs;
    public PimlrSwatchGrid grid;
    public Button saveButton;
    public Button backButton;
    public TMP_Text status;

    readonly int[] equipped = { 3, 0, 6, 0 }; // sample: black hair, white shirt, navy pants, white shoes
    int part;

    void Start()
    {
        grid.Build(PimlrPalette.Colors, PimlrPalette.Names, PimlrPalette.DisplayOrder);
        grid.Picked += idx =>
        {
            equipped[part] = idx;
            grid.LinkEntry(tabs[part], Vector2Int.down);
            LinkFooter();
            if (status) status.text = $"{tabs[part].GetComponentInChildren<TMP_Text>().text}: {PimlrPalette.NameOf(idx)}  (sample, not saved)";
        };
        for (int i = 0; i < tabs.Length; i++)
        {
            int k = i;
            tabs[i].onClick.AddListener(() => SelectPart(k));
            Link(tabs[i], up: null, down: null, left: i > 0 ? tabs[i - 1] : tabs[i], right: i < tabs.Length - 1 ? tabs[i + 1] : tabs[i]);
        }
        Link(backButton, up: null, down: backButton, left: saveButton, right: backButton);
        SelectPart(0);
        EventSystem.current?.SetSelectedGameObject(tabs[0].gameObject);
    }

    void SelectPart(int i)
    {
        part = i;
        for (int t = 0; t < tabs.Length; t++)
        {
            var tb = tabs[t].GetComponent<ThemedButton>();
            tb.variant = t == i ? ButtonVariant.Secondary : ButtonVariant.Tertiary;
            tb.Apply();
            tabs[t].transform.Find("Active Bar")?.gameObject.SetActive(t == i);
        }
        grid.exitUp = tabs[i];
        grid.exitDown = saveButton;
        grid.SetEquipped(equipped[i]);
        grid.Relink();
        foreach (var t in tabs) grid.LinkEntry(t, Vector2Int.down);
        LinkFooter();
    }

    void LinkFooter()
    {
        Link(saveButton, up: grid.EntryPoint, down: saveButton, left: saveButton, right: backButton);
        var bh = PimlrNavHint.On(backButton); bh.up = grid.EntryPoint;
        var n = backButton.navigation; n.mode = Navigation.Mode.Explicit; n.selectOnUp = grid.EntryPoint; backButton.navigation = n;
    }

    static void Link(Selectable s, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        var h = PimlrNavHint.On(s);
        if (up) h.up = up; if (down) h.down = down; if (left) h.left = left; if (right) h.right = right;
        var n = s.navigation; n.mode = Navigation.Mode.Explicit;
        if (up) n.selectOnUp = up; if (down) n.selectOnDown = down; if (left) n.selectOnLeft = left; if (right) n.selectOnRight = right;
        s.navigation = n;
    }
}
