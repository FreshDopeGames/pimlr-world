// #UI Slice 5: Gameplay Pause hub. Adds "PIMLR Pause Hub (#UI)" (PimlrPauseHub) to Global UI > Pause Screen >
// Overlay Panel and switches off the old "Pause BG" layout (its objects stay in the prefab; the snapshot turns them back
// on), and switches off the gameplay HUD's Controls panel. Pausing itself is unchanged: JUTPS InventoryUIManager still opens / closes the Overlay Panel.
// Menus: PIMLR > UI > Slice 5: Build Pause Hub / Slice 5: Revert Pause Hub

using System.Linq;
using PIMLR.UI;
using UnityEditor;
using UnityEngine;

public static class PimlrPauseSlice5Builder
{
    const string PrefabPath = "Assets/_Game/Resources/Global UI.prefab";
    const string Slice = "slice5";
    const string HubName = "PIMLR Pause Hub (#UI)";

    [MenuItem("PIMLR/UI/Slice 5: Build Pause Hub")]
    public static string Build()
    {
        Revert();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            var overlay = all.FirstOrDefault(t => t.name == "Overlay Panel" && t.parent && t.parent.name == "Pause Screen");
            if (!overlay) return "couldn't find Pause Screen/Overlay Panel - nothing built";
            var snap = root.GetComponent<PimlrUISnapshot>(); if (!snap) snap = root.AddComponent<PimlrUISnapshot>();

            var oldBg = overlay.Find("Pause BG");
            if (oldBg && oldBg.gameObject.activeSelf)
            {
                var e = PimlrUISnapshot.Capture(Slice, overlay);
                oldBg.gameObject.SetActive(false);
                e.deactivated.Add(oldBg.gameObject);
                snap.entries.Add(e);
            }

            // Craig (Nov 27): the gameplay HUD's Controls panel goes - the Pause screen's Controls sub-menu and the tutorial
            // cover it. PimlrControlsInfo leaves a HUD copy that's off in the prefab off.
            var hud = all.FirstOrDefault(t => t.name == "JUTPS Interface");
            var hudControls = hud ? hud.Cast<Transform>().FirstOrDefault(t => t.name == "Controls Info") : null;
            if (hudControls && hudControls.gameObject.activeSelf)
            {
                var e = PimlrUISnapshot.Capture(Slice, hud);
                hudControls.gameObject.SetActive(false);
                e.deactivated.Add(hudControls.gameObject);
                snap.entries.Add(e);
            }

            var go = new GameObject(HubName, typeof(RectTransform));
            go.layer = overlay.gameObject.layer;
            go.transform.SetParent(overlay, false);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<PimlrPauseHub>();

            EditorUtility.SetDirty(snap);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return "Slice 5 Pause hub built";
    }

    [MenuItem("PIMLR/UI/Slice 5: Revert Pause Hub")]
    public static string Revert()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        bool changed = false;
        try
        {
            var snap = root.GetComponent<PimlrUISnapshot>();
            if (snap)
            {
                foreach (var e in snap.entries.Where(e => e.slice == Slice).Reverse())
                {
                    if (e.target) PimlrUISnapshot.Restore(e);
                    foreach (var g in e.deactivated) if (g) g.SetActive(true);
                    changed = true;
                }
                snap.entries.RemoveAll(e => e.slice == Slice);
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t && t.name == HubName).ToList())
            { Object.DestroyImmediate(t.gameObject, true); changed = true; }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return changed ? "Slice 5 reverted" : "nothing to revert";
    }
}
