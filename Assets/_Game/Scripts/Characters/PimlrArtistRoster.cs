// PIMLR (Nov 16): the playable artists, and which one the player picked on the Select Your Artist carousel.
//
// Live copy: Assets/_Game/Resources/PIMLR/ArtistRoster.asset (loaded at runtime, so no scene references are needed).
// To add an artist: add an entry with a unique id and a display name; give it a Display Prefab (a visual-only model:
// mesh + Animator) for the carousel. An entry with no Display Prefab is shown as a copy of the scene's Player model -
// that is how Thurz appears today.
//
//   PimlrArtists.SelectedId / PimlrArtists.Selected   the current pick (saved in PlayerPrefs, survives sessions)
//   PimlrArtists.SelectionChanged                     raised when the player confirms a pick
//   entry.hubIntroDialogue                            optional: a different "Sirihanna contacts you" dialogue for
//                                                     this artist (empty = the scene's default dialogue)
//
// Unreal note: a UPrimaryDataAsset per artist (soft refs to the pawn class / mesh, dialogue DataTable row), listed by
// the Asset Manager; the pick lives on a UGameInstanceSubsystem and is written to the USaveGame.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "PIMLR/Artist Roster", fileName = "ArtistRoster")]
public class PimlrArtistRoster : ScriptableObject
{
    [Serializable]
    public class Artist
    {
        [Tooltip("Stable key saved with the player's pick. Never rename once shipped.")]
        public string id = "thurz";
        public string displayName = "THURZ";
        [Tooltip("One short line under the name on the carousel.")]
        public string tagline = "";
        [Tooltip("Visual-only model for the carousel. Empty = a copy of the scene's Player model.")]
        public GameObject displayPrefab;
        [Tooltip("Size tweak for the Display Prefab on the carousel (1 = as authored, scaled to the scene's characters).")]
        [Min(0.01f)] public float displayScale = 1f;
        [Tooltip("Optional: this artist's version of the Living Room intro dialogue. Empty = the scene's default.")]
        public DialogueScriptable hubIntroDialogue;
    }

    public List<Artist> artists = new List<Artist>();

    [Header("Carousel look")]
    public TMP_FontAsset titleFont;
    public TMP_FontAsset bodyFont;
}

public static class PimlrArtists
{
    public const string PrefKey = "PIMLR_SelectedArtist";
    const string ResourcePath = "PIMLR/ArtistRoster";

    public static event Action<string> SelectionChanged;

    static PimlrArtistRoster roster;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { roster = null; SelectionChanged = null; }

    public static PimlrArtistRoster Roster
    {
        get
        {
            if (roster) return roster;
            roster = Resources.Load<PimlrArtistRoster>(ResourcePath);
            if (!roster || roster.artists == null || roster.artists.Count == 0)
            {
                // No asset (or an empty one): the game still has its one artist.
                if (!roster) roster = ScriptableObject.CreateInstance<PimlrArtistRoster>();
                roster.artists = new List<PimlrArtistRoster.Artist> { new PimlrArtistRoster.Artist() };
            }
            return roster;
        }
    }

    public static IReadOnlyList<PimlrArtistRoster.Artist> All => Roster.artists;

    public static int IndexOf(string id)
    {
        var all = All;
        for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].id == id) return i;
        return -1;
    }

    /// <summary>The saved pick; the first artist if nothing valid is saved.</summary>
    public static string SelectedId
    {
        get
        {
            string id = PlayerPrefs.GetString(PrefKey, "");
            return IndexOf(id) >= 0 ? id : All[0].id;
        }
    }

    public static PimlrArtistRoster.Artist Selected => All[Mathf.Max(0, IndexOf(SelectedId))];

    public static void Select(string id)
    {
        if (IndexOf(id) < 0) return;
        PlayerPrefs.SetString(PrefKey, id);
        PlayerPrefs.Save();
        SelectionChanged?.Invoke(id);
    }
}
