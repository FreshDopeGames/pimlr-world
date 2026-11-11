using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// PIMLR: converts glTFast "Shader Graphs/glTF-*" materials to plain URP/Lit so the player no longer needs glTFast
/// (its shader) or the Ready Player Me .glb files (where the textures live as sub-assets).
///
/// For each glTF material it creates  Assets/_Game/Materials/Player URP/&lt;name&gt;_&lt;id&gt;.mat  and bakes its textures to PNG:
///   baseColorTexture/Factor   -> _BaseMap/_BaseColor   (_BaseColor is URP's main color, so Fit Check tinting keeps working)
///   normalTexture/_scale      -> _BumpMap/_BumpScale   (DXT5nm/BC5 swizzle undone)
///   metallicRoughnessTexture  -> _MetallicGlossMap     (glTF B=metal, G=rough  ->  URP R=metal, A=smoothness)
///   occlusionTexture          -> _OcclusionMap         (glTF R -> URP G)
///   emissive*, _Cull, alpha clip / transparent surface carried over.
/// Then it points every renderer in the open scene(s), every changeColor.colourableMaterials entry and the
/// Thurz.fbx material remap at the new materials. Re-running reuses materials/PNGs already created.
/// Menu: PIMLR / Characters / Convert Player glTF Materials to URP Lit   (customizable player only)
///       PIMLR / Characters / Convert ALL glTF Materials in Open Scenes to URP Lit
/// </summary>
public static class PimlrGltfToUrpLit
{
    const string OutDir = "Assets/_Game/Materials/Player URP";
    const string TexDir = OutDir + "/Textures";
    const string ThurzFbx = "Assets/_Game/Prefabs/Players/Thurz.fbx";

    enum Kind { Color, Normal, Occlusion, MetallicGloss }

    [MenuItem("PIMLR/Characters/Convert Player glTF Materials to URP Lit")]
    static void ConvertPlayer() => Run(false);

    [MenuItem("PIMLR/Characters/Convert ALL glTF Materials in Open Scenes to URP Lit")]
    static void ConvertAll() => Run(true);

    static bool IsGltf(Material m) =>
        m != null && m.shader != null && m.shader.name.IndexOf("glTF", StringComparison.OrdinalIgnoreCase) >= 0;

