// #UI Slice 4b: gameplay HUD layout from Craig's Nov 27 concept, layered on top of Slice 4a (the restyle).
//   music player   new PIMLR Music Player (top-left) with the Boost slide-down panel; the old top-centre "Music"
//                  panels stay (MusicSystem drives them) but are hidden: CanvasGroup on "Music" + their children off
//   objective      Goal Panel moves under the coin card, shows one objective (PimlrHudObjective), green diamond marker
//   weapon         new PIMLR Weapon Carousel (bottom-left); the old JUTPS "Weapons" card is switched off
//   vitals         stay bottom-centre; stamina warm yellow (cyan in Settings > Color-blind mode), health icon green
//   minimap        ring bright green (HealthGreen)
//   layout         PimlrHudLayout: landscape / portrait / touch placement
// Revert: undoes the 4b snapshot entries and additions, then rebuilds Slice 4a, which resets every themed field 4b
// changed on 4a's components (objective type, ring color, stamina tokens).
// Menus: PIMLR > UI > Slice 4b: Build HUD Layout / Slice 4b: Revert HUD Layout

using System.Collections.Generic;
using System.Linq;
using PIMLR.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PimlrHudSlice4bBuilder
{
    const string PrefabPath = "Assets/_Game/Resources/Global UI.prefab";
    const string Slice = "slice4b";
    const string SliceAdded = "slice4b-added"; // components this slice added (destroyed on revert)

    [MenuItem("PIMLR/UI/Slice 4b: Build HUD Layout")]
    public static string Build()
    {
        RevertOnly();
        if (!Has4a()) PimlrHudSlice4Builder.Build();

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var log = new List<string>();
        try
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            Transform Find(string name) => all.FirstOrDefault(t => t.name == name);
            var snap = root.GetComponent<PimlrUISnapshot>(); if (!snap) snap = root.AddComponent<PimlrUISnapshot>();
            void Snap(Component c) { if (c) snap.entries.Add(PimlrUISnapshot.Capture(Slice, c)); }
            void Added(Component c) { if (c) snap.entries.Add(new PimlrUISnapshot.Entry { slice = SliceAdded, target = c }); }
            void TurnOff(Transform owner, IEnumerable<GameObject> gos)
            {
                var e = PimlrUISnapshot.Capture(Slice, owner); // captures the owner's rect too, so Restore puts it back as-is
                foreach (var g in gos) if (g && g.activeSelf) { g.SetActive(false); e.deactivated.Add(g); }
                if (e.deactivated.Count > 0) snap.entries.Add(e);
            }

            var ui = Find("UI");
            var music = Find("Music");
            if (!ui || !music) return "Global UI: couldn't find UI / Music - nothing built";

            // ---- old music panels: hidden, still driven by MusicSystem ----
            var active = music.Find("ActiveSystem");
            var idle = music.Find("IdlePlayer");
            if (music.GetComponent<CanvasGroup>()) log.Add("Music already had a CanvasGroup (left as is)");
            else
            {
                var cg = music.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
                Added(cg);
            }
            foreach (var p in new[] { active, idle })
                if (p) TurnOff(p, p.Cast<Transform>().Select(c => c.gameObject));

            Sprite S(Transform panel, string child) { var t = panel ? panel.Find(child) : null; var i = t ? t.GetComponent<Image>() : null; return i ? i.sprite : null; }

            // ---- new music player ----
            var pgo = new GameObject("PIMLR Music Player (#UI)", typeof(RectTransform));
            pgo.layer = ui.gameObject.layer;
            pgo.transform.SetParent(ui, false);
            pgo.transform.SetSiblingIndex(music.GetSiblingIndex() + 1);
            var player = pgo.AddComponent<PimlrHudMusicPlayer>();
            player.shuffleSprite = S(active, "shuffle");
            player.previousSprite = S(active, "backward");
            player.nextSprite = S(active, "forward");
            player.repeatSprite = S(active, "replay");
            player.pauseSprite = S(active, "play/pause");
            player.playSprite = S(idle, "play/pause");
            PimlrHudKit.Place((RectTransform)pgo.transform, new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(440f, 112f));
            player.Build();

            // ---- weapon carousel (replaces the JUTPS Weapons card) ----
            var weapons = Find("Weapons");
            if (weapons) TurnOff(weapons.parent, new[] { weapons.gameObject }); else log.Add("missing Weapons");
            var cgo = new GameObject("PIMLR Weapon Carousel (#UI)", typeof(RectTransform));
            cgo.layer = ui.gameObject.layer;
            cgo.transform.SetParent(ui, false);
            var carousel = cgo.AddComponent<PimlrWeaponCarousel>();
            PimlrHudKit.Place((RectTransform)cgo.transform, Vector2.zero, new Vector2(24f, 24f), new Vector2(300f, 108f));
            carousel.Build();

            // ---- one objective, under the coin card ----
            var goals = Find("Goal Panel") as RectTransform;
            if (goals)
            {
                Snap(goals);
                goals.anchorMin = goals.anchorMax = goals.pivot = new Vector2(1f, 1f);
                goals.anchoredPosition = new Vector2(-11f, -112f);
                goals.sizeDelta = new Vector2(293f, goals.sizeDelta.y);
                foreach (var toggle in goals.GetComponentsInChildren<Toggle>(true))
                {
                    var row = toggle.transform.parent as RectTransform;
                    if (row && row != goals && Mathf.Approximately(row.anchorMin.x, row.anchorMax.x)) { Snap(row); row.sizeDelta = new Vector2(283f, row.sizeDelta.y); }
                    var label = toggle.GetComponentInChildren<TMPro.TMP_Text>(true);
                    var tt = label ? label.GetComponent<ThemedText>() : null;
                    if (tt) { tt.role = TypeRole.Heading; tt.sizeScale = 26f / 36f; tt.overrideColor = true; tt.color = ColorToken.TextPrimary; tt.Apply(); }
                    var bg = toggle.transform.Find("Background") as RectTransform;
                    if (bg)
                    {
                        Snap(bg);
                        bg.sizeDelta = new Vector2(16f, 16f);
                        bg.localRotation = Quaternion.Euler(0f, 0f, 45f); // diamond (reset on revert)
                        var ti = bg.GetComponent<ThemedImage>();
                        if (ti) { ti.color = ColorToken.HealthGreen; ti.alpha = 1f; ti.sprite = SpriteSlot.ObjectiveMarker; ti.Apply(); }
                    }
                }
                Added(goals.gameObject.AddComponent<PimlrHudObjective>());
            }
            else log.Add("missing Goal Panel");

            // ---- kill counter: readable at 4:3, one line (PimlrHudLayout places it under the objective) ----
            var kill = Find("Enemy Kill");
            if (kill)
            {
                var kTitle = kill.Find("Title") as RectTransform;
                var kInfo = kill.Find("kill info") as RectTransform;
                if (kTitle) { Snap(kTitle); kTitle.anchoredPosition = new Vector2(10f, kTitle.anchoredPosition.y); kTitle.sizeDelta = new Vector2(205f, kTitle.sizeDelta.y); var tt = kTitle.GetComponent<ThemedText>(); if (tt) { tt.sizeScale = 1.16f; tt.Apply(); } }
                if (kInfo) { Snap(kInfo); kInfo.anchoredPosition = new Vector2(215f, kInfo.anchoredPosition.y); kInfo.sizeDelta = new Vector2(70f, kInfo.sizeDelta.y); }
            }

            // ---- minimap ring: bright green ----
            var ring = Find("Minimap Ring (#UI)");
            var ringTi = ring ? ring.GetComponent<ThemedImage>() : null;
            if (ringTi) { ringTi.color = ColorToken.HealthGreen; ringTi.sprite = SpriteSlot.MinimapRing; ringTi.Apply(); } else log.Add("missing minimap ring (run 4a)");

            // ---- vitals: stamina yellow / color-blind cyan, icons ----
            var fields = root.GetComponent<PimlrThemedColorFields>();
            var stBg = Find("Stamina_barBG");
            var st = stBg ? stBg.GetComponent<UIStaminaBar>() : null;
            if (fields && st)
            {
                for (int i = 0; i < fields.entries.Count; i++)
                    if (fields.entries[i].target == st && fields.entries[i].field == "normalColor")
                    { var e = fields.entries[i]; e.token = ColorToken.StaminaYellow; fields.entries[i] = e; }
                fields.entries.Add(new PimlrThemedColorFields.Entry { target = st, field = "colorBlindColor", token = ColorToken.StaminaColorblind, alpha = 1f });
                fields.Apply();
                var so = new SerializedObject(st);
                var icon = Find("StaminaIcon");
                so.FindProperty("icon").objectReferenceValue = icon ? icon.GetComponent<Image>() : null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else log.Add("missing stamina bar / themed color fields");
            var hpIcon = Find("HealthIcon");
            var hpImg = hpIcon ? hpIcon.GetComponent<Image>() : null;
            if (hpImg)
            {
                Snap(hpImg);
                var ti = hpIcon.gameObject.AddComponent<ThemedImage>();
                ti.color = ColorToken.HealthGreen; ti.sprite = SpriteSlot.None; ti.Apply();
                Added(ti);
            }

            // ---- responsive layout ----
            var layoutHost = Find("JUTPS Interface");
            var layout = layoutHost.gameObject.AddComponent<PimlrHudLayout>();
            Added(layout);
            layout.player = player;
            layout.carousel = carousel;
            layout.coins = Find("Menu") as RectTransform;
            layout.objective = goals;
            layout.minimapRing = ring as RectTransform;
            layout.minimap = Find("MiniMapMaskImg") as RectTransform;
            layout.killCount = Find("Enemy Kill") as RectTransform;
            var menuBg = layout.coins ? layout.coins.Find("Bg") : null;
            layout.coinButton = menuBg ? menuBg.Find("Home") as RectTransform : null;
            if (layout.coinButton) { Snap(layout.coinButton); layout.coinButtonSize = layout.coinButton.sizeDelta; }
            layout.coinsLandscape = PimlrHudLayout.RectState.Of(layout.coins);
            layout.objectiveLandscape = PimlrHudLayout.RectState.Of(layout.objective);
            layout.ringLandscape = PimlrHudLayout.RectState.Of(layout.minimapRing);
            layout.minimapLandscape = PimlrHudLayout.RectState.Of(layout.minimap);
            layout.killLandscape = PimlrHudLayout.RectState.Of(layout.killCount);
            // These rects move at runtime (portrait), so snapshot the ones 4a didn't already own.
            foreach (var r in new[] { layout.coins, layout.minimapRing, layout.minimap, layout.killCount }) Snap(r);

            EditorUtility.SetDirty(snap);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return $"Slice 4b HUD layout built ({PrefabPath})" + (log.Count > 0 ? "\n  notes: " + string.Join("; ", log) : "");
    }

    [MenuItem("PIMLR/UI/Slice 4b: Revert HUD Layout")]
    public static string Revert()
    {
        bool changed = RevertOnly();
        PimlrHudSlice4Builder.Build(); // back to exactly the Slice 4a look
        return changed ? "Slice 4b reverted (Slice 4a rebuilt)" : "nothing to revert (Slice 4a rebuilt)";
    }

    static bool Has4a()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try { var s = root.GetComponent<PimlrUISnapshot>(); return s && s.entries.Any(e => e.slice == "slice4a"); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>Undo 4b's own changes (not 4a's). Returns true if anything was there.</summary>
    static bool RevertOnly()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        bool changed = false;
        try
        {
            var snap = root.GetComponent<PimlrUISnapshot>();
            if (snap)
            {
                // Restore in reverse so a rect snapped twice ends at its first (pre-4b) value.
                foreach (var e in snap.entries.Where(e => e.slice == Slice).Reverse())
                {
                    if (e.target) PimlrUISnapshot.Restore(e);
                    if (e.target is RectTransform rt && rt.name == "Background") rt.localRotation = Quaternion.identity;
                    foreach (var g in e.deactivated) if (g) g.SetActive(true);
                    changed = true;
                }
                foreach (var e in snap.entries.Where(e => e.slice == SliceAdded))
                {
                    if (e.target) Object.DestroyImmediate(e.target, true);
                    changed = true;
                }
                snap.entries.RemoveAll(e => e.slice == Slice || e.slice == SliceAdded);
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t && (t.name == "PIMLR Music Player (#UI)" || t.name == "PIMLR Weapon Carousel (#UI)")).ToList())
            { Object.DestroyImmediate(t.gameObject, true); changed = true; }
            foreach (var st in root.GetComponentsInChildren<UIStaminaBar>(true))
            {
                var so = new SerializedObject(st);
                var p = so.FindProperty("icon");
                if (p != null && p.objectReferenceValue) { p.objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo(); changed = true; }
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return changed;
    }
}
