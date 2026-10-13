using UnityEngine;

/// <summary>
/// PIMLR: the single outfit color palette for every scene (SceneStaticEU customization + YannicksWorld apply).
/// The INDEX is what gets saved per renderer in PlayerPrefs, so never reorder or remove entries.
/// Indices 0-8 keep their legacy meaning from the old 9-color list; 9-11 were added for the 12-color picker.
/// </summary>
public static class PimlrPalette
{
    public static readonly Color[] Colors =
    {
        new Color(1.00f, 1.00f, 1.00f), // 0  White   (legacy white)
        new Color(0.80f, 0.11f, 0.13f), // 1  Red     (legacy red)
        new Color(0.13f, 0.62f, 0.30f), // 2  Green   (legacy lime, toned down)
        new Color(0.06f, 0.06f, 0.06f), // 3  Black   (legacy black)
        new Color(0.33f, 0.19f, 0.12f), // 4  Brown   (legacy dark brown, lifted so it reads)
        new Color(0.50f, 0.51f, 0.53f), // 5  Gray    (was off-white, a near-duplicate of white)
        new Color(0.05f, 0.11f, 0.52f), // 6  Navy    (legacy navy)
        new Color(1.00f, 0.42f, 0.05f), // 7  Orange  (legacy orange)
        new Color(0.86f, 0.66f, 0.15f), // 8  Gold    (legacy "leopard" mustard)
        new Color(0.45f, 0.22f, 0.66f), // 9  Purple  (new)
        new Color(0.00f, 0.62f, 0.70f), // 10 Teal    (new)
        new Color(0.93f, 0.43f, 0.62f), // 11 Pink    (new)
    };

    public static readonly string[] Names =
    {
        "WHITE", "RED", "GREEN", "BLACK", "BROWN", "GRAY",
        "NAVY", "ORANGE", "GOLD", "PURPLE", "TEAL", "PINK",
    };

    /// <summary>Grid order for the picker: neutrals / warm / cool, 4 per row.</summary>
    public static readonly int[] DisplayOrder = { 0, 5, 3, 4, 1, 7, 8, 11, 2, 10, 6, 9 };

    public static int Count => Colors.Length;

    public static bool IsValid(int index) => index >= 0 && index < Colors.Length;

    public static Color Get(int index) => IsValid(index) ? Colors[index] : Colors[0];

    public static string NameOf(int index) => IsValid(index) ? Names[index] : "";
}
