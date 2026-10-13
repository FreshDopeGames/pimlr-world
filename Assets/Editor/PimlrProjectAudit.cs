// Place in Assets/Editor/PimlrProjectAudit.cs
// Run from the menu: PIMLR > Audit > Run Project Audit
// Writes PimlrAudit.md next to the Assets folder (project root, so it is never imported).
// Read-only: this script does not delete or modify anything.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PimlrProjectAudit
{
    static string _root;

    // Folders Unity always treats as "shipping or tooling" regardless of references.
    static readonly string[] KeepFolders =
    {
        "/Editor/", "/Resources/", "/StreamingAssets/", "/WebGLTemplates/",
        "/Plugins/", "/Gizmos/", "/Editor Default Resources/"
    };

    // Extensions we never report as "unreferenced" because AssetDatabase can't see
    // script-to-script usage (static classes, interfaces, etc).
    static readonly HashSet<string> SkipExt = new HashSet<string>
    {
        ".cs", ".asmdef", ".asmref", ".md", ".txt", ".json", ".xml", ".dll", ".rsp", ".uss", ".uxml"
    };

    static readonly Dictionary<string, string> PkgNotes = new Dictionary<string, string>
    {
        { "com.unity.ai.assistant",     "Editor AI tooling; the game does not need it" },
        { "com.unity.ai.inference",     "Inference runtime; remove unless you run ML models" },
        { "com.unity.collab-proxy",     "Unity Version Control UI (editor only); unnecessary if you use GitHub" },
        { "com.unity.visualscripting",  "Remove if no Visual Scripting graphs exist" },
        { "com.readyplayerme.core",     "Service is sunset; remove once avatars are stubbed" },
        { "com.atteneder.gltfast",      "Only needed for runtime avatar .glb loading" },
        { "com.unity.timeline",         "Keep only if Timeline assets/Playables are used" },
        { "com.unity.cinemachine",      "Keep if virtual cameras are used" },
    };

    static readonly Dictionary<string, string[]> PkgNamespaces = new Dictionary<string, string[]>
    {
        { "com.unity.cinemachine",     new[] { "Unity.Cinemachine", "Cinemachine" } },
        { "com.unity.timeline",        new[] { "UnityEngine.Timeline", "UnityEngine.Playables" } },
        { "com.unity.visualscripting", new[] { "Unity.VisualScripting" } },
        { "com.unity.inputsystem",     new[] { "UnityEngine.InputSystem" } },
        { "com.unity.ai.navigation",   new[] { "Unity.AI.Navigation" } },
        { "com.unity.addressables",    new[] { "UnityEngine.AddressableAssets" } },
        { "com.unity.postprocessing",  new[] { "UnityEngine.Rendering.PostProcessing" } },
        { "com.readyplayerme.core",    new[] { "ReadyPlayerMe" } },
        { "com.atteneder.gltfast",     new[] { "GLTFast" } },
        { "com.unity.ai.inference",    new[] { "Unity.InferenceEngine", "Unity.Sentis" } },
        { "com.unity.textmeshpro",     new[] { "TMPro" } },
    };

    [MenuItem("PIMLR/Audit/Run Project Audit")]
    public static void Run()
    {
        try
        {
            _root = Directory.GetParent(Application.dataPath).FullName;
            var sb = new StringBuilder();
            sb.AppendLine("# PIMLR Project Audit");
            sb.AppendLine("Unity " + Application.unityVersion + " | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine();
            sb.AppendLine("> Two kinds of weight: EDITOR weight (import time, Library size, compile time) and BUILD weight.");
            sb.AppendLine("> Unity only ships assets reachable from build scenes, Resources, StreamingAssets and project settings.");
            sb.AppendLine("> Unreferenced assets mostly slow the editor; they do not usually bloat the build.");
            sb.AppendLine();

            string[] all = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/") && File.Exists(Abs(p)))
                .ToArray();

            HashSet<string> reachable = ComputeReachable(all);

            SectionFolderSizes(sb, all);
            SectionLargest(sb, all);
            SectionScenes(sb);
            SectionUnreferenced(sb, all, reachable);
            SectionPackages(sb, all, reachable);
            SectionImportSettings(sb, reachable);
            SectionSpecialFolders(sb, all);
            SectionDuplicates(sb, all);

            string outPath = Path.Combine(_root, "PimlrAudit.md");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log("[PimlrAudit] Report written to " + outPath);
            EditorUtility.RevealInFinder(outPath);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ---------- helpers ----------

    static string Abs(string assetPath) { return Path.Combine(_root, assetPath); }
    static long Size(string assetPath) { return new FileInfo(Abs(assetPath)).Length; }
    static string MB(long bytes) { return (bytes / 1048576f).ToString("F1"); }
    static bool InKeepFolder(string p) { return KeepFolders.Any(k => p.Contains(k)); }

    static string FolderKey(string p, int depth)
    {
        string[] parts = p.Split('/');
        int n = Math.Min(parts.Length - 1, depth + 1);
        return string.Join("/", parts.Take(n));
    }

    static HashSet<string> ComputeReachable(string[] all)
    {
        var roots = new List<string>();
        roots.AddRange(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        roots.AddRange(all.Where(p => p.Contains("/Resources/")));

        // Assets referenced from project settings, not from scenes.
        if (GraphicsSettings.defaultRenderPipeline != null)
            roots.Add(AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            var rp = QualitySettings.GetRenderPipelineAssetAt(i);
            if (rp != null) roots.Add(AssetDatabase.GetAssetPath(rp));
        }
        foreach (var o in PlayerSettings.GetPreloadedAssets())
            if (o != null) roots.Add(AssetDatabase.GetAssetPath(o));

        roots = roots.Where(r => !string.IsNullOrEmpty(r)).Distinct().ToList();
        return new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
    }

    // ---------- sections ----------

    static void SectionFolderSizes(StringBuilder sb, string[] all)
    {
        sb.AppendLine("## 1. Folder sizes (source files on disk, excluding .meta)");
        foreach (int depth in new[] { 1, 2 })
        {
            sb.AppendLine();
            sb.AppendLine(depth == 1 ? "### Top level" : "### Second level");
            sb.AppendLine("| Folder | MB | Files |");
            sb.AppendLine("|---|---:|---:|");
            var rows = all.GroupBy(p => FolderKey(p, depth))
                .Select(g => new { Key = g.Key, Bytes = g.Sum(p => Size(p)), Count = g.Count() })
                .OrderByDescending(x => x.Bytes).Take(25);
            foreach (var r in rows) sb.AppendLine("| " + r.Key + " | " + MB(r.Bytes) + " | " + r.Count + " |");
        }
        sb.AppendLine();
    }

    static void SectionLargest(StringBuilder sb, string[] all)
    {
        sb.AppendLine("## 2. Largest 40 files");
        sb.AppendLine("| MB | Path |");
        sb.AppendLine("|---:|---|");
        foreach (var p in all.OrderByDescending(p => Size(p)).Take(40))
            sb.AppendLine("| " + MB(Size(p)) + " | " + p + " |");
        sb.AppendLine();
    }

    static void SectionScenes(StringBuilder sb)
    {
        sb.AppendLine("## 3. Scenes");
        var inBuild = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        sb.AppendLine("Enabled in build: " + (inBuild.Count == 0 ? "(none)" : string.Join(", ", inBuild)));
        sb.AppendLine();
        sb.AppendLine("Scenes NOT in the build (archive candidates):");
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!inBuild.Contains(p)) sb.AppendLine("- " + p + " (" + MB(Size(p)) + " MB)");
        }
        sb.AppendLine();
    }

    static void SectionUnreferenced(StringBuilder sb, string[] all, HashSet<string> reachable)
    {
        sb.AppendLine("## 4. Assets not reachable from build scenes / Resources / project settings");
        sb.AppendLine("Scripts, asmdefs and text files are excluded (static usage is invisible to AssetDatabase).");
        sb.AppendLine("Verify before deleting: assets loaded by string path, Addressables, or code-only references will show up here.");
        sb.AppendLine();

        var orphans = all.Where(p => !reachable.Contains(p)
                                     && !InKeepFolder(p)
                                     && !SkipExt.Contains(Path.GetExtension(p).ToLowerInvariant()))
                         .ToArray();

        long total = orphans.Sum(p => Size(p));
        sb.AppendLine("Total unreachable: " + MB(total) + " MB across " + orphans.Length + " files");
        sb.AppendLine();
        sb.AppendLine("| Folder (2 levels) | MB | Files |");
        sb.AppendLine("|---|---:|---:|");
        var rows = orphans.GroupBy(p => FolderKey(p, 2))
            .Select(g => new { Key = g.Key, Bytes = g.Sum(p => Size(p)), Count = g.Count() })
            .OrderByDescending(x => x.Bytes).Take(40);
        foreach (var r in rows) sb.AppendLine("| " + r.Key + " | " + MB(r.Bytes) + " | " + r.Count + " |");
        sb.AppendLine();

        var scriptsInDeadFolders = all.Where(p => p.EndsWith(".cs") && !InKeepFolder(p))
            .GroupBy(p => FolderKey(p, 2)).Select(g => new { Key = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(15);
        sb.AppendLine("Script counts by folder (for spotting out-of-scope game code, e.g. Helix / Humanity Rocks):");
        foreach (var s in scriptsInDeadFolders) sb.AppendLine("- " + s.Key + ": " + s.Count + " scripts");
        sb.AppendLine();
    }

    static void SectionPackages(StringBuilder sb, string[] all, HashSet<string> reachable)
    {
        sb.AppendLine("## 5. Packages (Packages/manifest.json)");
        string manifestPath = Path.Combine(_root, "Packages/manifest.json");
        if (!File.Exists(manifestPath)) { sb.AppendLine("manifest.json not found."); return; }

        string json = File.ReadAllText(manifestPath);
        Match block = Regex.Match(json, "\"dependencies\"\\s*:\\s*\\{([^}]*)\\}");
        var pkgs = new List<KeyValuePair<string, string>>();
        foreach (Match m in Regex.Matches(block.Groups[1].Value, "\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\""))
            pkgs.Add(new KeyValuePair<string, string>(m.Groups[1].Value, m.Groups[2].Value));

        // Read project code once.
        var code = new List<string>();
        foreach (var p in all.Where(p => p.EndsWith(".cs")))
            code.Add(File.ReadAllText(Abs(p)));

        var modules = pkgs.Where(k => k.Key.StartsWith("com.unity.modules.")).Select(k => k.Key.Substring(18)).ToList();
        var others = pkgs.Where(k => !k.Key.StartsWith("com.unity.modules.")).OrderBy(k => k.Key);

        sb.AppendLine("| Package | Version | Used by build content | Code refs | Verdict | Note |");
        sb.AppendLine("|---|---|:-:|---:|---|---|");
        foreach (var kv in others)
        {
            string name = kv.Key;
            bool usedByContent = reachable.Any(r => r.StartsWith("Packages/" + name + "/"));
            int refs = 0;
            string[] tokens;
            if (PkgNamespaces.TryGetValue(name, out tokens))
                refs = code.Count(c => tokens.Any(t => c.Contains(t)));

            bool core = name.StartsWith("com.unity.ide.") || name.StartsWith("com.unity.test-framework")
                        || name.StartsWith("com.unity.render-pipelines.") || name.StartsWith("com.unity.ugui");
            string note;
            PkgNotes.TryGetValue(name, out note);

            string verdict;
            if (core) verdict = "core/editor";
            else if (usedByContent || refs > 0) verdict = "in use";
            else if (PkgNamespaces.ContainsKey(name) || note != null) verdict = "REMOVE CANDIDATE";
            else verdict = "review";

            sb.AppendLine("| " + name + " | " + kv.Value + " | " + (usedByContent ? "yes" : "no") + " | " + refs
                          + " | " + verdict + " | " + (note ?? "") + " |");
        }
        sb.AppendLine();
        sb.AppendLine("Built-in modules enabled (disable any you don't need, e.g. vr, xr, ai, terrain, cloth, wind, "
                      + "unityanalytics): " + string.Join(", ", modules));
        sb.AppendLine();
        sb.AppendLine("Also check Assets/Samples/ (package samples imported into the project) and "
                      + "Assets/TextMesh Pro/Examples & Extras (demo scripts, safe to delete).");
        sb.AppendLine();
    }

    static void SectionImportSettings(StringBuilder sb, HashSet<string> reachable)
    {
        sb.AppendLine("## 6. Import settings on assets that DO ship (largest offenders first)");
        var texBig = new List<string>(); var texRead = new List<string>(); var texRaw = new List<string>();
        var meshRead = new List<string>(); var meshNoComp = new List<string>();
        var audioBig = new List<string>();

        var shipping = reachable.Where(p => p.StartsWith("Assets/")).ToArray();
        for (int i = 0; i < shipping.Length; i++)
        {
            string p = shipping[i];
            if (i % 200 == 0) EditorUtility.DisplayProgressBar("PIMLR Audit", "Import settings", (float)i / shipping.Length);
            AssetImporter imp = AssetImporter.GetAtPath(p);

            TextureImporter ti = imp as TextureImporter;
            if (ti != null)
            {
                var web = ti.GetPlatformTextureSettings("WebGL");
                int maxSize = web.overridden ? web.maxTextureSize : ti.maxTextureSize;
                if (maxSize >= 4096) texBig.Add(p);
                if (ti.isReadable) texRead.Add(p);
                if (ti.textureCompression == TextureImporterCompression.Uncompressed
                    && ti.textureType != TextureImporterType.Sprite && Size(p) > 512 * 1024) texRaw.Add(p);
                continue;
            }

            ModelImporter mi = imp as ModelImporter;
            if (mi != null)
            {
                if (mi.isReadable) meshRead.Add(p);
                if (mi.meshCompression == ModelImporterMeshCompression.Off && Size(p) > 500 * 1024) meshNoComp.Add(p);
                continue;
            }

            AudioImporter ai = imp as AudioImporter;
            if (ai != null && Size(p) > 1024 * 1024) audioBig.Add(p);
        }

        Action<string, List<string>> dump = (title, list) =>
        {
            sb.AppendLine();
            sb.AppendLine("### " + title + " (" + list.Count + ")");
            foreach (var p in list.OrderByDescending(x => Size(x)).Take(15))
                sb.AppendLine("- " + MB(Size(p)) + " MB  " + p);
        };
        dump("Textures with max size >= 4096 (WebGL override respected)", texBig);
        dump("Textures with Read/Write enabled (doubles memory)", texRead);
        dump("Textures > 512 KB with compression OFF", texRaw);
        dump("Models with Read/Write enabled", meshRead);
        dump("Models > 500 KB with mesh compression OFF", meshNoComp);
        dump("Audio clips > 1 MB (music should Stream; long SFX should be Compressed In Memory)", audioBig);
        sb.AppendLine();
    }

    static void SectionSpecialFolders(StringBuilder sb, string[] all)
    {
        sb.AppendLine("## 7. Folders that always ship");
        foreach (var key in new[] { "/Resources/", "/StreamingAssets/" })
        {
            var files = all.Where(p => p.Contains(key)).ToArray();
            sb.AppendLine("- " + key.Trim('/') + ": " + MB(files.Sum(p => Size(p))) + " MB in " + files.Length + " files");
        }
        sb.AppendLine("- StreamingAssets video is a common WebGL download-size culprit; check bitrate/resolution.");
        sb.AppendLine();
    }

    static void SectionDuplicates(StringBuilder sb, string[] all)
    {
        sb.AppendLine("## 8. Byte-identical duplicate files (>= 100 KB)");
        var candidates = all.Where(p => Size(p) >= 100 * 1024).ToArray();
        var byHash = new Dictionary<string, List<string>>();
        using (var md5 = MD5.Create())
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (i % 25 == 0) EditorUtility.DisplayProgressBar("PIMLR Audit", "Hashing files", (float)i / candidates.Length);
                string h;
                using (var fs = File.OpenRead(Abs(candidates[i])))
                    h = BitConverter.ToString(md5.ComputeHash(fs));
                List<string> list;
                if (!byHash.TryGetValue(h, out list)) { list = new List<string>(); byHash[h] = list; }
                list.Add(candidates[i]);
            }
        }
        var dupes = byHash.Values.Where(l => l.Count > 1)
            .OrderByDescending(l => Size(l[0]) * (l.Count - 1)).Take(30).ToList();
        long wasted = byHash.Values.Where(l => l.Count > 1).Sum(l => Size(l[0]) * (l.Count - 1));
        sb.AppendLine("Wasted by duplicates: " + MB(wasted) + " MB");
        foreach (var group in dupes)
        {
            sb.AppendLine("- " + MB(Size(group[0])) + " MB x " + group.Count);
            foreach (var p in group) sb.AppendLine("    - " + p);
        }
        sb.AppendLine();
    }
}
#endif