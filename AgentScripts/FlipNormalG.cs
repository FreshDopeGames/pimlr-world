using UnityEditor; using UnityEngine;
public static class FlipNormalG { public static string Main(bool flip) {
  var p="Assets/_Game/Characters/Thurz_v4/Textures/Thurz_v4_Normal.png"; var ti=(TextureImporter)AssetImporter.GetAtPath(p);
  ti.flipGreenChannel=flip; ti.SaveAndReimport();
  var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Characters/Thurz_v4/M_Thurz_v4.mat"); m.SetFloat("_BumpScale",1f); m.SetFloat("_Smoothness",1f); EditorUtility.SetDirty(m);
  return "flipG="+ti.flipGreenChannel; } }
