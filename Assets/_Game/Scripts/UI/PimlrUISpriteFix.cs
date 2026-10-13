using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PIMLR: turns 2D SpriteRenderers that were dropped into a UI Canvas into proper UI Images.
/// A SpriteRenderer isn't UI - it draws in the 3D world at the object's world position. On a "Screen Space -
/// Camera" canvas (the Living Room) that happens to land in front of the camera, so it shows; on the gameplay
/// HUD's "Screen Space - Overlay" canvas its world position is hundreds of units off in the level, so it never
/// shows. That's the PIMLR Coin icon in JUTPS Interface/User Interface/UI/Menu/Bg.
/// Runs on every scene load (PimlrInputRuntime). The lasting fix is the same in the prefab: remove the Sprite
/// Renderer from "PIMLR Coin" and add an Image with the coin sprite.
/// </summary>
public static class PimlrUISpriteFix
{
    public static void ConvertAll()
    {
        int n = 0;
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(sr.transform is RectTransform rt)) continue;              // only things laid out as UI
            if (sr.GetComponentInParent<Canvas>(true) == null) continue;
            if (sr.GetComponent<Graphic>() != null) continue;              // already has a UI graphic
            if (sr.sprite == null) continue;

            // Keep the size it had: sprite size (units) x its local scale, expressed in the parent's UI units.
            Vector3 s = rt.localScale;
            Vector2 size = new Vector2(sr.sprite.bounds.size.x * Mathf.Abs(s.x), sr.sprite.bounds.size.y * Mathf.Abs(s.y));

            var go = sr.gameObject;
            var sprite = sr.sprite;
            var color = sr.color;
            Object.Destroy(sr);                                            // a GameObject can't keep both renderers usefully

            if (!go.GetComponent<CanvasRenderer>()) go.AddComponent<CanvasRenderer>();
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            rt.localScale = Vector3.one;
            rt.sizeDelta = size;
            n++;
        }
        if (n > 0) Debug.Log($"PIMLR: converted {n} SpriteRenderer(s) inside UI canvases to UI Images (e.g. the HUD PIMLR Coin icon).");
    }
}
