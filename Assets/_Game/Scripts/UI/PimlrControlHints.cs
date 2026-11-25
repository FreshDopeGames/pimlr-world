// PIMLR #HINT: one-off control hints over the player's head (PimlrTooltip), shown the first few times something
// happens, outside the formal tutorial. Self-bootstrapping and persistent across scenes; no scene edits.
//   * Getting on a bike:   controller (TapToPedal) "Tap [A] repeatedly to pedal", Triggers mode "Hold [RT] to pedal",
//                          keyboard "Hold [W] to pedal".
//   * Equipping water balloons: "Hold [RT] / [Left Mouse] to wind up, release to throw".
// Each hint counts its showings in PlayerPrefs (PIMLR_Hint_*) and stops after TimesToShow.
// Reset for testing: PimlrControlHints.ResetCounts().
//
// Unreal note: a small UGameInstanceSubsystem listening to the pawn's OnMountVehicle / OnWeaponEquipped delegates,
// with the counters in the SaveGame object; the text goes through the same overhead widget as PimlrTooltip.

using UnityEngine;

public class PimlrControlHints : MonoBehaviour
{
    public static int TimesToShow = 3;
    public static float BikeHintSeconds = 6f;
    public static float BalloonHintSeconds = 6f;

    const string BikeKey = "PIMLR_Hint_BikePedal";
    const string BalloonKey = "PIMLR_Hint_BalloonThrow";

    static PimlrControlHints instance;
    bool wasOnBike;
    float offBikeSince = -999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance) return;
        var go = new GameObject("PIMLR_ControlHints");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PimlrControlHints>();
    }

    public static void ResetCounts()
    {
        PlayerPrefs.DeleteKey(BikeKey);
        PlayerPrefs.DeleteKey(BalloonKey);
    }

    void OnEnable() => PimlrPlayerGrenades.EquippedChanged += OnBalloonsEquipped;
    void OnDisable() => PimlrPlayerGrenades.EquippedChanged -= OnBalloonsEquipped;

    void Update()
    {
        // PlayerOnBike is marked every frame by DriveVehicles; a short grace keeps a one-frame gap from counting
        // as getting off and on again.
        bool onBike = PimlrInput.PlayerOnBike;
        if (onBike && !wasOnBike && Time.unscaledTime - offBikeSince > 1f) OnMountBike();
        if (!onBike && wasOnBike) offBikeSince = Time.unscaledTime;
        wasOnBike = onBike;
    }

    void OnMountBike()
    {
        if (!TryCount(BikeKey)) return;
        string text;
        if (!PimlrInput.UsingGamepad)
            text = $"Hold {PimlrTooltip.Key("W")} to pedal";
        else if (PimlrInput.PedalMode == PimlrPedalMode.TapToPedal)
            text = $"Tap {PimlrTooltip.Key(PimlrInput.Label(PimlrButton.Jump))} repeatedly to pedal - tap faster to go faster";
        else
            text = $"Hold {PimlrTooltip.Key(PimlrInput.IsPlayStation ? "R2" : "RT")} to pedal";

        PimlrTooltip.ShowOverPlayer(text, BikeHintSeconds, until: () => !PimlrInput.PlayerOnBike);
    }

    void OnBalloonsEquipped(bool equipped)
    {
        if (!equipped || !TryCount(BalloonKey)) return;
        string fire = PimlrTooltip.Key(PimlrInput.Label(PimlrButton.Fire));   // RT / R2 / Left Mouse
        PimlrTooltip.ShowOverPlayer($"Hold {fire} to wind up, release to throw", BalloonHintSeconds,
            until: () => { var g = PimlrPlayerGrenades.Instance; return !g || !g.IsEquipped || g.IsCooking; });
    }

    static bool TryCount(string key)
    {
        int shown = PlayerPrefs.GetInt(key, 0);
        if (shown >= TimesToShow) return false;
        PlayerPrefs.SetInt(key, shown + 1);
        PlayerPrefs.Save();
        return true;
    }
}
