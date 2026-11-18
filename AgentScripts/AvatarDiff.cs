using System.Linq; using UnityEditor; using UnityEngine;
public static class AvatarDiff { public static string Main() {
  string[] H(string p){ var a=AssetDatabase.LoadAllAssetsAtPath(p).OfType<Avatar>().First(); return a.humanDescription.human.Select(h=>h.humanName+"="+h.boneName).ToArray(); }
  var a3=H("Assets/_Game/Characters/Thurz_v3/Thurz_v3.fbx"); var a4=H("Assets/_Game/Characters/Thurz_v4/Thurz_v4.fbx");
  return "only v3: "+string.Join(", ",a3.Except(a4))+"\nonly v4: "+string.Join(", ",a4.Except(a3)); } }
