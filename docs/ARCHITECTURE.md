# PIMLR — Technical Architecture Reference

> Audience: the new engineering team taking ownership of **PIMLR**.
> Project: `/Users/greenmachine2.0/pilr/YannickThurz-Merge_HR_Project`
> Engine: **Unity 6000.6.0f1 (Unity 6.6)**, **URP** (Universal Render Pipeline), primary build target **WebGL**.
> Scope: **PIMLR only.** This repo also contains two other, unrelated games that are **out of scope** for this team:
> **Helix** (`Assets/CarControllerwithShooting/`) and **Humanity Rocks** (`Assets/ARC/`). They are mentioned here only where
> their presence affects PIMLR (build settings, shared render pipeline assets).

All file paths below are absolute-from-repo-root unless noted. Line numbers reflect the source at the time of writing
(`main` @ `faa7235c`) and may drift — treat them as starting points, not contracts.

---

## 1. Overview

**PIMLR** is a third-person, downtown "water-gun" action game. The player boots into a login screen, drops into a
**living-room hub**, then enters a **downtown open zone** (`YannicksWorld`) where they talk to an NPC (Sirihanna), get a
water gun, fight waves of zombies through scripted zones and bosses, unlock a vehicle, and (optionally) play an endless
**Infinite Mode**. A music system doubles as a power-up engine: playing a track applies a temporary stat boost. Coins are
earned in combat and spent on music tracks; coins persist to a remote backend.

The game is built on three third-party / shared foundations:
- **Julhiecio TPS Controller (JUTPS)** — character movement, camera, weapons, AI, vehicles, input.
- **Local GLB avatar assets** — player avatars imported from `Assets/Avatars`.
- A custom **manager + singleton** layer (`AuthManager`, `SceneManagerScript`, `UIManager`, `GameExecutionManager`,
  `CoinManager`) plus shared **MusicSystem** and **DialogueSysyem** (sic — the folder is misspelled on disk).

### Single-player vs. multiplayer

PIMLR ships as a **single-player** experience. There is a **multiplayer remnant** in the codebase that is actively
*torn down* rather than used: `SceneStaticEU_Manager.OnEnable()`
(`Assets/_Game/Scripts/Manager/SceneStaticEU_Manager.cs:49–57`) destroys any live `SocketNetworkManager.Instance` and
`TS.Generics.AllScenes.instance` when the living room loads. Recent commits (`9933afa1`, `a5066336`, `faa7235c`)
reference Mirror-based multiplayer and a lobby scene, but **none of the four live PIMLR scenes** drive a multiplayer flow.
Treat the socket/lobby code as legacy that must be defended against (the explicit `Destroy()` calls exist precisely to
stop stale networking singletons from contaminating single-player state).

### The 3-games repo context

`ProjectSettings/EditorBuildSettings.asset` lists scenes for all three games. Only PIMLR's are enabled:

| Build idx | Scene | Enabled | Owner |
|----------:|-------|:-------:|-------|
| 0 | `Assets/_Game/Scene/Pimlr/00_MainMenu.unity` | ✅ | **PIMLR** |
| 1 | `Assets/_Game/Scene/Pimlr/03_LevelSelection.unity` | ❌ | **PIMLR** (present, disabled) |
| 2 | `Assets/_Game/Scene/Pimlr/SceneStaticEU.unity` | ✅ | **PIMLR** |
| 3 | `Assets/_Game/Scene/Pimlr/YannicksWorld.unity` | ✅ | **PIMLR** |
| 4+ | `Assets/CarControllerwithShooting/Scenes/*` | ❌ | Helix (out of scope) |
| later | `Assets/ARC/Assets/Scenes/*` | ❌ | Humanity Rocks (out of scope) |

> The two other games' assets are large (ARC ~GBs, CarControllerwithShooting hundreds of MB) and inflate the repo, but
> they are disabled in build and out of scope. The only place they leak into PIMLR is the shared URP/quality settings —
> see §5 and §7.

---

## 2. Project Structure (PIMLR-relevant folders)

```
Assets/
├── _Game/                              # PIMLR's own code, scenes, prefabs, art
│   ├── Scene/Pimlr/                    # The 4 live scenes (+ several backups/old scenes, NOT in build)
│   ├── Scripts/
│   │   ├── Manager/                    # AuthManager, SceneManagerScript, UIManager,
│   │   │                               #   SceneStaticEU_Manager, Singleton, PlayerManager, SceneHandler
│   │   ├── Level/LevelInit.cs          # Per-scene bootstrap (instantiates managers from Resources)
│   │   ├── UI/                         # Login, signup, loading, fader, stamina bar, level-complete...
│   │   │   └── PMLR/                   # UI_GoalPanel, PlmrMainMenuPanel, PlmrAvatarCreatePanel...
│   │   ├── Other/                      # BoundsRestriction (#5), FMVSequencer (#9),
│   │   │                               #   MusicInputHandler (#8), Ai Chat Interaction, MinimapBlipController
│   │   ├── Customization/              # CodeMonkey-style color/material swap (living-room preview)
│   │   ├── GameReadyPlayerMe/          # Local GLB avatar loading compatibility scripts
│   │   └── Enum/EnumManager.cs         # GameMode / GameScene enums
│   ├── CC-CharaterCustomization/       # Mesh-swap customization data model (CodeMonkey)
│   ├── Resources/                      # Prefabs instantiated by name (UI, AuthManager, PIMLR_UI, Loading Screen...)
│   ├── Prefabs/ Textures/ Materials/   # PIMLR art + the player prefab (My Player)
│
├── AddedImplements/
│   ├── Scripts/GameExecutionManager.cs # Zone/combat/vehicle orchestration (the gameplay brain)
│   └── coinmanager/CoinManager.cs      # Coin economy + server sync
│
├── Scripts/InfiniteMode.cs             # Endless wave spawner
│
├── Julhiecio TPS Controller/           # JUTPS: controller, camera, weapons, AI, vehicles, input (third-party)
│   ├── Scripts/Gameplay/...            #   JUCharacterController, Damager, ItemSwitchManager, Weapon, DriveVehicles
│   ├── Scripts/Libraries/...           #   JUCharacterControllerCore (sprint stamina, punch dmg), JUVehicleEngine
│   ├── Scripts/AI/...                  #   JUCharacterArtificialInteligenceBrain, ZombieAI, PatrolAI
│   └── Inputs/JUTPSInputControlls.*    #   InputSystem actions + generated (baked) C# wrapper
│
├── Avatars/                            # Local GLB avatar files used by the player loader
├── MusicSystem/Script/MusicSystem.cs   # Music playback + boost triggering (shared asset)
├── DialogueSysyem/                     # Baked dialogue system (ScriptableObject trees + event system)
│   ├── Scripts/                        #   Dialogue, DialogueManager, DialogueScriptable, Response, ResponseHandler
│   ├── EventSystem/                    #   BaseGameEvent<T>, VoidEvent, DialogueScriptableEvent, Vector3Event
│   └── YannickWorld/                   #   PIMLR dialogue assets + DialogueAwakener (NPC trigger)
│
├── FlowiseAPI.cs  ChatbotUIController.cs  # Remote LLM chat for Sirihanna (legacy/optional path; root-level)
├── Constant.cs                         # Scene names, game URLs, coin meta-key (root-level)
├── Editor/PimlrBuildScript.cs          # Headless WebGL build entry point
├── StreamingAssets/                    # Loadingvideo.mp4, PLMRVideo.mp4 (FMV)
├── WebGLTemplates/                     # Better2020 (splash video), Responsive
│
├── CarControllerwithShooting/          # ── Helix (OUT OF SCOPE) ──
└── ARC/                                # ── Humanity Rocks (OUT OF SCOPE) ──
```

