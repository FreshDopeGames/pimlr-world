using UnityEditor; using UnityEngine;
public static class MatProbe { public static string Main(float smooth, float bump) {
  var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Characters/Thurz_v4/M_Thurz_v4.mat");
  m.SetFloat("_Smoothness",smooth); m.SetFloat("_BumpScale",bump); EditorUtility.SetDirty(m);
  var ti=(TextureImporter)AssetImporter.GetAtPath("Assets/_Game/Characters/Thurz_v4/Textures/Thurz_v4_Normal.png");
  return $"smooth={smooth} bump={bump} normal type={ti.textureType} flipG={ti.flipGreenChannel}"; } }
