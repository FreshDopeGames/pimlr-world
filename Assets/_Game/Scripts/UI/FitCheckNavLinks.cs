using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Fit Check: feeds PimlrMenuNavigator explicit PimlrNavHint links.
/// Up/Down walks the body parts, or the swatch grid; Right goes parts -> swatches, Left goes back.
public class FitCheckNavLinks : MonoBehaviour
{
    [SerializeField] Transform bodyPartsRoot;   // parent of HAIR / SHIRT / PANTS / SHOES
    [SerializeField] Transform swatchesRoot;    // parent of the swatches (hierarchy order = visual order)
    [SerializeField] Selectable saveButton;     // optional
    [SerializeField] int swatchColumns = 4;
    [SerializeField] float refreshInterval = 0.25f;

    readonly List<Selectable> parts = new List<Selectable>();
    readonly List<Selectable> swatches = new List<Selectable>();
    Selectable lastPart, lastSwatch;
    float timer;

    void OnEnable() { timer = 0f; }

    void Update()
    {
        var es = EventSystem.current;
        var cur = es != null ? es.currentSelectedGameObject : null;
        if (cur != null && cur.TryGetComponent(out Selectable s))
        {
            if (parts.Contains(s)) lastPart = s;
            else if (swatches.Contains(s)) lastSwatch = s;
        }

        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = refreshInterval;
        Collect(bodyPartsRoot, parts);
        Collect(swatchesRoot, swatches);
        Wire();
    }

    static void Collect(Transform root, List<Selectable> into)
    {
        into.Clear();
        if (!root) return;
        foreach (var s in root.GetComponentsInChildren<Selectable>(false))
            if (s.IsInteractable()) into.Add(s);
    }

    // A link to itself means "stay put": it stops the navigator falling back to geometry.
    static void Link(Selectable s, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        if (!s.TryGetComponent(out PimlrNavHint h)) h = s.gameObject.AddComponent<PimlrNavHint>();
        h.up = up ? up : s;  h.down = down ? down : s;
        h.left = left ? left : s;  h.right = right ? right : s;
    }

    void Wire()
    {
        int pn = parts.Count, sn = swatches.Count, cols = Mathf.Max(1, swatchColumns);
        Selectable partTarget = lastPart ? lastPart : (pn > 0 ? parts[0] : null);
        Selectable swatchTarget = lastSwatch ? lastSwatch : (sn > 0 ? swatches[0] : null);

        for (int i = 0; i < pn; i++)   // body parts: wrap top <-> bottom
            Link(parts[i], parts[(i - 1 + pn) % pn], parts[(i + 1) % pn], null, swatchTarget);

        for (int i = 0; i < sn; i++)   // swatches: no wrap, bottom row falls to SAVE
        {
            int c = i % cols;
            Selectable up    = i - cols >= 0 ? swatches[i - cols] : null;
            Selectable down  = i + cols < sn ? swatches[i + cols] : saveButton;
            Selectable left  = c > 0 ? swatches[i - 1] : partTarget;
            Selectable right = (c < cols - 1 && i + 1 < sn) ? swatches[i + 1] : null;
            Link(swatches[i], up, down, left, right);
        }

        if (saveButton) Link(saveButton, swatchTarget, null, partTarget, null);
    }
}