**Two things to internalize early:**
1. PIMLR code is split across **three** roots: `Assets/_Game/`, `Assets/AddedImplements/`, and `Assets/Scripts/`
   (plus a few root-level files like `Constant.cs`, `FlowiseAPI.cs`, `ChatbotUIController.cs`). The single most important
   gameplay class — `GameExecutionManager` — lives under `AddedImplements/`, not `_Game/`.
2. The folder `DialogueSysyem` is **misspelled on disk**. Don't "fix" it casually; meta GUIDs and scene references depend
   on the path.

---

## 3. Scenes

PIMLR has three build scenes. Scene transitions should always load by name rather than relying on build indices.

### 3.1 `00_MainMenu` (build 0) — startup / login

- **Role:** App entry; login/signup UI. In the current demo build it is **auto-skipped**.
- **Key managers:** `AuthManager` (instantiated via `LevelInit` from `Resources/AuthManager.prefab`), `UIManager`,
  `SceneManagerScript`.
- **Flow:** `AuthManager.Start()` (`AuthManager.cs:58–62`) checks `SceneManager.GetActiveScene().name == "00_MainMenu"`
  and immediately calls `DummyLogin()` (PIMLR #2). `DummyLogin()` (`AuthManager.cs:138–144`) writes dummy
  `username/password` PlayerPrefs and calls `SceneManagerScript.Instance.LoadScene("SceneStaticEU")`. The login panel
  never appears under normal startup.
- A real login path still exists: `UILogin.OnLogin()` → `AuthManager.LoginUser(email, password)` → POST
  `https://www.idea-labs.xyz/api/login` → `HandleLoginResponse()` (`AuthManager.cs:145–177`) stores the bearer `token`
  and loads `SceneStaticEU`. On any network failure it falls back to `DummyLogin()`.

### 3.2 `SceneStaticEU` (build 2) — living room / hub

- **Role:** Safe hub. Player spawns frozen, customizes the avatar, can pick game modes, and transitions to gameplay.
- **Manager:** `SceneStaticEU_Manager` (`Assets/_Game/Scripts/Manager/SceneStaticEU_Manager.cs`).
  - `Start()` (line ~29) sets `characterController.BlockHorizontalInput = true` — player is locked on spawn.
  - `OnEnable()` (lines 41–57) subscribes to `CoinManager.coinValueChanged` for the coin display **and destroys**
    `SocketNetworkManager.Instance` and `TS.Generics.AllScenes.instance` if present (multiplayer cleanup).
  - `SceneLoad(string sceneToLoad)` (line ~93) hides menus, re-enables the controller (`PlayerControllerStart()`),
    and calls `SceneManagerScript.Instance.LoadScene(sceneToLoad)`.
- **UI:** `PlmrMainMenuPanel` (zone/game-mode selection, also routes to the out-of-scope Helix/HR via
  `Constant.Helix_Scene_Name` / `Constant.Humanity_Scene_Name`), `PlmrAvatarCreatePanel` (customization).
- **Avatar:** Local GLB avatar selection from `Assets/Avatars` plus a CodeMonkey-style color/mesh customization preview (see §4.5).

### 3.3 `YannicksWorld` (build 3) — downtown gameplay

- **Role:** The main play space. Zone progression, enemy/boss combat, vehicle chase, infinite mode.
- **Manager:** `GameExecutionManager` (`Assets/AddedImplements/Scripts/GameExecutionManager.cs`) — the gameplay brain.
- **Other key objects:** the player prefab (JUTPS + local GLB avatar, tagged `Player`), `zombieSpawner` (`JUAutoInstantiate`),
  `zombieBoss`, `waterBallonzombie`, `PlayerCar`/`AiCar` (vehicles, initially disabled), `ai_Chat_Triger` (NPC trigger),
  `MovementLeash` (a `BoundsRestriction`, PIMLR #5, must be wired in the scene), the music system, and the goal HUD.
- Zone behavior is driven by `PlayerPrefs["currentZoneMode"]` parsed into the `Zone` enum at `Start()` — see §4.2.

### 3.4 Legacy scenes — excluded

- Legacy scenes are present in the project but excluded from the build. The original pre-gameplay zone
  selector; the demo flow replaced it with in-hub selection (`PlmrMainMenuPanel`). Kept for reference; not in the live
  flow. Several other non-build scenes exist in the folder (`01_Game`, `02_CharacterSelection`, `04_Pimlrp`,
  `DemoBuild`, `YannicksWorld backup 15_08`, `03_LevelSelection_Old`) — all legacy, none in `EditorBuildSettings`.

### 3.5 Scene-flow graph

```
                         ┌─────────────────────────┐
                         │  00_MainMenu (build 0)   │
                         │  AuthManager.Start()     │
                         │  → DummyLogin()  [#2]    │   (real login path exists but is bypassed)
                         └────────────┬─────────────┘
                                      │ LoadScene("SceneStaticEU")
                                      ▼
        ┌──────────────────────────────────────────────────────────────┐
        │ SceneManagerScript.LoadSceneCoroutine()                       │
        │  • ShowMenu("Loading Screen")                                 │
        │  • [#9] PlayInterLevelFMVCoroutine() if playInterLevelFMV     │
        │  • LoadSceneAsync(name); wait progress≥0.9; activate          │
        └────────────┬─────────────────────────────────────────────────┘
                      ▼
        ┌─────────────────────────────────────┐
        │ SceneStaticEU (build 2) — hub        │
        │  • player frozen on spawn            │
        │  • destroy stale Socket/AllScenes    │  ← multiplayer remnant cleanup
        │  • customize avatar / pick mode      │
        └────────────┬────────────────────────┘
                      │ SceneStaticEU_Manager.SceneLoad("YannicksWorld")
                      │   (PlayerPrefs["currentZoneMode"] set by PlmrMainMenuPanel.OnSelectLevel)
                      ▼
        ┌─────────────────────────────────────────────────────────────┐
        │ YannicksWorld (build 3) — gameplay                          │
        │  GameExecutionManager.Start() reads currentZoneMode →        │
        │   ChatWilly → Zone1 → ZoneBoss1 → Zone2 → ZoneBoss2          │
        │                                   └ (or InfiniteMode)        │
        │  BoundsRestriction clamps player until UnlockVehicle()       │
        └─────────────────────────────────────────────────────────────┘
              ▲                                          │
              └──────── SceneLoad("SceneStaticEU") ──────┘ (return to hub / reload zone)

   03_LevelSelection (build 1) is DISABLED and not in this flow.
```

---

## 4. Systems (deep)

### 4.0 Singleton base

`Assets/_Game/Scripts/Manager/Singleton.cs` is the generic base for all persistent managers:

```csharp
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour {
    public static T Instance { get; }          // static accessor
    public virtual void Awake() {
        if (_instance == null) { _instance = this as T; if (dontdestry) DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);              // duplicate guard
    }
}
```

Used by `AuthManager`, `SceneManagerScript`, `GameExecutionManager`, `CoinManager`, `InfiniteMode`.
**Gotcha:** persistence is gated by a serialized `dontdestry` bool per instance. If a manager is *not* marked, it does
not survive a scene load and must be re-instantiated by `LevelInit`.

---

### 4.1 Boot / Auth / Scene management

**Files:** `AuthManager.cs`, `SceneManagerScript.cs`, `UIManager.cs`, `LevelInit.cs`, `Constant.cs`.

**`LevelInit`** (`Assets/_Game/Scripts/Level/LevelInit.cs`) runs in `Awake()` on a scene root and conditionally
instantiates managers from `Resources/` only if they don't already exist (`FindObjectOfType<T>()` guard): `UI.prefab`,
`PIMLR_UI.prefab`, `AuthManager.prefab`. This is how each scene self-bootstraps without duplicating singletons.

**`AuthManager`** (`Singleton<AuthManager>`):
- `baseUrl = "https://www.idea-labs.xyz/api/"` (line 10). Endpoints: `register`, `login`, `logout`, `get-activity`,
  `save-activity`. Activity endpoints attach `Authorization: Bearer {token}`.
- `Start()` → `DummyLogin()` only on `00_MainMenu` (#2).
- `RegisterUser()` awards +50 coins on success (`AuthManager.cs:111`).
- `currentGameMode` (default `GameMode.IdeaLabs`) drives loading-screen art and routing.
- `PowerChange(AchievementReward)` / `ResetPowerups()` — applies/clears live stat boosts (see §4.3).
- Achievement counters `enemyWashValue`/`bossWashValue`/`policeCarValue` are **PlayerPrefs-backed properties**
  (`AuthManager.cs:20–53`), keyed by `AchievementType.ToString()`.

**`SceneManagerScript`** (`Singleton<SceneManagerScript>`) — the scene loader **and** the central UI/service registry.
It holds references to `uiManager`, `loadingScreen`, `goalPanel`, `musicSystem`, `minimapBlipController`,
`musicPurchasePanel`, `musicData[]`, and the loading/FMV `VideoPlayer`s.

- `LoadScene(string sceneName = "")` (lines 56–68): resets `Time.timeScale = 1`, unlocks the cursor, and (if not already
  loading) starts `LoadSceneCoroutine` + `WaitForScreenFadeOut`.
- `LoadSceneCoroutine` (lines 71–148): shows the loading screen → **[#9] optional FMV** → `LoadSceneAsync` with
  `allowSceneActivation = false` → drive the loading slider until `progress ≥ 0.9` → activate → close loading UI.
- **Empty-name behavior (gotcha):** with no `sceneName` it calls `LoadSceneAsync(buildIndex + 1)` (line 99). Because
  index 1 (`03_LevelSelection`) is disabled, relying on `buildIndex+1` is fragile. **PIMLR always loads by name**
  (`"SceneStaticEU"`, `"YannicksWorld"`), so this branch is effectively dead in the live flow — but don't reintroduce
  no-arg loads.
- `Start()` (lines 225–245) wires the loading/wave videos to `StreamingAssets/Loadingvideo.mp4` and sets cursor state.
- `OnEnable()` → `FetchMusicData()` reads per-track purchase flags from PlayerPrefs (keyed by `audioClip.name`) and
  refreshes the playable list (lines 257–269).

**`UIManager`** (NOT a singleton — plain `MonoBehaviour` from `UI.prefab`): holds a serialized `UI_Screen[] UIMenus`
(name → GameObject). `ShowMenu(name, disableAll)` / `CloseMenu(name)` / `DisableAllScreens()` + a `UIFader`.
Notable index dependency: `GameExecutionManager` and `DialogueAwakener` reach the level-complete popup via
`uiManager.UIMenus[5].UI_Gameobject` — a **hardcoded array index**. Reordering `UIMenus` in the inspector breaks this.

**System map (boot/scene):**

```
LevelInit.Awake ──instantiates──▶ AuthManager / UIManager / PIMLR_UI  (from Resources/, guarded)
        │
AuthManager.Start ──#2──▶ DummyLogin ──▶ SceneManagerScript.LoadScene("SceneStaticEU")
        │                                         │
        │                                LoadSceneCoroutine ──[#9]──▶ FMV ──▶ LoadSceneAsync ──▶ activate
        ▼
SceneManagerScript (registry): uiManager, goalPanel, musicSystem, loadingScreen, musicData[], fmv*
```

---

### 4.2 Sirihanna dialogue + Zone progression

This is the spine of PIMLR's gameplay loop. There are **two** dialogue mechanisms in the repo; understand which one is
live.

**Live trigger:** `DialogueAwakener` (`Assets/DialogueSysyem/YannickWorld/DialogueAwakenerArea/DialogueAwakener.cs`)
on the Sirihanna NPC collider.
- Fields: `dialogueScript` (a `DialogueScriptable`), `RequiredCorrectAnswers`, `PlayerPoints`, `isInteracted`.
- `Start()` (lines 29–51) handles **scene-reload resume**: if `PlayerPrefs["currentZoneMode"] == "Zone1"` it calls
  `EndDialogBoxAndSpawnZonbies()` directly (skip dialogue); if `== "Zone2"` it calls
  `GameExecutionManager.Instance.BossDead()` (skip Zone1 boss). This is how the game resumes mid-progression.
- `OnTriggerEnter` (line 58): when the `Player` enters, `PlayerPoints = 0`, freezes the controller
  (`PlayerControllerStop()`), and opens the dialogue.
- Response callbacks (wired in dialogue assets via the event system): `IncreasePoints()`, `IncreasePointsAndEnd()`,
  `WrongAnswerAndEnd()`, `EndDialogBox()`, and the key one:
- **`EndDialogBoxAndSpawnZonbies()`** (lines 144–172) — the progression unlock:
  ```
  DialogueManager.instance.EndDialogue();
  GameExecutionManager.Instance.currentZoneMode = Zone.Zone1;          // + persist to PlayerPrefs
  StartCoroutine(WaitForscreenfadeOut());                              // show "Zone Panel" then "JUTPS Interface"
  PlayerControllerStart();                                             // unfreeze
  characterController.GetComponent<ItemSwitchManager>().IsPlayer = true; // unlock weapon switching (= gun)
  GameExecutionManager.Instance.zombieSpawner.gameObject.SetActive(true);
  CoinManager.Instance.SetCoins(CoinManager.Instance.GetCoins() - 40);   // 40-coin entry cost
  ```
  **The gun "unlock" is implicit**: there is no `AddWaterGun()` call here — equipping is gated solely by
  `ItemSwitchManager.IsPlayer = true`. (A leftover `AddWaterGun()` exists in `GameExecutionManager` but is unused on this
  path.)

**Backing dialogue framework** (`Assets/DialogueSysyem/Scripts/`): `DialogueScriptable` (lines + `Response[]`),
`Dialogue` (typewriter coroutine + branching), `DialogueManager` (singleton `StartDialogue`/`EndDialogue`),
`Response` (text + `VoidEvent`/`DialogueScriptableEvent`/`Vector3Event` hooks + `isEvent`), `ResponseHandler` (spawns
choice buttons). Events use `BaseGameEvent<T>` (`Assets/DialogueSysyem/EventSystem/`). PIMLR's Sirihanna conversation
assets live under `YannickWorld/DialoguePackOne/` (Dialogue1..15) with paired event assets like
`DialogsEnd&SpawnZombiesEvents.asset`.

**Legacy/optional AI chat path:** `AiChatInteraction` (`Assets/_Game/Scripts/Other/Ai Chat Interaction.cs`) opens an
`ai_BOT_Canvas` driven by `ChatbotUIController.cs` + `FlowiseAPI.cs` (remote LLM). `OnClosePanel()` calls
`Application.ExternalCall("stopSpeaking")` (WebGL JS interop) and re-enables the controller. This is the original online
chat; the demo direction is to use the **baked** DialogueSystem instead (less network fragility). See §6.

**`GameExecutionManager` — the zone state machine** (`AddedImplements/Scripts/GameExecutionManager.cs`):

The `Zone` enum is defined in `Assets/_Game/Scripts/UI/UILevelCompletePopUp.cs`:
`Zone1, ZoneBoss1, Zone2, ZoneBoss2, ChatWilly, InfiniteMode`.

`Start()` (lines 72–134):
- Parses `PlayerPrefs["currentZoneMode"]` → `currentZoneMode` (default `ChatWilly` if parse fails).
- Caches the level-complete popup via `uiManager.UIMenus[5]` (the hardcoded index).
- Disables both cars and the chat trigger, then branches per zone:
  - **ChatWilly:** lock weapon (`ItemSwitchManager.IsPlayer = false`), set `currentZoneMode = Zone1`, enable
    `ai_Chat_Triger` (so the next dialogue completion drives Zone1).
  - **Zone1:** unlock weapon, fade in goal "0/10", `Invoke("Zone1Start", 3)`.
  - **ZoneBoss1:** unlock weapon, "0/1", `Invoke("SpawnBoss", 3)`.
  - **Zone2:** unlock weapon, "0/10", `StartCoroutine(Zone2Start())`.
  - **ZoneBoss2:** unlock weapon, `StartCoroutine(Zone2Boss())`.
  - **InfiniteMode:** unlock weapon, enable `infiniteMode` object.

`Update()` (lines 137–222) is the progression driver:
- ESC / mouse-click toggle cursor lock (lines 139–159).
- When `zombiesKilled >= 10` (line 161): reset counter, stop the spawner, destroy spawned zombies. Then:
  - If **not** `zone2Start`: advance to `ZoneBoss1`, persist, complete `Zone1_Enemy_Battle` goal,
    `Invoke("SpawnBoss", 10f)`.
  - If `zone2Start`: set `zone2Finish`, wire the minimap car blips, enable both cars, run the camera cinematic
    (`MoveToTargetAndBack`), complete `Zone2_Enemy_Battle`, advance to `ZoneBoss2`, persist.
- When `zone1Finish` (set by `BossDead()`): complete `Zone1_Boss_Battle`, advance to `Zone2`, start `Zone2Start()`.

Combat hooks:
- `OnKillEnemy()` (lines 276–293): `zombiesKilled++`, update goal text, award coins (**+10/kill in Zone2**, **+5
  otherwise**).
- `BossDead()` (lines 296–299): sets `zone1Finish = true`.
- `SpawnBoss()`, `Zone1Start()`, `Zone2Start()`, `Zone2Boss()` handle equipping, spawner config, and goal toggles.
- `Zone2Start()` swaps the spawner prefab to `waterBallonzombie` and sets `zone2Start = true` (which flips the coin
  multiplier).

**Goal HUD:** `UI_GoalPanel` (`Assets/_Game/Scripts/UI/PMLR/UI_GoalPanel.cs`) tracks a `GoalList` enum
(`ChatWithSirihanna, Zone1_Enemy_Battle, Zone1_Boss_Battle, Zone2_Enemy_Battle, EnemyCarDestroy`) via toggles, shows the
kill counter, and animates the music-boost popup (DOTween). It also resets active power-ups on disable/destroy
(`PowerChange(Nothing)`).

**System map (dialogue/zone):**

```
Player enters NPC trigger ─▶ DialogueAwakener.OnTriggerEnter ─▶ DialogueManager.StartDialogue
                                                                         │ (typewriter + responses)
   correct answers (IncreasePoints…) ──▶ EndDialogBoxAndSpawnZonbies ───┤
                                                                         ▼
   currentZoneMode=Zone1 (persist)  •  IsPlayer=true (gun)  •  spawner ON  •  coins −40  •  goal ✓ ChatWithSirihanna
                                                                         │
                          GameExecutionManager.Update(): kills≥10 ─▶ ZoneBoss1 ─▶ Zone2 ─▶ ZoneBoss2
                                                                         │
                                          BossDead()/OnKillEnemy() ◀────┘  (coins +5 / +10, goals toggle)
```

**Gotcha:** zone state is a stringified enum in PlayerPrefs. Renaming `Zone` members breaks every existing save (parse
fails → silently defaults to `ChatWilly`).

---

### 4.3 Music + attribute boosts + economy

**Files:** `Assets/MusicSystem/Script/MusicSystem.cs`, `MusicData` (defined in `SceneManagerScript.cs:284–299`),
`Assets/_Game/Scripts/Other/MusicInputHandler.cs` (#8), `AuthManager.PowerChange`,
`Assets/AddedImplements/coinmanager/CoinManager.cs`.

**MusicSystem** registers itself on `SceneManagerScript.Instance.musicSystem` in `Start()`, builds `audioList` from
`musicData` filtered by `isPurchased`, and plays/pauses/skips tracks. Each track carries boost metadata in `MusicData`:
`boostname`, `boostInfo`, `achievementType`, `achievementReward`, `musicValue` (price), `isFree`.

Boost flow on play / skip:
- `PlayMusic()` (pause path) and the three skip methods (`ChangeMusicForward`/`ChangeMusicBackward`/`ChangeMusic`)
  drive `goalPanel.StartBoost(boostname, boostInfo, achievementReward)`, which calls
  `AuthManager.PowerChange(reward)`.
- **PIMLR #4** added `AuthManager.PowerChange(AchievementReward.Nothing)` + hide the booster panel before applying a new
  boost, so boosts no longer **stack** across track switches. (Verify each skip method clears first — this was the bug.)

**`AuthManager.PowerChange(reward)`** (`AuthManager.cs:359–402`) calls `ResetPowerups()` then applies one effect against
live JUTPS objects (so if `GameExecutionManager.Instance` is null, boosts silently no-op):
- `MovementSpeed` → `playerHandler.jUCharacterController.Speed = 6` (baseline 3)
- `VehicleSpeed` → `carController.VehicleEngine.MaxVelocity = 600`, `TorqueForce = 1200` (baseline 300/600)
- `MaxHP` → `CharacterHealth.MaxHealth = Health = 150` (baseline 100)
- `FreezeRay` → stub (no effect here; the actual freeze is implemented in `JUHealth.DoDamage`)
- `ResetPowerups()` (lines 403–414) restores Speed 3 / 300 / 600 / MaxHealth 100.

**`MusicInputHandler`** (#8) reads devices **directly** (not via the generated input wrapper):
`Gamepad.current` LB = previous, RB = next, Start = play/pause; keyboard fallback Q/E/P (`MusicInputHandler.cs:59–73`).
The comment in the file explains why: the baked `JUTPSInputControlls.cs` wrapper is generated only inside the Editor, so
new actions aren't visible at runtime — direct `Gamepad`/`Keyboard` reads guarantee the controls work.

**Economy — `CoinManager`** (`Singleton<CoinManager>`, `AddedImplements/coinmanager/CoinManager.cs`):
- `Awake()` (line 41) base-inits then `StartCoroutine(AuthManager.Instance.GetUserData(Constant.coinKey, ...))` to load
  the server balance (`meta_key = "coin"`).
- The `Coins` property setter (lines ~12–22) calls `sendDataToServer()` (POST `save-activity`) **and** raises
  `Action<int> coinValueChanged` on **every** change. UI (`SceneManagerScript._coins`, `SceneStaticEU_Manager`,
  `PlmrMainMenuPanel`) subscribes to update displays.
- `SetCoins(int)` clamps to ≥0; `GetCoins()` returns the cached value.
- **Gotcha:** no batching — rapid kills fire one HTTP POST each. If the server is down, the local value still updates and
  the remote sync fails silently (gameplay never blocks on it).

**System map (music/economy):**

```
MusicInputHandler (gamepad/keyboard) ─▶ MusicSystem.ChangeMusic*/PlayMusic
        │                                          │
        │                          [#4] PowerChange(Nothing) + hide panel  (clear old boost)
        ▼                                          ▼
   UI_GoalPanel.StartBoost ─▶ AuthManager.PowerChange(reward) ─▶ live JUTPS stat change
        ▲
CoinManager.SetCoins ─▶ setter ─▶ sendDataToServer (POST save-activity) + coinValueChanged ─▶ UI text
        ▲
GameExecutionManager.OnKillEnemy (+5/+10) ; InfiniteMode.CompleteWave (+50×wave) ; DialogueAwakener (−40)
```

---

### 4.4 Player / combat / vehicle (JUTPS)

PIMLR delegates the heavy lifting to **Julhiecio TPS Controller**. The PIMLR-specific tweaks are small overrides on top.

**Character controller:** `JUCharacterController` (`.../Gameplay/Character Controllers/JUCharacterController.cs`) extends
`JUCharacterBrain` (`.../Libraries/Character Controller Libs/JUCharacterControllerCore.cs`). Movement runs in
`FixedUpdate → Movement()`; state/aim/weapons in `Update`. PIMLR uses the `Block*Input`, `CanMove/Jump/Rotate`,
`EnableRoll`, `UseDefaultControllerInput` flags to freeze the player during dialogue/menus (see
`AiChatInteraction.PlayerControllerStop`, `SceneStaticEU_Manager`).

**PIMLR-specific overrides:**
- **#13 punch damage:** at init, if the character is tagged `Player`, `RightHandDamager.Damage` / `LeftHandDamager.Damage`
  are set to `5` (`JUCharacterControllerCore.cs` ~lines 304–309). Enemies keep defaults (Damager default 20). Applied
  once in `Awake` — changing the tag later won't retro-apply.
- **#11 stamina UI:** `JUCharacterControllerCore` exposes `NormalizedSprintStamina` (0..1) and `CanSprintNow`;
  `UIStaminaBar` (`Assets/_Game/Scripts/UI/UIStaminaBar.cs`) lerps a filled `Image` to it and tints red when depleted.
  It auto-resolves the player via `GameExecutionManager.Instance.playerHandler.jUCharacterController`.

**Health/damage:** `JUHealth.DoDamage(damage, hitPosition)` is the single damage sink. It consults
`AuthManager.Instance.achievementReward`: `Defense` halves damage to "My Player"; `FreezeRay` freezes non-player targets
(special-cased branch for the police car by GameObject name). `OnDeath` UnityEvent fires the kill hooks (e.g.,
`GameExecutionManager.OnKillEnemy`). Melee damage is gated by the `MeleeAttack` state-machine behaviour (enables damagers
between normalized times 0.15–0.8).

**Vehicles:** `DriveVehicles` (`.../Gameplay/Abilities/DriveVehicles.cs`) handles enter/exit and seat IK; vehicle physics
via `JUVehicleEngine`/`CarController`. **#12:** `UnequipWeaponOnEnter = false` by default — entering a vehicle now keeps
the water gun equipped (set true to restore the original holster-on-enter behavior). Boosts modify
`carController.VehicleEngine.MaxVelocity/TorqueForce` (see §4.3).

**#5 Movement leash — `BoundsRestriction`** (`Assets/_Game/Scripts/Other/BoundsRestriction.cs`):
- A scene-wired component that clamps the player to a world-space AABB (`center`/`size`, X/Z by default; Y optional) in
  `LateUpdate` while `GameExecutionManager.vehicleUnlocked == false`.
- Subscribes to `GameExecutionManager.onVehicleUnlocked` in `OnEnable`; disables itself when the event fires **or** if
  `vehicleUnlocked` is already true on enable (re-entry safe).
- Player transform auto-resolves from `GameExecutionManager.Instance.playerHandler.jUCharacterController` if not assigned.
- `GameExecutionManager.UnlockVehicle()` (lines 64–70) flips the flag and invokes the event **once**.
- **Gotcha:** the leash is a manual scene wiring step (an empty GO named e.g. `MovementLeash` with this component, sized
  via the gizmo). If it isn't in the scene, vehicle unlock has no movement effect. If `UnlockVehicle()` is never called
  (e.g., dialogue never completes), the player stays clamped.

**AI:** `JUCharacterArtificialInteligenceBrain` + `ZombieAI`/`PatrolAI` drive enemy pathfinding/combat. Enemies must have
`ItemSwitchManager.IsPlayer = false` so they don't switch weapons mid-fight.

**System map (player/combat/vehicle):**

```
Input (JUInput / InputSystem) ─▶ JUCharacterController (move/aim/shoot)
                                       │
   Weapon.Fire ─▶ Bullet ─▶ JUHealth.DoDamage ──(achievementReward: Defense/FreezeRay)──┐
   MeleeAttack SMB ─▶ Damager(Player=5 [#13]) ─▶ JUHealth.DoDamage ──────────────────────┤
                                                                                          ▼
                                              OnDeath ─▶ GameExecutionManager.OnKillEnemy (coins, goal)
DriveVehicles (#12 keep weapon) ─▶ CarController/JUVehicleEngine
BoundsRestriction (#5) clamps player ◀── vehicleUnlocked flag ── UnlockVehicle() ─▶ onVehicleUnlocked
UIStaminaBar (#11) ◀── NormalizedSprintStamina
```

---

### 4.5 UI / customization / local avatars

**UI dispatch:** `UIManager` (named-panel show/hide, see §4.1). Loading art is mode-aware via `UILodingScreen`
(`LoadingBarData[]` indexed by `GameMode`). `UIFader` does fades. `UILevelCompletePopUp` defines the `Zone` enum and the
level-complete popup (reached via the hardcoded `UIMenus[5]`).

**Customization — two parallel systems (a known seam):**
1. **CodeMonkey mesh/color swap** (`Assets/_Game/CC-CharaterCustomization/Scripts/PlayerCharacterCustomized.cs` +
   `Assets/_Game/Scripts/Customization/Customization_Handler.cs` / `Customization_Applier.cs`): fast living-room preview.
   Color/mesh selections persist in PlayerPrefs keyed by renderer name; a hardcoded `DEFAULT_SAVE_JSON` provides the base
   preset. `Customization_Applier.ApplyCustomization()` writes colors onto renderer materials on start.
2. **Local GLB avatars** (`Assets/_Game/Scripts/GameReadyPlayerMe/LocalAvatarLoader.cs`): editor-populated references to
   `.glb` assets under `Assets/Avatars`; the selected model is instantiated locally with no avatar-service dependency.

**Gotcha:** imported GLBs must use a humanoid rig if JUTPS hand/foot IK and humanoid bone lookup are required. Non-humanoid
avatars can still render, but weapon and foot-placement systems will remain disabled by their runtime guards.

**System map (UI/customization):**

```
UIManager.ShowMenu(name) ──▶ UI_Screen[] (Login_Panel, Loading Screen, JUTPS Interface, Zone Panel, CharaterCreatePanel…)
PlmrMainMenuPanel.OnSelectLevel ─▶ PlayerPrefs["currentZoneMode"] ─▶ LoadScene("YannicksWorld")

Customization_Handler/Applier (CodeMonkey color+mesh, PlayerPrefs)        ┐  (NOT synced)
LocalAvatarLoader (local GLB catalog ─▶ instantiate selected avatar)       ┘
```

---

## 5. Build & deploy workflow

- **Engine:** Unity **6000.6.0f1 (Unity 6.6)**. **Color space: Linear** (`ProjectSettings/ProjectSettings.asset`,
  `m_ActiveColorSpace: 1`) — imported textures must have correct sRGB flags or colors render wrong.
- **Render pipeline (PIMLR):** `Assets/Reversed Interactive/New Gen Urban/URPSettings/UniversalRP-HighQuality.asset`.
  Its `m_RendererType: 1` is the *RendererType enum* pointing at a renderer-data asset in `m_RendererDataList`; that asset
  is **`ForwardRenderer.asset`** with **`m_RenderingMode: 0` (Forward)**. So **PIMLR renders Forward**. MSAA 4x,
  HDR on, SRP Batcher **off** (`m_UseSRPBatcher: 0`), 2-cascade shadows @ 2048.
- **The deferred asset is the other game's:** `Assets/ARC/Assets/RP/URP/UniversalRenderPipelineAsset_High.asset` has
  `m_PrefilteringModeDeferredRendering: 2` (deferred) and requires depth/opaque textures. It belongs to Humanity Rocks.
  Both pipeline assets live in the same project; **quality levels** in `ProjectSettings/QualitySettings.asset` map levels
  to these assets. PIMLR's default quality must resolve to the **Reversed/Forward** asset. See §7 for the WebGL risk.
- **Shader variant stripping:** `Assets/UniversalRenderPipelineGlobalSettings.asset` has `m_StripDebugVariants: 1` and
  **`m_StripUnusedVariants: 1` (aggressive)** — keywords not detected at build time get stripped and cannot be summoned
  at runtime.
- **Build entry point:** `Assets/Editor/PimlrBuildScript.cs`:
  - `BuildWebGL()` — collects **enabled** scenes from `EditorBuildSettings.scenes`, errors if none, builds to
    `Build/WebGL` for `BuildTarget.WebGL` / `BuildTargetGroup.WebGL`.
  - `BuildWebGLBaked()` — variant that bakes lightmaps first (needs a GPU; omit `-nographics`).
  - Headless invocation:
    ```bash
    Unity -batchmode -nographics -projectPath <proj> -buildTarget WebGL \
      -executeMethod PimlrBuildScript.BuildWebGL -logFile <log>
    ```
- **WebGL templates:** `Assets/WebGLTemplates/Better2020` (splash video; expects `Stryker_14.mp4` under
  `TemplateData/`), plus `Responsive`.
- **StreamingAssets:** `Loadingvideo.mp4` (loading loop) and `PLMRVideo.mp4` (#9 inter-level FMV fallback).

**Pre-build checklist:** only PIMLR scenes enabled (0,2,3; 1 disabled); default WebGL quality → Forward/Reversed asset;
no PIMLR material references an ARC deferred shader; StreamingAssets videos present; `AuthManager.baseUrl` correct;
Flowise is optional legacy functionality and is not required for the baked Sirihanna gameplay path.

---

## 6. External dependencies (and their fragility)

| Dependency | Where | Used for | Fragility |
|---|---|---|---|
| **Login API** `https://www.idea-labs.xyz/api/` | `AuthManager.cs:10` | register/login/logout + `get-activity`/`save-activity` (coins, achievements) | Hard network dependency. Failures fall back to `DummyLogin()` (dev). Coin saves fail silently if down. |
| **Flowise LLM** `https://flowise-h06w.onrender.com/api/v1/prediction/<id>` | `FlowiseAPI.cs:13–14` (bearer token hardcoded line 14) | Sirihanna AI chat (legacy/optional) | **Render free tier sleeps** on idle → first request after sleep is slow/times out, and there is **no offline fallback** (chat UI opens but never responds). Earlier endpoints (e.g. `flowise.thecela.com`, and historically ngrok-style tunnels) are commented out — **ngrok/tunnel URLs are ephemeral and rotate**, so any hardcoded tunnel will die. The supported direction is to replace this with the **baked DialogueSystem** (§4.2). |
| **Local GLB avatars** | `LocalAvatarLoader.cs` | runtime avatar selection/instantiation | Requires editor-populated asset references; no network dependency. |
| **Inter-level FMV** | `SceneManagerScript` (#9) / `FMVSequencer.cs` | optional cutscene; `StreamingAssets/PLMRVideo.mp4` or remote URL | Off by default; 60s safety timeout (`fmvMaxWaitSeconds`) prevents a stuck video from hanging the load. A remote video URL (e.g. an `idea-nfts.com` land video referenced historically) is only as reliable as that host. |
| **WebGL JS interop** | `AiChatInteraction.OnClosePanel` → `Application.ExternalCall("stopSpeaking")` | stop TTS | Requires a `stopSpeaking()` JS function in the host page; missing = silent no-op. |

**Hardcoded secrets:** the Flowise bearer token is committed in `FlowiseAPI.cs`. Rotate/remove before any public build.

---

## 7. Known issues

### 7.1 WebGL "blown white / pink-rainbow" rendering (highest priority)

- **Symptom:** on WebGL, the scene renders blown-out white or a pink/magenta (missing-shader) rainbow.
- **Likely cause:** the project ships **two URP pipelines** — PIMLR's Forward (`Reversed .../ForwardRenderer.asset`,
  `m_RenderingMode: 0`) and ARC's **Deferred** (`m_PrefilteringModeDeferredRendering: 2`). WebGL (GLES) handles URP
  deferred poorly (limited MRT, expensive G-buffer). Combined with **aggressive variant stripping**
  (`m_StripUnusedVariants: 1`), any deferred-only or otherwise-undetected shader keyword can be stripped at build time;
  at runtime the shader falls back to a magenta error material → pink output. Blown-white is consistent with a
  Linear-color-space + tonemapping/exposure mismatch or HDR buffers misbehaving on the WebGL target.
- **Resolution direction (in order):**
  1. Confirm the WebGL default quality level maps to the **Reversed Forward** URP asset, and that **no other quality
     level reachable on WebGL** maps to the ARC deferred asset. Ideally delete/redirect the ARC quality level for the
     PIMLR build.
  2. Audit `YannicksWorld`/`SceneStaticEU` materials for any referencing ARC deferred shaders or
     `Assets/ARC/.../Materials/`.
  3. If artifacts persist, relax `m_StripUnusedVariants` (or add the needed keywords to a shader-variant allowlist) and
     rebuild to confirm a stripped-variant root cause.
  4. Verify Linear-space texture import flags and post-processing/tonemapping settings on WebGL.
  5. Long-term: **silo** the projects so PIMLR builds with a single Forward-only pipeline and the ARC assets aren't in
     the build graph at all.

### 7.2 Other notable issues

- **Out-of-scope coupling:** some Helix code sits inside `Assets/_Game/Scripts/` and `PlmrMainMenuPanel`/`UILodingScreen`
  branch on `GameMode.Helix`/`HumanityRocks`. Severable, but be careful — it's wired into the hub menu.
- **`UIManager.UIMenus[5]` hardcoded index** for the level-complete popup (used by `GameExecutionManager` and
  `DialogueAwakener`). Reordering the array breaks gameplay.
- **PlayerPrefs-as-save:** zone mode, coins (cache), purchase flags, customization, achievements are all PlayerPrefs.
  Stringified enum for `currentZoneMode` → renames break saves (silent fallback to `ChatWilly`).
- **`UIManager` is not a singleton** — duplicate instances could conflict.
- **Editor-only fixes** (cannot be done from code): truck parking placement, excess vehicle spawns, incomplete map
  boundary colliders, material assignment for the hologram shader and Sirihanna recolor.
- **No-arg `LoadScene()`** relies on `buildIndex+1`, which collides with the disabled `03_LevelSelection` — always load
  by name.

---

## 8. How to extend / maintain

- **Add a zone or change progression:** edit the `Zone` enum (in `UILevelCompletePopUp.cs`) **and** keep the
  `PlayerPrefs["currentZoneMode"]` strings, `GameExecutionManager.Start()` branch, `Update()` transitions, and the
  `GoalList`/`UI_GoalPanel` toggles in sync. Remember the resume logic in `DialogueAwakener.Start()`.
- **Tune combat/economy:** punch damage in `JUCharacterControllerCore` (#13); coin rewards in
  `GameExecutionManager.OnKillEnemy` and `InfiniteMode.CompleteWave`; dialogue entry cost in
  `DialogueAwakener.EndDialogBoxAndSpawnZonbies` (−40). Boost magnitudes in `AuthManager.PowerChange`/`ResetPowerups`.
- **Replace AI chat with baked dialogue (recommended):** stop activating `AiChatInteraction`'s `ai_BOT_Canvas`; route the
  Sirihanna NPC through `DialogueAwakener` + a `DialogueScriptable` tree whose final response fires the
  `EndDialogBoxAndSpawnZonbies` event. This removes the Flowise/Render dependency.
- **Wire the movement leash (#5):** add an empty GO with `BoundsRestriction` to `YannicksWorld`, size the gizmo box to the
  starting area, and ensure `UnlockVehicle()` is called when the vehicle becomes drivable.
- **Enable the inter-level FMV (#9):** on `SceneManagerScript`, check `playInterLevelFMV`, assign `fmvVideoPlayer`
  (+ optional `fmvScreenRoot`), and either set `fmvClip` or drop `PLMRVideo.mp4` in StreamingAssets.
- **New music track + boost:** add a `MusicData` entry on `SceneManagerScript.musicData[]` (clip, price, free flag,
  `boostname`/`boostInfo`, `achievementReward`); purchase state persists in PlayerPrefs keyed by `audioClip.name`.
- **Input controls:** for music, edit `MusicInputHandler` (it reads devices directly; do **not** expect the baked
  `JUTPSInputControlls.cs` wrapper to pick up new actions without regenerating it in the Editor).
- **Manager lifetime:** if a manager must survive scene loads, ensure its serialized `dontdestry` is set and that
  `LevelInit` re-instantiates it on scenes that need it. Watch the duplicate-destroy in `Singleton.Awake`.
- **Before any public build:** rotate/remove the hardcoded Flowise token; confirm `AuthManager.baseUrl`; verify the WebGL
  pipeline/quality mapping (§7.1).
- **Reference points:** start at `AuthManager.Start` → `SceneManagerScript.LoadScene` → `GameExecutionManager.Start`/
  `Update` → `DialogueAwakener.EndDialogBoxAndSpawnZonbies`. Those four cover boot, scene loading, the gameplay loop, and
  the progression unlock.
```
