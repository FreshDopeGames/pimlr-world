// PIMLR #UI Slice 5: track availability + unlocking for the Pause screen's Music Library.
// The same rules as the existing store (MusicPurchaseEntry), in one place the new UI can ask:
//   owned       MusicData.isPurchased (PlayerPrefs by clip name, or isFree) - restored by SceneManagerScript.FetchMusicData
//   achievement achievementType + totalTargetAmount vs AuthManager's washes / police cars (lockedBtnMsg is its label)
//   price       MusicData.musicValue in PIMLR Coin (CoinManager)
// Buy() does exactly what MusicPurchaseEntry.OnPurchaseBtnClick does (coins, PlayerPrefs, MusicSystem list, refetch),
// plus an analytics event. No second ownership system.
//
// Unreal note: a UPimlrTrackStore subsystem over the track UDataAssets + the save game's owned-track set.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum PimlrTrackState
{
    Playing,          // unlocked and playing in the Music Library
    Unlocked,         // owned / free
    CanBuy,           // locked, achievement met, enough coins
    NeedCoins,        // locked, achievement met, not enough coins
    NeedAchievement,  // locked until an achievement (e.g. "10 Washes")
}

public static class PimlrTrackStore
{
    public static MusicData[] Catalog => SceneManagerScript.Instance ? SceneManagerScript.Instance.musicData : null;
    public static int Coins => CoinManager.Instance ? CoinManager.Instance.GetCoins() : 0;

    public static bool Owned(MusicData d) => d != null && (d.isPurchased || d.isFree);

    public static bool AchievementMet(MusicData d)
    {
        if (d == null || Owned(d)) return true;
        var a = AuthManager.Instance;
        if (!a) return true;
        switch (d.achievementType)
        {
            case AchievementType.EnemyWash: return d.totalTargetAmount <= a.enemyWashValue;
            case AchievementType.BossWash: return d.totalTargetAmount <= a.bossWashValue;
            case AchievementType.PoliceCar: return d.totalTargetAmount <= a.policeCarValue;
        }
        return true;
    }

    public static PimlrTrackState State(MusicData d, AudioClip libraryPlaying = null)
    {
        if (Owned(d)) return d.audioClip && d.audioClip == libraryPlaying ? PimlrTrackState.Playing : PimlrTrackState.Unlocked;
        if (!AchievementMet(d)) return PimlrTrackState.NeedAchievement;
        return Coins >= d.musicValue ? PimlrTrackState.CanBuy : PimlrTrackState.NeedCoins;
    }

    /// <summary>Projects in catalog order (first appearance).</summary>
    public static List<string> Projects()
    {
        var list = new List<string>();
        var c = Catalog;
        if (c != null) foreach (var d in c) { if (d == null || !d.audioClip) continue; var p = PimlrTrackInfo.Project(d); if (!list.Contains(p)) list.Add(p); }
        return list;
    }

    /// <summary>A project's tracks by track number (catalog order for ties).</summary>
    public static List<MusicData> Tracks(string project)
    {
        var c = Catalog;
        if (c == null) return new List<MusicData>();
        return c.Where(d => d != null && d.audioClip && PimlrTrackInfo.Project(d) == project)
                .Select((d, i) => (d, i)).OrderBy(x => x.d.trackNumber > 0 ? x.d.trackNumber : 10000 + x.i).Select(x => x.d).ToList();
    }

    public enum BuyResult { Bought, AlreadyOwned, NeedCoins, NeedAchievement, NoStore }

    public static BuyResult Buy(MusicData d)
    {
        if (d == null || !d.audioClip || !CoinManager.Instance || !SceneManagerScript.Instance) return BuyResult.NoStore;
        if (Owned(d)) return BuyResult.AlreadyOwned;
        if (!AchievementMet(d)) return BuyResult.NeedAchievement;
        int coins = Coins;
        if (coins < d.musicValue) return BuyResult.NeedCoins;

        // Same steps as MusicPurchaseEntry.OnPurchaseBtnClick.
        d.isPurchased = true;
        CoinManager.Instance.SetCoins(coins - d.musicValue);
        PlayerPrefs.SetInt(d.audioClip.name, 1);
        var ms = SceneManagerScript.Instance.musicSystem;
        if (ms != null && !ms.audioList.Contains(d.audioClip)) ms.audioList.Add(d.audioClip);
        SceneManagerScript.Instance.FetchMusicData();

        PIMLR.Analytics.PimlrAnalytics.Log("track_purchase", PimlrMusicAnalytics.TrackData(d)
            .Add("price", d.musicValue).Add("balance_after", coins - d.musicValue).Add("where", "pause_library"));
        return BuyResult.Bought;
    }
}
