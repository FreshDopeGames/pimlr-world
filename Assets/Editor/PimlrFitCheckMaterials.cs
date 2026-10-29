using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// PIMLR: puts the Fit Check "colourable" materials (the all-white Wolf3D_Hair / Outfit_Top / Outfit_Bottom /
/// Outfit_Footwear) back on the player's clothing renderers in every open scene.
/// changeColor (and Customization_Applier) tint renderers[i].materials[0], so each renderer must use the plain
/// white material listed at the same index in colourableMaterials - a textured material ignores most of the tint.
/// Run it after re-skinning the player (Swap RPM Avatar does this automatically for scene targets).
/// Menu: PIMLR / Characters / Restore Fit Check Materials
/// </summary>
public static class PimlrFitCheckMaterials
{
    [MenuItem("PIMLR/Characters/Restore Fit Check Materials")]
    public static void RestoreMenu()
    {
        int fixedCount = Restore(true);
        EditorUtility.DisplayDialog("Restore Fit Check Materials",
            fixedCount > 0 ? $"Set {fixedCount} renderer(s) back to their white Fit Check material. Save the scene(s)."
                           : "Every customizable renderer already uses its Fit Check material.", "OK");
    }

    /// <returns>Number of renderers changed.</returns>
    public static int Restore(bool log)
    {
        int changed = 0;
        foreach (var cc in Object.FindObjectsOfType<changeColor>(true))
        {
            if (cc.renderers == null || cc.colourableMaterials == null) continue;
            int n = Mathf.Min(cc.renderers.Length, cc.colourableMaterials.Length);
            for (int i = 0; i < n; i++)
            {
                Renderer r = cc.renderers[i];
                Material white = cc.colourableMaterials[i];
                if (r == null || white == null) continue;
                Material[] mats = r.sharedMaterials;
                if (mats.Length > 0 && mats[0] == white) continue;
                if (mats.Length == 0) mats = new Material[1];
                Undo.RecordObject(r, "Restore Fit Check Materials");
                mats[0] = white;
                r.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                EditorSceneManager.MarkSceneDirty(r.gameObject.scene);
                if (log) Debug.Log($"Fit Check: {r.transform.parent?.name}/{r.name} -> {white.name}", r);
                changed++;
            }
        }
        return changed;
    }
}