    static void Run(bool allRenderers)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) { EditorUtility.DisplayDialog("glTF -> URP Lit", "Couldn't find the 'Universal Render Pipeline/Lit' shader.", "OK"); return; }

        var customizers = Object.FindObjectsOfType<changeColor>(true);

        // 1) which renderers / materials are in scope
        var scopeRenderers = new HashSet<Renderer>();
        if (allRenderers)
        {
            foreach (var r in Object.FindObjectsOfType<Renderer>(true)) scopeRenderers.Add(r);
        }
        else
        {
            foreach (var cc in customizers)
                if (cc.renderers != null)
                    foreach (var r in cc.renderers)
                    {
                        if (r == null) continue;
                        Transform character = r.transform.parent != null ? r.transform.parent : r.transform;
                        foreach (var cr in character.GetComponentsInChildren<Renderer>(true)) scopeRenderers.Add(cr);
                    }
        }

        var sources = new HashSet<Material>();
        foreach (var r in scopeRenderers)
            foreach (var m in r.sharedMaterials) if (IsGltf(m)) sources.Add(m);
        foreach (var cc in customizers)
            if (cc.colourableMaterials != null)
                foreach (var m in cc.colourableMaterials) if (IsGltf(m)) sources.Add(m);

        if (sources.Count == 0)
        {
            EditorUtility.DisplayDialog("glTF -> URP Lit", "No glTF materials found in scope. Nothing to do.", "OK");
            return;
        }

        EnsureFolder(OutDir);
        EnsureFolder(TexDir);

        // 2) convert
        var map = new Dictionary<Material, Material>();
        int made = 0, reused = 0, i = 0;
        try
        {
            foreach (var src in sources)
            {
                EditorUtility.DisplayProgressBar("glTF -> URP Lit", src.name, (float)i++ / sources.Count);
                string baseName = Safe(src.name) + "_" + ShortId(src);
                string matPath = $"{OutDir}/{baseName}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (existing != null) { map[src] = existing; reused++; continue; }

                Material dst = ConvertMaterial(src, lit, baseName);
                AssetDatabase.CreateAsset(dst, matPath);
                map[src] = dst;
                made++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.SaveAssets();

        // 3) re-point scene references
        int renderersChanged = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int k = 0; k < mats.Length; k++)
                if (mats[k] != null && map.TryGetValue(mats[k], out var n)) { mats[k] = n; changed = true; }
            if (!changed) continue;
            Undo.RecordObject(r, "glTF -> URP Lit");
            r.sharedMaterials = mats;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            EditorSceneManager.MarkSceneDirty(r.gameObject.scene);
            renderersChanged++;
        }
        foreach (var cc in customizers)
        {
            if (cc.colourableMaterials == null) continue;
            bool changed = false;
            for (int k = 0; k < cc.colourableMaterials.Length; k++)
            {
                var m = cc.colourableMaterials[k];
                if (m == null || !map.TryGetValue(m, out var n)) continue;
                if (!changed) Undo.RecordObject(cc, "glTF -> URP Lit");
                cc.colourableMaterials[k] = n;
                changed = true;
            }
            if (!changed) continue;
            EditorUtility.SetDirty(cc);
            PrefabUtility.RecordPrefabInstancePropertyModifications(cc);
            EditorSceneManager.MarkSceneDirty(cc.gameObject.scene);
        }

        // 4) keep Thurz.fbx's material remap in step
        bool fbxUpdated = false;
        if (AssetImporter.GetAtPath(ThurzFbx) is ModelImporter mi)
        {
            foreach (var kv in mi.GetExternalObjectMap())
                if (kv.Value is Material m && map.TryGetValue(m, out var n)) { mi.AddRemap(kv.Key, n); fbxUpdated = true; }
            if (fbxUpdated) mi.SaveAndReimport();
        }

        foreach (var kv in map) Debug.Log($"glTF -> URP Lit: {kv.Key.name} -> {AssetDatabase.GetAssetPath(kv.Value)}", kv.Value);
        EditorUtility.DisplayDialog("glTF -> URP Lit",
            $"Materials: {made} created, {reused} reused.\nRenderers re-pointed: {renderersChanged}." +
            (fbxUpdated ? "\nThurz.fbx remap updated." : "") +
            "\n\nSave the scene (Ctrl+S). Run this in each scene that has the player (SceneStaticEU, YannicksWorld).",
            "OK");
    }

    // ---------- prefab assets ----------

    [MenuItem("PIMLR/Characters/Convert glTF Materials in Selected Prefabs to URP Lit")]
    static void ConvertSelectedPrefabs()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) { EditorUtility.DisplayDialog("glTF -> URP Lit", "Couldn't find the 'Universal Render Pipeline/Lit' shader.", "OK"); return; }

        // Selected prefabs, or every prefab inside selected folders.
        var paths = new List<string>();
        foreach (var o in Selection.objects)
        {
            string p = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(p)) continue;
            if (AssetDatabase.IsValidFolder(p))
                paths.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { p }).Select(AssetDatabase.GUIDToAssetPath));
            else if (p.EndsWith(".prefab")) paths.Add(p);
        }
        paths = paths.Distinct().ToList();
        if (paths.Count == 0)
        {
            EditorUtility.DisplayDialog("glTF -> URP Lit", "Select one or more prefabs (or a folder of prefabs) in the Project window first.", "OK");
            return;
        }

        EnsureFolder(OutDir);
        EnsureFolder(TexDir);
        var map = new Dictionary<Material, Material>();
        int made = 0, reused = 0, prefabsChanged = 0, missing = 0;
        try
        {
            for (int i = 0; i < paths.Count; i++)
            {
                EditorUtility.DisplayProgressBar("glTF -> URP Lit (prefabs)", paths[i], (float)i / paths.Count);
                GameObject root = PrefabUtility.LoadPrefabContents(paths[i]);
                try
                {
                    bool changed = false;
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        bool rChanged = false;
                        for (int k = 0; k < mats.Length; k++)
                        {
                            var m = mats[k];
                            if (m == null) { missing++; continue; }
                            if (!IsGltf(m)) continue;
                            if (!map.TryGetValue(m, out var n))
                            {
                                string baseName = Safe(m.name) + "_" + ShortId(m);
                                string matPath = $"{OutDir}/{baseName}.mat";
                                n = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                                if (n != null) reused++;
                                else { n = ConvertMaterial(m, lit, baseName); AssetDatabase.CreateAsset(n, matPath); made++; }
                                map[m] = n;
                            }
                            mats[k] = n;
                            rChanged = true;
                        }
                        if (rChanged) { r.sharedMaterials = mats; changed = true; }
                    }
                    if (changed) { PrefabUtility.SaveAsPrefabAsset(root, paths[i]); prefabsChanged++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.SaveAssets();

        foreach (var kv in map) Debug.Log($"glTF -> URP Lit: {kv.Key.name} -> {AssetDatabase.GetAssetPath(kv.Value)}", kv.Value);
        EditorUtility.DisplayDialog("glTF -> URP Lit (prefabs)",
            $"Prefabs checked: {paths.Count}, updated: {prefabsChanged}.\nMaterials: {made} created, {reused} reused." +
            (missing > 0 ? $"\n\n{missing} material slot(s) were already empty/missing - restore the deleted source files and run this again." : ""),
            "OK");
    }

    static Material ConvertMaterial(Material src, Shader lit, string baseName)
    {
        var dst = new Material(lit) { name = baseName };

        Color baseColor = Col(src, "baseColorFactor", Color.white);
        dst.SetColor("_BaseColor", baseColor);
        dst.SetColor("_Color", baseColor);
        Texture t = Tex(src, "baseColorTexture");
        if (t != null)
        {
            var baked = Bake(t, baseName + "_BaseColor", Kind.Color);
            dst.SetTexture("_BaseMap", baked);
            dst.SetTexture("_MainTex", baked);
            CopyST(src, "baseColorTexture", dst, "_BaseMap");
            CopyST(src, "baseColorTexture", dst, "_MainTex");
        }

        t = Tex(src, "normalTexture");
        if (t != null)
        {
            dst.SetTexture("_BumpMap", Bake(t, baseName + "_Normal", Kind.Normal));
            dst.SetFloat("_BumpScale", Flt(src, "normalTexture_scale", 1f));
            dst.EnableKeyword("_NORMALMAP");
        }

        float metal = Flt(src, "metallicFactor", 1f);
        float rough = Flt(src, "roughnessFactor", 1f);
        t = Tex(src, "metallicRoughnessTexture");
        if (t != null)
        {
            dst.SetTexture("_MetallicGlossMap", Bake(t, baseName + "_MetallicGloss", Kind.MetallicGloss, metal, rough));
            dst.SetFloat("_Metallic", 1f);
            dst.SetFloat("_Smoothness", 1f);
            dst.SetFloat("_SmoothnessTextureChannel", 0f);
            dst.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        else
        {
            dst.SetFloat("_Metallic", metal);
            dst.SetFloat("_Smoothness", 1f - rough);
        }

        t = Tex(src, "occlusionTexture");
        if (t != null)
        {
            dst.SetTexture("_OcclusionMap", Bake(t, baseName + "_Occlusion", Kind.Occlusion));
            dst.SetFloat("_OcclusionStrength", Flt(src, "occlusionTexture_strength", 1f));
            dst.EnableKeyword("_OCCLUSIONMAP");
        }

        Color emissive = Col(src, "emissiveFactor", Color.black);
        t = Tex(src, "emissiveTexture");
        if (emissive.maxColorComponent > 0f)
        {
            dst.SetColor("_EmissionColor", emissive);
            if (t != null) dst.SetTexture("_EmissionMap", Bake(t, baseName + "_Emission", Kind.Color));
            dst.EnableKeyword("_EMISSION");
            dst.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }

        dst.SetFloat("_Cull", Flt(src, "_Cull", 2f));

        bool transparent = Flt(src, "_Surface", 0f) > 0.5f;
        bool alphaClip = Flt(src, "_AlphaClip", 0f) > 0.5f;
        if (alphaClip)
        {
            dst.SetFloat("_AlphaClip", 1f);
            dst.SetFloat("_Cutoff", Flt(src, "alphaCutoff", 0.5f));
            dst.EnableKeyword("_ALPHATEST_ON");
        }
        if (transparent)
        {
            dst.SetFloat("_Surface", 1f);
            dst.SetFloat("_Blend", 0f);
            dst.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            dst.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            dst.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            dst.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            dst.SetFloat("_ZWrite", 0f);
            dst.SetOverrideTag("RenderType", "Transparent");
            dst.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            dst.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            dst.SetFloat("_Surface", 0f);
            dst.SetFloat("_SrcBlend", 1f);
            dst.SetFloat("_DstBlend", 0f);
            dst.SetFloat("_ZWrite", 1f);
            dst.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
            dst.renderQueue = alphaClip ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;
        }
        return dst;
    }

    // ---------- texture baking ----------

    static Texture2D Bake(Texture src, string fileName, Kind kind, float metal = 1f, float rough = 1f)
    {
        string path = $"{TexDir}/{fileName}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        Texture2D read = ReadBack(src);
        Color32[] px = read.GetPixels32();
        switch (kind)
        {
            case Kind.Normal: FixNormal(px); break;
            case Kind.Occlusion:
                for (int i = 0; i < px.Length; i++) { byte o = px[i].r; px[i] = new Color32(o, o, o, 255); }
                break;
            case Kind.MetallicGloss:
                for (int i = 0; i < px.Length; i++)
                {
                    float m = px[i].b / 255f * metal;
                    float s = 1f - px[i].g / 255f * rough;
                    byte mb = (byte)Mathf.RoundToInt(Mathf.Clamp01(m) * 255f);
                    px[i] = new Color32(mb, mb, mb, (byte)Mathf.RoundToInt(Mathf.Clamp01(s) * 255f));
                }
                break;
        }
        read.SetPixels32(px);
        read.Apply();
        File.WriteAllBytes(path, read.EncodeToPNG());
        int w = read.width, h = read.height;
        Object.DestroyImmediate(read);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = kind == Kind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = kind == Kind.Color;
        ti.alphaSource = kind == Kind.MetallicGloss ? TextureImporterAlphaSource.FromInput : ti.alphaSource;
        ti.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(w, h)), 32, 8192);
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// GPU read-back that keeps the stored bytes exactly (sRGB textures go through an sRGB target, data through a linear one).
    static Texture2D ReadBack(Texture src)
    {
        bool srgb = GraphicsFormatUtility.IsSRGBFormat(src.graphicsFormat);
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32,
                                            srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
        var prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, !srgb);
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    /// Normal maps imported as DXT5nm (X in A, Y in G) or BC5 (X in R, Y in G, no Z) -> plain RGB tangent-space normals.
    static void FixNormal(Color32[] px)
    {
        if (px.Length == 0) return;
        double sumR = 0, sumB = 0; byte aMin = 255, aMax = 0;
        foreach (var p in px) { sumR += p.r; sumB += p.b; if (p.a < aMin) aMin = p.a; if (p.a > aMax) aMax = p.a; }
        double meanR = sumR / px.Length / 255.0, meanB = sumB / px.Length / 255.0;
        bool ag = meanR > 0.95 && aMax - aMin > 20;
        bool rg = !ag && meanB < 0.05;
        if (!ag && !rg) return; // already a normal RGB normal map

        for (int i = 0; i < px.Length; i++)
        {
            float x = (ag ? px[i].a : px[i].r) / 255f * 2f - 1f;
            float y = px[i].g / 255f * 2f - 1f;
            float z = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x - y * y));
            px[i] = new Color32(ToByte(x), ToByte(y), ToByte(z), 255);
        }
    }

    static byte ToByte(float n) => (byte)Mathf.RoundToInt(Mathf.Clamp01(n * 0.5f + 0.5f) * 255f);

    // ---------- helpers ----------

    static Texture Tex(Material m, string p) => m.HasProperty(p) ? m.GetTexture(p) : null;
    static float Flt(Material m, string p, float d) => m.HasProperty(p) ? m.GetFloat(p) : d;
    static Color Col(Material m, string p, Color d) => m.HasProperty(p) ? m.GetColor(p) : d;

    static void CopyST(Material src, string srcProp, Material dst, string dstProp)
    {
        if (!src.HasProperty(srcProp)) return;
        dst.SetTextureScale(dstProp, src.GetTextureScale(srcProp));
        dst.SetTextureOffset(dstProp, src.GetTextureOffset(srcProp));
    }

    static string ShortId(Material m)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string guid, out long localId);
        return Hash128.Compute(guid + ":" + localId).ToString().Substring(0, 6);
    }

    static string Safe(string n)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        return n.Trim();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
