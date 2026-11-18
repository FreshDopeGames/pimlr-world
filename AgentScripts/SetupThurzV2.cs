using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SetupThurzV2
{
    const string Root = "Assets/_Game/Characters/Thurz_v2";
    public static string Main()
    {
        var log = new System.Text.StringBuilder();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        void Tex(string file, bool srgb, bool normal, int max)
        {
            var p = $"{Root}/Textures/{file}";
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) { log.AppendLine("MISSING " + p); return; }
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = srgb; ti.maxTextureSize = max; ti.mipmapEnabled = true;
            ti.alphaSource = file.Contains("MetallicSmoothness") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.SaveAndReimport(); log.AppendLine("tex ok " + file);
        }
        Tex("Thurz_v2_BaseColor.png", true, false, 4096);
        Tex("Thurz_v2_TintMask.png", false, false, 4096);
        Tex("Thurz_v2_Normal.png", false, true, 4096);
        Tex("Thurz_v2_MetallicSmoothness.png", false, false, 2048);

        var shader = Shader.Find("PIMLR/TintMaskLit");
        if (shader == null) return log.AppendLine("SHADER NOT FOUND (compile error?)").ToString();
        var matPath = $"{Root}/M_Thurz_v2.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
        mat.shader = shader;
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/Thurz_v2_BaseColor.png"));
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/Thurz_v2_BaseColor.png"));
        mat.SetTexture("_TintMask", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/Thurz_v2_TintMask.png"));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/Thurz_v2_Normal.png"));
        mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/Thurz_v2_MetallicSmoothness.png"));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.85f); mat.SetFloat("_BumpScale", 1f);
        mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(mat);
        log.AppendLine("material ok " + matPath);

        var fbx = $"{Root}/Thurz_v2.fbx";
        var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
        if (mi == null) return log.AppendLine("FBX importer missing").ToString();
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Thurz_v2"), mat);
        mi.importBlendShapes = true; mi.importTangents = ModelImporterTangents.Import;
        mi.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
        log.AppendLine($"avatar: {(avatar ? avatar.name : "none")} valid={(avatar && avatar.isValid)} human={(avatar && avatar.isHuman)}");
        if (avatar && avatar.isHuman)
        {
            var hd = avatar.humanDescription;
            log.AppendLine("mapped bones: " + hd.human.Length + " -> " + string.Join(",", hd.human.Take(60).Select(h => h.humanName + "=" + h.boneName)));
        }
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            log.AppendLine($"renderer {r.name}: mat={(r.sharedMaterial ? r.sharedMaterial.name : "null")} bones={r.bones.Length} verts={r.sharedMesh.vertexCount}");
        var b = go.GetComponentsInChildren<Renderer>().Select(x => x.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
        log.AppendLine("bounds size " + b.size.ToString("F3") + " min " + b.min.ToString("F3"));
        AssetDatabase.SaveAssets();
        return log.ToString();
    }
}
