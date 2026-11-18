using System.Linq; using UnityEditor; using UnityEngine;
public static class SetupThurzV3 {
  const string Root = "Assets/_Game/Characters/Thurz_v3";
  public static string Main() {
    var log = new System.Text.StringBuilder();
    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    void Tex(string f, bool srgb, bool normal, int max) {
      var ti = AssetImporter.GetAtPath($"{Root}/Textures/{f}") as TextureImporter; if (!ti) { log.AppendLine("MISSING " + f); return; }
      ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.sRGBTexture = srgb; ti.maxTextureSize = max;
      ti.alphaSource = f.Contains("MetallicSmoothness") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None; ti.SaveAndReimport(); }
    Material Mat(string part) {
      foreach (var (f, s, n, m) in new[] { ("BaseColor", true, false, 4096), ("TintMask", false, false, 4096), ("Normal", false, true, 4096), ("MetallicSmoothness", false, false, 2048) })
        Tex($"Thurz_v3_{part}_{f}.png", s, n, m);
      var path = $"{Root}/M_Thurz_v3_{part}.mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
      var sh = Shader.Find("PIMLR/TintMaskLit"); if (!mat) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, path); }
      mat.shader = sh;
      Texture2D T(string f) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/Thurz_v3_{part}_{f}.png");
      mat.SetTexture("_BaseMap", T("BaseColor")); mat.SetTexture("_MainTex", T("BaseColor")); mat.SetTexture("_TintMask", T("TintMask"));
      mat.SetTexture("_BumpMap", T("Normal")); mat.SetTexture("_MetallicGlossMap", T("MetallicSmoothness"));
      mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Smoothness", 0.85f); mat.SetFloat("_BumpScale", 1f);
      mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
      mat.SetTexture("_OcclusionMap", null); mat.DisableKeyword("_OCCLUSIONMAP"); EditorUtility.SetDirty(mat); // AO bake unusable (too dark) - left unwired
      log.AppendLine("material " + path); return mat;
    }
    var body = Mat("Body"); var head = Mat("Head");
    var fbx = $"{Root}/Thurz_v3.fbx"; var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
    mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; mi.importAnimation = false;
    mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; mi.importTangents = ModelImporterTangents.CalculateMikk;
    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Thurz_v3_Body"), body);
    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Thurz_v3_Head"), head);
    mi.SaveAndReimport();
    var av = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
    log.AppendLine($"avatar valid={(av && av.isValid)} human={(av && av.isHuman)} bones={(av && av.isHuman ? av.humanDescription.human.Length : 0)}");
    foreach (var r in AssetDatabase.LoadAssetAtPath<GameObject>(fbx).GetComponentsInChildren<SkinnedMeshRenderer>(true))
      log.AppendLine($"{r.name}: {r.sharedMaterial?.name} verts={r.sharedMesh.vertexCount} tris={r.sharedMesh.triangles.Length / 3}");
    AssetDatabase.SaveAssets(); return log.ToString();
  }
}
