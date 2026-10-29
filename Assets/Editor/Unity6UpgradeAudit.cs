// PIMLR World — Unity 6 Upgrade Audit
// Drop into: Assets/Editor/Unity6UpgradeAudit.cs   (Unity 2022.3.x)
// Menu:      Tools > PIMLR > Unity 6 Upgrade Audit
// READ-ONLY: scans the project and writes UpgradeAudit_Unity6.md to the project root.
//            It never modifies assets, scenes or packages.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PIMLR.EditorTools
{
    public static class Unity6UpgradeAudit
    {
        enum Sev { Blocker, Fix, Warn }

        class Finding
        {
            public Sev Sev;
            public string Category;
            public string Path;
            public string Detail;
        }

        class CodeRule
        {
            public readonly Regex Rx; public readonly Sev Sev; public readonly string Category; public readonly string Note;
            public CodeRule(string pattern, Sev sev, string category, string note)
            {
                Rx = new Regex(pattern, RegexOptions.Compiled);
                Sev = sev; Category = category; Note = note;
            }
        }

        // ---------- Script rules ----------
        static readonly CodeRule[] CodeRules =
        {
            new CodeRule(@"\busing\s+ReadyPlayerMe", Sev.Blocker, "Ready Player Me",
                "RPM Core does not compile on Unity 6.x and will never be patched. Remove this dependency in 2022.3 first."),
            new CodeRule(@"\busing\s+Cinemachine\s*;", Sev.Fix, "Cinemachine 2 -> 3",
                "Namespace becomes Unity.Cinemachine (CM3 runs on 2022.3, so this can be done before the editor jump)."),
            new CodeRule(@"\bCinemachine(VirtualCamera|FreeLook|Transposer|FramingTransposer|OrbitalTransposer|POV)\b", Sev.Fix, "Cinemachine 2 -> 3",
                "Deprecated CM2 type. CM3 uses CinemachineCamera + behaviour components; use the Cinemachine Upgrader."),
            new CodeRule(@"\b(enableWordWrapping|enableKerning)\b", Sev.Fix, "TextMeshPro",
                "Obsolete-as-error in Unity 6 TMP. Use textWrappingMode / fontFeatures."),
            new CodeRule(@"\b(UxmlFactory|UxmlTraits)\b", Sev.Fix, "UI Toolkit",
                "Pattern no longer supported. Use [UxmlElement] / [UxmlAttribute]."),
            new CodeRule(@"\bFindObjects?OfType\s*[<(]", Sev.Warn, "Obsolete API",
                "Obsolete warning in Unity 6. Use FindFirstObjectByType / FindObjectsByType."),
        };

        static readonly Regex ScriptGuidRx =
            new Regex(@"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("Tools/PIMLR/Unity 6 Upgrade Audit")]
        public static void Run()
        {
            var findings = new List<Finding>();
            try
            {
                ScanManifest(findings);
                ScanFolders(findings);
                ScanCode(findings);
                ScanPrefabs(findings);
                ScanScenes(findings);
                ScanMaterials(findings);
                ScanRenderers(findings);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[Unity6 Audit] Cancelled.");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string outPath = Path.Combine(ProjectRoot, "UpgradeAudit_Unity6.md");
            File.WriteAllText(outPath, BuildReport(findings));

            int blockers = findings.Count(f => f.Sev == Sev.Blocker);
            int fixes = findings.Count(f => f.Sev == Sev.Fix);
            int warns = findings.Count(f => f.Sev == Sev.Warn);
            string verdict = blockers == 0 ? "READY to try on a branch" : "NOT READY";
            Debug.Log($"[Unity6 Audit] {verdict} — Blockers: {blockers}, Fix: {fixes}, Warn: {warns}. Report: {outPath}");
            EditorUtility.RevealInFinder(outPath);
        }

        static void Progress(string label, int i, int total)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Unity 6 Upgrade Audit", label, total == 0 ? 1f : (float)i / total))
                throw new OperationCanceledException();
        }

        static void Add(List<Finding> list, Sev sev, string cat, string path, string detail) =>
            list.Add(new Finding { Sev = sev, Category = cat, Path = path, Detail = detail });

        // ---------- Packages/manifest.json ----------
        static void ScanManifest(List<Finding> f)
        {
            string path = Path.Combine(ProjectRoot, "Packages", "manifest.json");
            if (!File.Exists(path)) return;
            string json = File.ReadAllText(path);

            void Check(string pattern, Sev sev, string cat, string note)
            {
                foreach (Match m in Regex.Matches(json, pattern))
                    Add(f, sev, cat, "Packages/manifest.json", $"{m.Value.Trim()} — {note}");
            }

            Check(@"""com\.readyplayerme[^""]*""\s*:\s*""[^""]*""", Sev.Blocker, "Ready Player Me",
                "Remove after avatars are converted to local FBX/prefabs.");
            Check(@"""com\.atteneder\.gltfast""\s*:\s*""[^""]*""", Sev.Fix, "glTFast",
                "Replaced by com.unity.cloud.gltfast on Unity 6. Not needed at all once no meshes come from .glb.");
            Check(@"""com\.unity\.cinemachine""\s*:\s*""2\.[^""]*""", Sev.Fix, "Cinemachine 2 -> 3",
                "Upgrade to Cinemachine 3.x while still on 2022.3 to isolate camera breakage.");
            Check(@"""com\.unity\.textmeshpro""\s*:\s*""[^""]*""", Sev.Warn, "TextMeshPro",
                "Merged into com.unity.ugui on Unity 6; migrated automatically.");
        }

        // ---------- Folders worth removing ----------
        static void ScanFolders(List<Finding> f)
        {
            if (Directory.Exists(Path.Combine(Application.dataPath, "TextMesh Pro/Examples & Extras")))
                Add(f, Sev.Fix, "TextMeshPro", "Assets/TextMesh Pro/Examples & Extras",
                    "Demo content that fails to compile on Unity 6. Delete the folder.");

            foreach (var (folder, game) in new[] { ("ARC", "Humanity Rocks"), ("CarControllerwithShooting", "Helix") })
                if (Directory.Exists(Path.Combine(Application.dataPath, folder)))
                    Add(f, Sev.Warn, "Out-of-scope content", $"Assets/{folder}",
                        $"{game} — not PIMLR. Removing it (on a branch) shrinks the upgrade surface and the URP renderer conflict.");
        }

        // ---------- C# scripts under Assets ----------
        static void ScanCode(List<Finding> f)
        {
            string[] files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string full = files[i].Replace('\\', '/');
                string rel = "Assets" + full.Substring(Application.dataPath.Length);
                if (rel.Contains("/TextMesh Pro/Examples & Extras/")) continue; // covered by folder finding
                if (i % 25 == 0) Progress("Scripts: " + rel, i, files.Length);

                string[] lines = File.ReadAllLines(full);
                for (int ln = 0; ln < lines.Length; ln++)
                    foreach (var r in CodeRules)
                        if (r.Rx.IsMatch(lines[ln]))
                            Add(f, r.Sev, r.Category, $"{rel}:{ln + 1}", r.Note);
            }
        }

        // ---------- Prefabs ----------
        static void ScanPrefabs(List<Finding> f)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (i % 10 == 0) Progress("Prefabs: " + p, i, guids.Length);
                if (i > 0 && i % 300 == 0) EditorUtility.UnloadUnusedAssetsImmediate();

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (root == null) continue;

                int missing = 0;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (missing > 0)
                    Add(f, Sev.Fix, "Missing scripts", p, $"{missing} missing script component(s).");

                string[] rpmTypes = root.GetComponentsInChildren<Component>(true)
                    .Where(c => c != null && (c.GetType().Namespace ?? "").StartsWith("ReadyPlayerMe"))
                    .Select(c => c.GetType().Name).Distinct().ToArray();
                if (rpmTypes.Length > 0)
                    Add(f, Sev.Blocker, "Ready Player Me", p, "RPM components: " + string.Join(", ", rpmTypes));

                string[] glbMeshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(s => s.sharedMesh != null)
                    .Select(s => AssetDatabase.GetAssetPath(s.sharedMesh))
                    .Where(a => a.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ||
                                a.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                    .Distinct().ToArray();
                if (glbMeshes.Length > 0)
                    Add(f, Sev.Fix, "Avatar meshes from .glb", p,
                        "Skinned meshes depend on the glTFast importer: " + string.Join(", ", glbMeshes) +
                        ". Convert to FBX before the upgrade (also what Unreal will want).");
            }
        }

        // ---------- Build scenes (text scan, does not open scenes) ----------
        static void ScanScenes(List<Finding> f)
        {
            var scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                string path = scenes[i].path;
                Progress("Scenes: " + path, i, scenes.Length);
                if (!File.Exists(path)) continue;

                string tag = scenes[i].enabled ? "" : " (disabled in Build Settings)";
                string text = File.ReadAllText(path);
                if (!text.StartsWith("%YAML"))
                {
                    Add(f, Sev.Warn, "Serialization", path + tag,
                        "Scene is binary. Set Project Settings > Editor > Asset Serialization to Force Text to audit it.");
                    continue;
                }

                var missing = new HashSet<string>();
                var rpm = new HashSet<string>();
                foreach (Match m in ScriptGuidRx.Matches(text))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                    if (string.IsNullOrEmpty(assetPath)) missing.Add(m.Groups[1].Value);
                    else if (assetPath.Replace(" ", "").IndexOf("readyplayerme", StringComparison.OrdinalIgnoreCase) >= 0)
                        rpm.Add(Path.GetFileNameWithoutExtension(assetPath));
                }

                if (missing.Count > 0)
                    Add(f, Sev.Fix, "Missing scripts", path + tag, $"{missing.Count} unresolved script GUID(s) in the scene.");
                if (rpm.Count > 0)
                    Add(f, Sev.Blocker, "Ready Player Me", path + tag, "Scene uses RPM scripts: " + string.Join(", ", rpm));
            }
        }

        // ---------- Materials ----------
        static void ScanMaterials(List<Finding> f)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (i % 50 == 0) Progress("Materials: " + p, i, guids.Length);

                var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (mat == null) continue;
                string sh = mat.shader != null ? mat.shader.name : "<none>";

                if (mat.shader == null || sh == "Hidden/InternalErrorShader")
                    Add(f, Sev.Fix, "Pink material (shader missing)", p, "Shader missing or failed to compile.");
                else if (sh == "Standard" || sh.StartsWith("Standard (") || sh.StartsWith("Legacy Shaders/") ||
                         sh.StartsWith("Mobile/") || sh.StartsWith("Autodesk Interactive"))
                    Add(f, Sev.Warn, "Built-in RP material", p,
                        $"'{sh}' renders pink in URP. Pre-existing; fix with Window > Rendering > Render Pipeline Converter.");
            }
        }

        // ---------- URP renderers ----------
        static void ScanRenderers(List<Finding> f)
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" });
            if (guids.Length <= 1) return;

            string[] modes = { "Forward", "Deferred", "Forward+" };
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var obj = AssetDatabase.LoadMainAssetAtPath(p);
                string mode = "?";
                if (obj != null)
                {
                    var prop = new SerializedObject(obj).FindProperty("m_RenderingMode");
                    if (prop != null && prop.intValue >= 0 && prop.intValue < modes.Length) mode = modes[prop.intValue];
                }
                Add(f, Sev.Warn, "Multiple URP renderers", p,
                    $"{mode} renderer — {guids.Length} renderers in project. Prime suspect for WebGL white/pink output; settle on one.");
            }
        }

        // ---------- Report ----------
        static string BuildReport(List<Finding> f)
        {
            int b = f.Count(i => i.Sev == Sev.Blocker);
            int x = f.Count(i => i.Sev == Sev.Fix);
            int w = f.Count(i => i.Sev == Sev.Warn);

            var sb = new StringBuilder();
            sb.AppendLine("# PIMLR World — Unity 6 Upgrade Audit");
            sb.AppendLine($"Editor: {Application.unityVersion} | Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            sb.AppendLine(b == 0
                ? "**Verdict: no blockers. Safe to attempt the upgrade on a branch.**"
                : $"**Verdict: NOT READY — {b} blocker(s) must be cleared in 2022.3 first.**");
            sb.AppendLine();
            sb.AppendLine($"Blockers: {b} | Fix before/during upgrade: {x} | Warnings: {w}");

            foreach (Sev s in new[] { Sev.Blocker, Sev.Fix, Sev.Warn })
            {
                var group = f.Where(i => i.Sev == s).ToList();
                if (group.Count == 0) continue;
                sb.AppendLine().AppendLine($"## {s} ({group.Count})");
                foreach (var cat in group.GroupBy(i => i.Category).OrderBy(g => g.Key))
                {
                    sb.AppendLine().AppendLine($"### {cat.Key} ({cat.Count()})");
                    foreach (var i in cat.OrderBy(i => i.Path))
                        sb.AppendLine($"- `{i.Path}` — {i.Detail}");
                }
            }
            return sb.ToString();
        }
    }
}
#endif
