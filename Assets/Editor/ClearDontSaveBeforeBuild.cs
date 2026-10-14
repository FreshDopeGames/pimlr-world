using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class ClearDontSaveBeforeBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;   // run first

    public void OnPreprocessBuild(BuildReport report)
    {
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture t:Sprite t:Material t:Mesh", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (obj != null && (obj.hideFlags & HideFlags.DontSave) != 0)
                {
                    obj.hideFlags = HideFlags.None;
                    count++;
                }
            }
        }
        if (count > 0) Debug.Log($"[PIMLR] Cleared DontSave on {count} project assets before build.");
    }
}