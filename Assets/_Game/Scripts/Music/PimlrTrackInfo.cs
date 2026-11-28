// PIMLR #UI (Nov 27): one place that answers "who / what / which project" for a MusicData track.
// Uses the MusicData fields (artist, title, featuredArtists, project, trackNumber) when they're filled, otherwise parses
// the clip name, which follows "Artist - Title ft. Guests" or "Artist - Title (ft. Guests)".
// Project has no runtime fallback beyond "Singles": fill it with PIMLR > Music > Fill Track Info (reads the folder).
//
// Unreal note: these become fields on the track's UDataAsset (UPimlrTrackAsset); the parse helper is editor-only there.

using System.Text.RegularExpressions;
using UnityEngine;

public static class PimlrTrackInfo
{
    public const string DefaultProject = "Singles";

    public struct Parsed { public string artist, title, featured; }

    static readonly Regex Ft = new Regex(@"\s*\(?\s*\b(?:ft|feat)\.?\s+(.+?)\)?\s*$", RegexOptions.IgnoreCase);

    /// <summary>"Thurz - BIG YAPER (ft. Mike & Keys)" -> artist Thurz, title BIG YAPER, featured Mike & Keys.</summary>
    public static Parsed Parse(string clipName)
    {
        var p = new Parsed { artist = "", title = clipName ?? "", featured = "" };
        if (string.IsNullOrEmpty(clipName)) return p;
        string s = Regex.Replace(clipName, @"\s+", " ").Trim();
        int dash = s.IndexOf(" - ", System.StringComparison.Ordinal);
        if (dash > 0) { p.artist = s.Substring(0, dash).Trim(); s = s.Substring(dash + 3).Trim(); }
        var m = Ft.Match(s);
        if (m.Success) { p.featured = m.Groups[1].Value.Trim().TrimEnd(')'); s = s.Substring(0, m.Index).Trim(); }
        p.title = s;
        return p;
    }

    static Parsed Clip(MusicData d) => Parse(d != null && d.audioClip ? d.audioClip.name : "");

    public static string Artist(MusicData d) => d != null && !string.IsNullOrWhiteSpace(d.artist) ? d.artist : Clip(d).artist;
    public static string Title(MusicData d) => d != null && !string.IsNullOrWhiteSpace(d.title) ? d.title : Clip(d).title;
    public static string Featured(MusicData d) => d != null && !string.IsNullOrWhiteSpace(d.featuredArtists) ? d.featuredArtists : Clip(d).featured;
    public static string Project(MusicData d) => d != null && !string.IsNullOrWhiteSpace(d.project) ? d.project : DefaultProject;

    /// <summary>"Thurz ft. Mike & Keys" (or just the artist).</summary>
    public static string Credit(MusicData d)
    {
        string a = Artist(d), f = Featured(d);
        return string.IsNullOrEmpty(f) ? a : (string.IsNullOrEmpty(a) ? "ft. " + f : a + " ft. " + f);
    }

    /// <summary>The catalog entry for a clip (null if the clip isn't in the catalog).</summary>
    public static MusicData Find(AudioClip clip)
    {
        var data = SceneManagerScript.Instance ? SceneManagerScript.Instance.musicData : null;
        if (data == null || !clip) return null;
        foreach (var d in data) if (d != null && d.audioClip == clip) return d;
        return null;
    }
}
