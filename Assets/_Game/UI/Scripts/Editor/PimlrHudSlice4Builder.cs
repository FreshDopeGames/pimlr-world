// #UI Slice 4a: restyle the gameplay HUD in place, inside Assets/_Game/Resources/Global UI.prefab.
// Positions and behavior are unchanged; only the look moves onto the PIMLR theme:
//   vitals      health / stamina bars: carbon track, flat fill; JUTPS's own color logic kept, palette from the theme
//               (health: lime full -> danger empty, cyan healing, orange damage; stamina: cyan, gray when exhausted)
//   weapon      ammo block on a carbon card, Utility type
//   coins       carbon card, Utility type
//   objectives  carbon card, Caption type at the 14 px floor (was 12), lime checkmarks
//   kill count  carbon card, Caption / Utility type
//   minimap     forest ring around the map, lime player blip, enemy / boss blips danger / orange
// Every Image / TMP touched is snapshotted (typed values) in a PimlrUISnapshot on the prefab root; Revert restores them,
// removes the added components and the "(#UI)" objects.
// Menus: PIMLR > UI > Slice 4a: Build HUD Restyle / Slice 4a: Revert HUD Restyle

using System.Collections.Generic;
using System.Linq;
using PIMLR.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PimlrHudSlice4Builder
{
    const string PrefabPath = "Assets/_Game/Resources/Global UI.prefab";
    const string Slice = "slice4a";

    [MenuItem("PIMLR/UI/Slice 4a: Build HUD Restyle")]
    public static string Build()
    {
        Revert();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var log = new List<string>();
        try
        {
            var snap = root.GetComponent<PimlrUISnapshot>() ?? root.AddComponent<PimlrUISnapshot>();
            Transform Find(string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            void Snap(Component c) { if (c) snap.entries.Add(PimlrUISnapshot.Capture(Slice, c)); }

            void ThemeImage(Transform t, ColorToken color, float alpha, SpriteSlot slot, bool keepColor = false, bool autoType = true)
            {
                var img = t ? t.GetComponent<Image>() : null;
                if (!img) { log.Add("missing image: " + (t ? t.name : "?")); return; }
                Snap(img);
                var ti = t.GetComponent<ThemedImage>() ?? t.gameObject.AddComponent<ThemedImage>();
                ti.color = color; ti.alpha = alpha; ti.sprite = slot; ti.keepColor = keepColor; ti.autoImageType = autoType;
                ti.Apply();
            }
            void ThemeText(Transform t, TypeRole role, float scale, bool keepColor = false, ColorToken? color = null, TextCase textCase = TextCase.FromRole)
            {
                var tmp = t ? t.GetComponent<TMP_Text>() : null;
                if (!tmp) { log.Add("missing text: " + (t ? t.name : "?")); return; }
                Snap(tmp);
                var tt = t.GetComponent<ThemedText>() ?? t.gameObject.AddComponent<ThemedText>();
                tt.role = role; tt.sizeScale = scale; tt.keepColor = keepColor; tt.textCase = textCase;
                if (color.HasValue) { tt.overrideColor = true; tt.color = color.Value; }
                tt.Apply();
            }

            var fields = root.GetComponent<PimlrThemedColorFields>() ?? root.AddComponent<PimlrThemedColorFields>();
            fields.entries.Clear();
            void ColorField(Component c, string field, ColorToken token) { if (c) fields.entries.Add(new PimlrThemedColorFields.Entry { target = c, field = field, token = token, alpha = 1f }); else log.Add("missing component for " + field); }

            // ---- vitals ----
            var hpBg = Find("Health_barBG");
            ThemeImage(hpBg, ColorToken.SurfaceScrim, 0.85f, SpriteSlot.BarBack);
            var hpFill = Find("Heath_bar_Fill");
            ThemeImage(hpFill, ColorToken.HealthGreen, 1f, SpriteSlot.BarFill, keepColor: true, autoType: false);
            var hpText = Find("Health Text (TMP)");
            ThemeText(hpText, TypeRole.Utility, 1.5f, keepColor: true);
            // Health color rule (Craig): green, red at or below 25%; no blends or heal/damage flashes.
            if (hpBg)
            {
                var hc = hpBg.gameObject.AddComponent<PimlrHealthBarColor>();
                hc.fill = hpFill ? hpFill.GetComponent<Image>() : null; hc.label = hpText ? hpText.GetComponent<TMP_Text>() : null; hc.lowThreshold = 0.25f;
            }

            var stBg = Find("Stamina_barBG");
            ThemeImage(stBg, ColorToken.SurfaceScrim, 0.85f, SpriteSlot.BarBack);
            ThemeImage(Find("Stamina_bar_Fill"), ColorToken.BrandLime, 1f, SpriteSlot.BarFill, keepColor: true, autoType: false);
            var st = stBg ? stBg.GetComponent("UIStaminaBar") : null;
            ColorField(st, "normalColor", ColorToken.BrandLime);   // Craig: lime (gray when exhausted stays)
            ColorField(st, "depletedColor", ColorToken.StateDisabled);

            // ---- weapon / ammo ----
            ThemeImage(Find("WeaponInfo"), ColorToken.SurfaceScrim, 0.85f, SpriteSlot.Card);
            ThemeText(Find("Ammo Count (TMP)"), TypeRole.Utility, 1.875f);

            // ---- coins ----
            var menu = Find("Menu");
            ThemeImage(menu ? menu.Find("Bg") : null, ColorToken.SurfaceScrim, 0.85f, SpriteSlot.Card);
            ThemeText(Find("CoinTXT"), TypeRole.Utility, 1.94f);

            // ---- objectives ----
            var goals = Find("Goal Panel");
            ThemeImage(goals, ColorToken.SurfaceScrim, 0.8f, SpriteSlot.Card);
            if (goals)
            {
                // A little more room so the labels don't auto-shrink below the floor: panel 200 -> 250 wide, rows 40 -> 50 tall.
                // Only grow axes that aren't stretched to the parent (stretched children follow their parent automatically).
                void Resize(Transform t, float dw, float dh)
                {
                    var rt = t as RectTransform; if (!rt) return;
                    bool stretchX = !Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x), stretchY = !Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y);
                    if (stretchX && stretchY) return;
                    Snap(rt); rt.sizeDelta += new Vector2(stretchX ? 0f : dw, stretchY ? 0f : dh);
                }
                Resize(goals, 50f, 0f);
                foreach (Transform row in goals)
                {
                    Resize(row, 50f, 10f);
                    var tg = row.GetComponentInChildren<Toggle>(true);
                    if (tg) { Resize(tg.transform, 50f, 10f); var lbl = tg.GetComponentInChildren<TMP_Text>(true); if (lbl) Resize(lbl.transform, 50f, 10f); }
                }
            }
            if (goals)
                foreach (var toggle in goals.GetComponentsInChildren<Toggle>(true))
                {
                    var bg = toggle.transform.Find("Background");
                    ThemeImage(bg, ColorToken.TextSecondary, 0.55f, SpriteSlot.BarFill);   // flat square (the old sprite was pre-colored yellow)
                    ThemeImage(bg ? bg.Find("Checkmark") : null, ColorToken.BrandLime, 1f, SpriteSlot.None);
                    // 1.16: keeps objectives >= 21 px even at 4:3, where this 1280x720 canvas shrinks (24 px at 16:9)
                    ThemeText(toggle.GetComponentInChildren<TMP_Text>(true) ? toggle.GetComponentInChildren<TMP_Text>(true).transform : null, TypeRole.Caption, 1.16f, color: ColorToken.TextPrimary);
                }

            // ---- kill counter ----
            var kill = Find("Enemy Kill");
            ThemeImage(kill, ColorToken.SurfaceScrim, 0.8f, SpriteSlot.Card);
            ThemeText(kill ? kill.Find("Title") : null, TypeRole.Caption, 1f);
            ThemeText(kill ? kill.Find("kill info") : null, TypeRole.Utility, 1.2f, color: ColorToken.BrandLime);

            // ---- minimap ----
            var mask = Find("MiniMapMaskImg");
            if (mask)
            {
                var ring = new GameObject("Minimap Ring (#UI)", typeof(RectTransform), typeof(Image));
                ring.transform.SetParent(mask.parent, false);
                ring.transform.SetSiblingIndex(mask.GetSiblingIndex()); // behind the map
                var rr = (RectTransform)ring.transform; var mr = (RectTransform)mask;
                rr.anchorMin = mr.anchorMin; rr.anchorMax = mr.anchorMax; rr.pivot = mr.pivot;
                rr.anchoredPosition = mr.anchoredPosition; rr.sizeDelta = mr.sizeDelta + new Vector2(10f, 10f);
                var ri = ring.GetComponent<Image>(); ri.sprite = mask.GetComponent<Image>().sprite; ri.raycastTarget = false;
                var rt = ring.AddComponent<ThemedImage>(); rt.color = ColorToken.BrandForest; rt.sprite = SpriteSlot.None; rt.Apply();
                ThemeImage(mask.Find("MiniMap Parent/Player"), ColorToken.BrandLime, 1f, SpriteSlot.None);
                // enemy / boss blip colors: themed in PimlrMinimapEnemyBlips.Start (the component is added at runtime)
            }
            else log.Add("missing minimap");

            EditorUtility.SetDirty(snap);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return $"Global UI restyled ({PrefabPath})" + (log.Count > 0 ? "\n  notes: " + string.Join("; ", log) : "");
    }

    [MenuItem("PIMLR/UI/Slice 4a: Revert HUD Restyle")]
    public static string Revert()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        bool changed = false;
        try
        {
            var snap = root.GetComponent<PimlrUISnapshot>();
            if (snap && snap.entries.Any(e => e.slice == Slice))
            {
                foreach (var e in snap.entries.Where(e => e.slice == Slice && e.target))
                {
                    foreach (var th in e.target.GetComponents<ThemedBehaviour>()) Object.DestroyImmediate(th, true);
                    PimlrUISnapshot.Restore(e);
                }
                snap.entries.RemoveAll(e => e.slice == Slice);
                if (snap.entries.Count == 0) Object.DestroyImmediate(snap, true);
                changed = true;
            }
            var fields = root.GetComponent<PimlrThemedColorFields>();
            if (fields) { Object.DestroyImmediate(fields, true); changed = true; }
            foreach (var hc in root.GetComponentsInChildren<PimlrHealthBarColor>(true)) { Object.DestroyImmediate(hc, true); changed = true; }
            // Only 4a's own object: later slices' "(#UI)" objects (4b player / carousel, 5 pause hub) have their own reverts.
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t && t.name == "Minimap Ring (#UI)").ToList())
            { Object.DestroyImmediate(t.gameObject, true); changed = true; }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return changed ? "reverted" : "nothing to revert";
    }
}
