// #UI: token keys for the PIMLR UI design system. Screens reference these keys, never raw colors/fonts/sprites.
// Values are serialized as ints, so ONLY APPEND new members at the end; never reorder or remove (same rule as Zone).
//
// Unreal note: these become UENUMs (EPimlrColorToken, EPimlrTypeRole, ...) used as TMap keys in UPimlrUIStyle.

namespace PIMLR.UI
{
    public enum ColorToken
    {
        SurfaceBase = 0,    // #222220 charcoal
        SurfaceRaised = 1,  // #2E2E2B cards / rows
        SurfaceScrim = 2,   // #0E0E0D @ 70%
        BrandForest = 3,    // #3C6C1A
        BrandBrick = 4,     // #872818
        BrandLime = 5,      // #CDF952
        TextPrimary = 6,    // #F4F1EA
        TextSecondary = 7,  // #A8A69E
        TextOnLime = 8,     // #222220
        AccentMusic = 9,    // #9B5CF6 purple: Track Boost, music
        AccentInfo = 10,    // #22D3EE cyan: objectives, Infinite Mode
        AccentWarn = 11,    // #FF8A1F orange: low ammo, IN DANGER
        AccentGold = 12,    // #F5C542 rank 1, achievements, coins
        StateDanger = 13,   // #E5484D
        StateDisabled = 14, // #55554F
        White = 15,
        Clear = 16,
        HealthGreen = 17,   // #00FF00 the green of the plus-sign health pickup (health bar)
    }

    public enum TypeRole
    {
        Display = 0,  // Russo One (PHATT slot later)
        Heading = 1,  // Russo One
        Body = 2,     // Barlow Medium
        Utility = 3,  // Barlow SemiBold: numbers, timers, coins, ammo
        Caption = 4,  // Barlow Medium small: labels, stat names
        PlayerName = 5, // Russo One, ORIGINAL casing: player names are user data, never force-uppercased
    }

    public enum SpriteSlot
    {
        None = 0,
        Panel = 1,        // 9-slice angled panel frame
        PanelEdge = 2,    // 9-slice outline that matches Panel
        Card = 3,         // 9-slice card
        Button = 4,       // 9-slice button shape (states via tint)
        FocusRing = 5,    // 9-slice outline matching Button
        Pattern = 6,      // tileable brand pattern
        Halftone = 7,     // tileable halftone / grunge
        BarFill = 8,
        BarBack = 9,
        Badge = 10,       // rank badge base
        Chip = 11,        // small pill (mode chip, coin chip)
    }

    /// <summary>How much a button moves on hover/focus/press. Big menu buttons pop; slots and icons stay calm.</summary>
    public enum ButtonMotion
    {
        Theme = 0,    // the theme's default (hoverScale)
        Compact = 1,  // the theme's compactHoverScale (inventory slots, list rows)
        None = 2,     // no scale at all (buttons another script animates, tight layouts)
    }

    /// <summary>Text casing for a ThemedText.</summary>
    public enum TextCase
    {
        FromRole = 0, // whatever the type role says
        Original = 1, // never change the casing (names, messages, descriptions)
        Upper = 2,
    }

    public enum ButtonVariant
    {
        Primary = 0,
        Secondary = 1,
        Tertiary = 2,
        Destructive = 3,
        Icon = 4,
    }
}
