// PIMLR #CAM (Nov 16): the Living Room's Leaderboards live "outside the window".
// Pressing Leaderboards on the Living Room menu closes the menu, pulls the camera back from over the player's
// shoulder, out through the middle pane of the triple window, to a spot just outside looking back in at the room;
// then the board opens. Back flies the camera in again to the over-the-shoulder view and re-opens the menu.
//
// The window pose is set for SceneStaticEU's "Living_Room_2". To re-aim it without code, add empty objects to the
// scene (they are optional; position + rotation are used as-is):
//   "PIMLR_LeaderboardCam"          where the camera ends up (blue Z arrow = where it looks)
//   "PIMLR_LeaderboardCam_Through"  a point inside the room the path bends toward, in line with the window
// The board's backdrop is also made more see-through while the camera is out there, so the room shows behind it.
//
// Wiring (no prefab edits): PlmrMainMenuPanel.OnLeaderboardButton -> Open, LeaderboardUI.OnBackButton -> TryClose.
// Anywhere without the hub camera (another scene) both fall back to the old instant open / close.
//
// Unreal note: a CameraActor outside the window; SetViewTargetWithBlend (EaseInOut ~1.2 s), add the leaderboard widget
// on blend finished (timer or a latent action), reverse on Back. A Level Sequence works too if the path needs a bend.

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PimlrLeaderboardCamera : MonoBehaviour
{
    public const string OutsideMarker = "PIMLR_LeaderboardCam";
    public const string ThroughMarker = "PIMLR_LeaderboardCam_Through";

    /// <summary>Just outside the middle pane of the triple window (SceneStaticEU).</summary>
    public static Vector3 OutsidePosition = new Vector3(2.0f, 2.9f, 2.6f);
    /// <summary>Inside the room, in line with that pane: the path bends toward it so the camera leaves through the glass, not the wall.</summary>
    public static Vector3 ThroughPoint = new Vector3(2.0f, 3.0f, -3.0f);
    /// <summary>Height on the player the outside camera looks at.</summary>
    public static float LookAtHeight = 1.5f;
    public static float FlyOutSeconds = 1.3f;
    public static float FlyBackSeconds = 1.05f;
    /// <summary>Opacity of the board's full-screen shade / its panel while the camera is outside (the scene's own values are restored on close).</summary>
    public static float ShadeAlpha = 0.1f;
    public static float PanelAlpha = 0.45f;
    /// <summary>Opacity of the black rankings box, and the board's scale, while the camera is outside.</summary>
    public static float ListAlpha = 0.72f;
    public static float BoardScale = 0.9f;

    static PimlrLeaderboardCamera runner;
    static bool busy, outside;

    Image shade, panel, list;
    Color shadeBefore, panelBefore, listBefore;
    Vector3 scaleBefore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { runner = null; busy = false; outside = false; }

    static PimlrLeaderboardCamera Runner
    {
        get
        {
            if (!runner) runner = new GameObject("PIMLR_LeaderboardCamera").AddComponent<PimlrLeaderboardCamera>();
            return runner;
        }
    }

    void OnDestroy() { if (runner == this) { runner = null; busy = false; outside = false; } }

    /// <summary>True if the camera move took over (the caller must not open the board itself).</summary>
    public static bool Open(GameObject menuPanel, LeaderboardUI board)
    {
        if (busy || outside) return true;
        if (!board || SceneManager.GetActiveScene().name != PimlrSessionFlow.HubScene) return false;
        if (PimlrHubCamera.Held || !PimlrHubCamera.Take()) return false;
        if (menuPanel) menuPanel.SetActive(false);
        Runner.StartCoroutine(Runner.OpenRoutine(board));
        return true;
    }

    /// <summary>True if the camera is outside (or on its way): the board is closed, the camera flies back, then afterReturn runs.</summary>
    public static bool TryClose(LeaderboardUI board, System.Action afterReturn)
    {
        if (busy) return true;            // mid-flight: swallow the press
        if (!outside || !runner) return false;
        runner.StartCoroutine(runner.CloseRoutine(board, afterReturn));
        return true;
    }

    IEnumerator OpenRoutine(LeaderboardUI board)
    {
        busy = true;
        Vector3 pos = OutsidePosition, through = ThroughPoint;
        Quaternion rot;
        var marker = GameObject.Find(OutsideMarker);
        var throughMarker = GameObject.Find(ThroughMarker);
        if (throughMarker) through = throughMarker.transform.position;
        if (marker)
        {
            pos = marker.transform.position;
            rot = marker.transform.rotation;
        }
        else
        {
            // Look back in at the player through the glass.
            var player = GameObject.FindGameObjectWithTag("Player");
            Vector3 target = player ? player.transform.position + Vector3.up * LookAtHeight : through;
            rot = Quaternion.LookRotation((target - pos).normalized, Vector3.up);
        }
        flownThrough = through;

        yield return PimlrHubCamera.Fly(pos, rot, 0f, FlyOutSeconds, through);

        if (board)
        {
            board.gameObject.SetActive(true);
            SeeThrough(board, true);
        }
        outside = true;
        busy = false;
    }

    Vector3 flownThrough;

    IEnumerator CloseRoutine(LeaderboardUI board, System.Action afterReturn)
    {
        busy = true;
        if (board)
        {
            SeeThrough(board, false);
            board.gameObject.SetActive(false);
        }
        yield return PimlrHubCamera.Fly(PimlrHubCamera.HomePosition, PimlrHubCamera.HomeRotation, PimlrHubCamera.HomeFov, FlyBackSeconds, flownThrough);
        PimlrHubCamera.Release();
        outside = false;
        busy = false;
        afterReturn?.Invoke();
    }

    // The board's own full-screen shade is ~86% black and its panel opaque: lighten both so the room shows.
    void SeeThrough(LeaderboardUI board, bool on)
    {
        if (on)
        {
            panel = board.GetComponent<Image>();
            var shadeT = board.transform.Find("BGShade");
            shade = shadeT ? shadeT.GetComponent<Image>() : null;
            if (panel) { panelBefore = panel.color; var c = panel.color; c.a = Mathf.Min(c.a, PanelAlpha); panel.color = c; }
            if (shade) { shadeBefore = shade.color; var c = shade.color; c.a = Mathf.Min(c.a, ShadeAlpha); shade.color = c; }
            var listT = board.transform.Find("Leaderboard Listings Panel");
            list = listT ? listT.GetComponent<Image>() : null;
            if (list) { listBefore = list.color; var c = list.color; c.a = Mathf.Min(c.a, ListAlpha); list.color = c; }
            scaleBefore = board.transform.localScale;
            board.transform.localScale = scaleBefore * BoardScale;
        }
        else
        {
            if (panel) panel.color = panelBefore;
            if (shade) shade.color = shadeBefore;
            if (list) list.color = listBefore;
            board.transform.localScale = scaleBefore;
            panel = null; shade = null; list = null;
        }
    }
}
