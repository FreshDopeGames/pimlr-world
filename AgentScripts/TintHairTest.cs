using UnityEngine;
public static class TintHairTest { public static string Main(float r, float g, float b) {
  var n=0; foreach (var smr in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
    if (smr.name=="Renderer_Hair") { changeColor.TintRenderer(smr,new Color(r,g,b)); n++; }
  return "tinted "+n; } }
