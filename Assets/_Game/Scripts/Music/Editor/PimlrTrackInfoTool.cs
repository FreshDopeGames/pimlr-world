// PIMLR #UI (Nov 27): PIMLR > Music > Fill Track Info.
// Fills empty MusicData track-info fields on Global UI's SceneManagerScript (Resources prefab):
//   artist / title / featuredArtists  from the clip name ("Artist - Title (ft. Guests)")
//   project                           from the clip's folder "Artist - Project - PIMLR World" (skips sub-folders such
//                                     as NewSound); a clip with no such folder becomes "Singles"
//   trackNumber                       catalog order within its project
// Only empty fields are written, so anything typed by hand is kept. Prints what it filled.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PimlrTrackInfoTool
{
    const string PrefabPath = "Assets/_Game/Resources/Global UI.prefab";

    [MenuItem("PIMLR/Music/Fill Track Info")]
    public static string Fill()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var log = new List<string>();
        try
        {
            var sms = root.GetComponentInChildren<SceneManagerScript>(true);
            if (!sms) return "no SceneManagerScript in Global UI";
            var counts = new Dictionary<string, int>();
            foreach (var d in sms.musicData)
            {
                if (d == null || !d.audioClip) continue;
                var p = PimlrTrackInfo.Parse(d.audioClip.name);
                if (string.IsNullOrWhiteSpace(d.artist)) d.artist = p.artist;
                if (string.IsNullOrWhiteSpace(d.title)) d.title = p.title;
                if (string.IsNullOrWhiteSpace(d.featuredArtists)) d.featuredArtists = p.featured;
                if (string.IsNullOrWhiteSpace(d.project)) d.project = ProjectFromFolder(AssetDatabase.GetAssetPath(d.audioClip));
                counts.TryGetValue(d.project, out int n);
                counts[d.project] = ++n;
                if (d.trackNumber <= 0) d.trackNumber = n;
                log.Add($"{d.project} #{d.trackNumber}: {d.title} | {d.artist}" + (string.IsNullOrEmpty(d.featuredArtists) ? "" : " ft. " + d.featuredArtists));
            }
            EditorUtility.SetDirty(sms);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var report = "Track info filled:\n  " + string.Join("\n  ", log);
        Debug.Log("[PIMLR Music] " + report);
        return report;
    }

    /// <summary>".../Thurz - Purple Avirex - PIMLR World/x.wav" -> "Purple Avirex".</summary>
    public static string ProjectFromFolder(string assetPath)
    {
        var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        while (!string.IsNullOrEmpty(dir))
        {
            var name = Path.GetFileName(dir);
            var parts = name.Split(new[] { " - " }, System.StringSplitOptions.None);
            if (parts.Length >= 3 && parts[parts.Length - 1].Trim().StartsWith("PIMLR")) return parts[1].Trim();
            if (parts.Length == 2) return parts[1].Trim();
            if (name == "Audio" || name == "Assets") break;
            dir = Path.GetDirectoryName(dir)?.Replace('\\', '/');
        }
        return PimlrTrackInfo.DefaultProject;
    }
}
