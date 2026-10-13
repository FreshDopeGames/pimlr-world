using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PIMLR: one-time cleanup for the music player after switching to the single ActiveSystem panel.
///  - Copies the album art you put on the Living Room's redesigned "Idle" panel (its Shop button) onto
///    ActiveSystem/Shop in Global UI, so both the hub and YannicksWorld show it (the variant inherits it).
///  - Switches the Idle panels off in both prefabs (MusicSystem now shows ActiveSystem for playing AND paused).
/// Menu: PIMLR / UI / Music Player - Use ActiveSystem Panel
/// </summary>
public static class PimlrMusicPlayerPanelFixer
{
    const string GlobalUI = "Assets/_Game/Resources/Global UI.prefab";
    const string LivingRoomUI = "Assets/_Game/Resources/Living Room UI Variant.prefab";

    [MenuItem("PIMLR/UI/Music Player - Use ActiveSystem Panel")]
    static void Fix()
    {
        // 1) Find the album art: a Shop sprite on any Idle panel that differs from that player's ActiveSystem Shop.
        Sprite albumArt = null;
        foreach (var path in new[] { LivingRoomUI, GlobalUI })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var ms in root.GetComponentsInChildren<MusicSystem>(true))
                {
                    var idleShop = Shop(ms.idleSystem);
                    var activeShop = Shop(ms.activeSystem);
                    if (idleShop && idleShop.sprite && (!activeShop || activeShop.sprite != idleShop.sprite))
                    { albumArt = idleShop.sprite; break; }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            if (albumArt) break;
        }

        int changed = 0;
        // 2) Base prefab first (the variant inherits), then the variant.
        foreach (var path in new[] { GlobalUI, LivingRoomUI })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var ms in root.GetComponentsInChildren<MusicSystem>(true))
                {
                    if (path == GlobalUI && albumArt)
                    {
                        var activeShop = Shop(ms.activeSystem);
                        if (activeShop && activeShop.sprite != albumArt) { activeShop.sprite = albumArt; activeShop.preserveAspect = true; changed++; }
                    }
                    if (ms.activeSystem && ms.idleSystem && ms.idleSystem.activeSelf) { ms.idleSystem.SetActive(false); changed++; }
                    if (ms.activeSystem2 && ms.idleSystem2 && ms.idleSystem2.activeSelf) { ms.idleSystem2.SetActive(false); changed++; }
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        Debug.Log($"PIMLR Music Player: {changed} change(s). Album art: {(albumArt ? albumArt.name : "none found")}. " +
                  "MusicSystem now always shows ActiveSystem; the Idle copies can be deleted whenever you like.");
        EditorUtility.DisplayDialog("Music Player", $"Done ({changed} change(s)).\nAlbum art: {(albumArt ? albumArt.name : "none found")}", "OK");
    }

    static Image Shop(GameObject panel)
    {
        if (!panel) return null;
        var t = panel.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Shop");
        return t ? t.GetComponent<Image>() : null;
    }
}
