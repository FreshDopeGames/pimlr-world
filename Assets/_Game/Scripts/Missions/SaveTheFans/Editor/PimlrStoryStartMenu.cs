// PIMLR #STF (dev tool) — choose which Story stage YannicksWorld starts at when you press Play.
// Put this file in any Editor folder, e.g. Assets/_Game/Scripts/Missions/SaveTheFans/Editor/.
//
// Why: GameExecutionManager.Start() always begins at the saved stage (PlayerPrefs "currentZoneMode").
// A fresh save starts at Sirihanna / Level 1, so the water gun isn't unlocked yet. Ticking
// Save The Fans' "Begin On Start" doesn't change that: the mission and the Level 1 intro run together.
// Setting the saved stage to Save The Fans instead makes GameExecutionManager give you the water gun
// and start the mission itself, exactly as it does after beating Boss 1.
//
// Menu: PIMLR > Dev > Start Story At > ...   (the current choice has a check mark)
// Untick "Begin On Start" on SaveTheFans_Mission when you use this.

using UnityEditor;
using UnityEngine;

public static class PimlrStoryStartMenu
{
    const string Root = "PIMLR/Dev/Start Story At/";

    [MenuItem(Root + "Sirihanna (fresh start)", false, 0)]     static void Fresh()  => Set(Zone.ChatWilly);
    [MenuItem(Root + "Zone 1 (thugs)", false, 1)]              static void Z1()     => Set(Zone.Zone1);
    [MenuItem(Root + "Boss 1", false, 2)]                      static void B1()     => Set(Zone.ZoneBoss1);
    [MenuItem(Root + "Save The Fans (fan 1)", false, 3)]       static void Stf()    => Set(Zone.SaveTheFans, resetFans: true);
    [MenuItem(Root + "Save The Fans (resume saved fan)", false, 4)] static void StfResume() => Set(Zone.SaveTheFans);
    [MenuItem(Root + "Zone 2 (bombers)", false, 5)]            static void Z2()     => Set(Zone.Zone2);
    [MenuItem(Root + "Boss 2", false, 6)]                      static void B2()     => Set(Zone.ZoneBoss2);

    [MenuItem(Root + "Sirihanna (fresh start)", true)]         static bool vFresh() => Check("Sirihanna (fresh start)", Zone.ChatWilly);
    [MenuItem(Root + "Zone 1 (thugs)", true)]                  static bool vZ1()    => Check("Zone 1 (thugs)", Zone.Zone1);
    [MenuItem(Root + "Boss 1", true)]                          static bool vB1()    => Check("Boss 1", Zone.ZoneBoss1);
    [MenuItem(Root + "Save The Fans (fan 1)", true)]           static bool vStf()   => Check("Save The Fans (fan 1)", Zone.SaveTheFans);
    [MenuItem(Root + "Zone 2 (bombers)", true)]                static bool vZ2()    => Check("Zone 2 (bombers)", Zone.Zone2);
    [MenuItem(Root + "Boss 2", true)]                          static bool vB2()    => Check("Boss 2", Zone.ZoneBoss2);

    static void Set(Zone zone, bool resetFans = false)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PIMLR Dev] Stop Play mode first; the stage is read when YannicksWorld starts.");
            return;
        }
        ZoneProgress.Current = zone;
        if (resetFans) PlayerPrefs.DeleteKey(PIMLR.Missions.SaveTheFans.SaveTheFansMission.PrefNextHeat);
        PlayerPrefs.Save();
        Debug.Log($"[PIMLR Dev] YannicksWorld will start at {zone}" + (resetFans ? " (fan 1)." : "."));
    }

    static bool Check(string item, Zone zone)
    {
        Menu.SetChecked(Root + item, ZoneProgress.Current == zone);
        return true;
    }
}
