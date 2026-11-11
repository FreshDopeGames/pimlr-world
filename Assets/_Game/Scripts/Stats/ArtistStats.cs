using System;
using UnityEngine;

// PIMLR #STATS: an artist's six ratings (1-10). Released artists use 2-9 and always total 30, so no artist is
// simply better; 1 and 10 stay free for boosts and future tuning. All 5s = today's tuning exactly (Thurz).
// Phase 2 (Artist Roster) embeds one of these in each ArtistDefinition.
//
// Unreal: six attributes on a GAS AttributeSet; ratings live on the artist's UPrimaryDataAsset.
public enum StatKind
{
    Speed,
    Agility,
    Accuracy,
    Health,
    Stamina,
    Jump
}

[Serializable]
public struct ArtistStats
{
    public const int Budget = 30;
    public const int ReleasedMin = 2;
    public const int ReleasedMax = 9;

    [Range(1, 10)] public int speed;
    [Range(1, 10)] public int agility;
    [Range(1, 10)] public int accuracy;
    [Range(1, 10)] public int health;
    [Range(1, 10)] public int stamina;
    [Range(1, 10)] public int jump;

    /// <summary>Thurz / today's tuning.</summary>
    public static ArtistStats Baseline => new ArtistStats { speed = 5, agility = 5, accuracy = 5, health = 5, stamina = 5, jump = 5 };

    public int Total => speed + agility + accuracy + health + stamina + jump;

    public int Get(StatKind k)
    {
        switch (k)
        {
            case StatKind.Speed: return speed;
            case StatKind.Agility: return agility;
            case StatKind.Accuracy: return accuracy;
            case StatKind.Health: return health;
            case StatKind.Stamina: return stamina;
            default: return jump;
        }
    }

    /// <summary>True if this is a legal released artist: every rating 2-9 and the total is 30.</summary>
    public bool IsValidForRelease(out string problem)
    {
        foreach (StatKind k in Enum.GetValues(typeof(StatKind)))
        {
            int v = Get(k);
            if (v < ReleasedMin || v > ReleasedMax) { problem = $"{k} is {v}; released artists use {ReleasedMin}-{ReleasedMax}."; return false; }
        }
        if (Total != Budget) { problem = $"Ratings total {Total}; they must total {Budget}."; return false; }
        problem = null;
        return true;
    }

    public override string ToString() => $"SPD {speed} AGI {agility} ACC {accuracy} HP {health} STA {stamina} JMP {jump}";
}
