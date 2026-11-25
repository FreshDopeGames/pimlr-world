// PIMLR: one-click WebGL build + deploy (pimlrworld.com, site root).
//   PIMLR > Build WebGL & Deploy    - builds to <project>/Build, deploys only if the build Succeeded
//   PIMLR > Deploy Last WebGL Build - re-runs the deploy on whatever is in <project>/Build
// The deploy itself is Tools/Deploy/deploy-webgl.ps1, opened in a visible PowerShell window.
// A menu item (not an IPostprocessBuildWithReport hook) because post-build callbacks see the
// result as Unknown, so they can't tell a failed build from a good one.
//
// Unreal translation: a post-step after RunUAT BuildCookRun (Python or BuildGraph). Unreal has
// no web export, so its "deploy" target is Steam/Epic depots, not this VM.
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class PimlrDeployMenu
{
    const string OutDir = "Build";                          // matches the folder the deploy script expects
    const string DeployScript = "Tools/Deploy/deploy-webgl.ps1";

    static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

    [MenuItem("PIMLR/Build WebGL && Deploy", priority = 1)]
    public static void BuildAndDeploy()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
            !EditorUtility.DisplayDialog("PIMLR Deploy",
                "The active platform isn't Web. Building will switch platforms first, which can take a long time. Continue?",
                "Build anyway", "Cancel"))
            return;

        // PIMLR: hashed filenames make Unity 6's loader.js rebuild itself in a loop
        // ("Backend has requested a buildprogram run 6 times"), so force the setting off.
        if (PlayerSettings.WebGL.nameFilesAsHashes)
        {
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            Debug.Log("PIMLR DEPLOY: turned off Name Files As Hashes (it causes the 6-run build loop).");
        }
        if (PlayerSettings.WebGL.template != "PROJECT:PIMLR")
            Debug.LogWarning("PIMLR DEPLOY: WebGL Template is '" + PlayerSettings.WebGL.template + "', not PIMLR - the deploy guard will refuse an index.html with leftover placeholders.");

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) { Debug.LogError("PIMLR DEPLOY: no enabled scenes in Build Settings - nothing built."); return; }

        Debug.Log($"PIMLR DEPLOY: building {scenes.Length} scene(s) to {OutDir}/ ...");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutDir,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        });

        var s = report.summary;
        Debug.Log($"PIMLR DEPLOY: build {s.result} in {s.totalTime:hh\\:mm\\:ss}, {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} error(s)");
        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("PIMLR DEPLOY: build did not succeed - nothing was uploaded. Check the Console above for the first error.");
            return;
        }
        RunDeploy();
    }

    [MenuItem("PIMLR/Deploy Last WebGL Build", priority = 2)]
    public static void RunDeploy()
    {
        string script = Path.Combine(ProjectRoot, DeployScript);
        if (!File.Exists(script)) { Debug.LogError("PIMLR DEPLOY: missing " + script); return; }

        var psi = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -NoExit -File \"{script}\" -BuildDir \"{Path.Combine(ProjectRoot, OutDir)}\"")
        {
            UseShellExecute = true,          // visible window: shows progress and any gcloud prompts
            WorkingDirectory = ProjectRoot
        };
        Process.Start(psi);
        Debug.Log("PIMLR DEPLOY: deploy window opened - it checks the build, uploads, syncs the VM, and verifies the live site.");
    }
}
