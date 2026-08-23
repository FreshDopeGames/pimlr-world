# PIMLR World

Standalone **PIMLR** game, isolated from the original multi-game "YannickThurz" project.
**Unity 2022.3.12f1**, URP, WebGL target.

## What this is
PIMLR is a single-player third-person action/story game: a living-room hub that leads into
downtown gameplay (wash the "haters" with a water gun, music-driven attribute boosts, a
drivable vehicle, a police-car chase). It was unbundled from a project that contained three
games — PIMLR, Helix, and Humanity Rocks. **PIMLR is the only game in this repo;** ~11 GB of
unused / other-game content was stripped.

> A few shared asset packs remain because PIMLR references assets inside them — notably
> **Heavy Station Kit** (PIMLR's environment art) and a small number of shared assets in `ARC/`.
> PIMLR's vehicle uses **RealisticCarControllerV3** plus a car from **CarControllerwithShooting**
> (the shared "Helix car"). These are intentional dependencies, not leftover games.

## What was done (unbundling + QA fixes)
Isolated PIMLR into this repo and addressed the following from the two QA passes
(Polish Pass + Playtest 1):

**Fixed (code, compiles clean):**
- **Skip login → boot straight into the living room** — `AuthManager` (loads `SceneStaticEU` by name)
- **Sirihanna = baked scripted dialogue** that unlocks the water gun — `ChatbotUIController` (removes the live AI/Flowise dependency)
- **Attribute boosts clear on track skip / stop / pause** — `MusicSystem`
- **Water gun stays equipped when entering the vehicle** — `DriveVehicles`
- **Punch = 5 damage, player-only** — `JUCharacterControllerCore`
- **Gamepad music controls** (LB/RB = prev/next, Start = play/pause) — new `MusicInputHandler`
- **Sirihanna hologram material**
- **Fixed a pre-existing compile error** in `ARC/Assets/Scripts/TS/UI/QualityPage/QualityUIManager.cs`

**Not done — needs Editor/scene work:**
- #5 movement leash before vehicle unlock · #6 parallel-park the truck · #7 remove excess cars past block 1 ·
  #9 full-motion videos between levels (code present, off by default) · #10 map boundary · #11 stamina-bar UI ·
  #15 Sirihanna outfit recolor (texture work)

## ⚠️ Known issue — WebGL lighting / rendering
Headless/batchmode WebGL builds render incorrectly (blown-out white; pink/rainbow garbage on surfaces).
The production GUI-build pipeline renders correctly. To get a correct build:
1. **Build from the Unity Editor** (File → Build Settings → WebGL → Build) — **not** headless `-batchmode -nographics`.
2. Keep WebGL on the **forward** URP (quality level 3). The **deferred** URP (quality level 2) produces the
   pink/rainbow garbage on WebGL.
3. If surfaces still look wrong, set `Assets/UniversalRenderPipelineGlobalSettings → Strip Unused Variants = off`
   (variant stripping can drop shader variants the runtime needs), then rebuild.

## Build & run
- Unity **2022.3.12f1** (URP) with the **WebGL Build Support** module.
- Open the project; first import builds `Library/`.
- Build: **File → Build Settings → WebGL → Build**.
  (A headless helper exists at `Assets/Editor/PimlrBuildScript.cs` — `PimlrBuildScript.BuildWebGL` — but for
  correct rendering use the GUI build per the issue above.)
- Live build scenes: `Assets/_Game/Scene/Pimlr/00_MainMenu` → `SceneStaticEU` (living room) → `YannicksWorld` (downtown).

## External dependencies (verify before shipping)
- **Ending video** streams from `idea-nfts.com` — consider bundling locally for reliability.
- **Login API**: `idea-labs.xyz`.
- **Ready Player Me**: avatar loading over the network (CORS/connectivity sensitive).
- Legacy Sirihanna chat used Flowise / ngrok endpoints — **now bypassed** by the baked dialogue; the API
  tokens have been redacted from `FlowiseAPI.cs`. Rotate them if they were ever live.

## Architecture
See **`docs/ARCHITECTURE.md`** — deep technical reference (scenes, systems, data flow, build/deploy).

----

# PIMLR World — Feature Inventory & Migration Planning Doc (v2)

*Scoped per your correction: PIMLR World is a single, standalone game — not a hub linking out to Helix or HumanityRocks. Those are separate projects and are excluded below except where I need to confirm a file isn't secretly shared. YannicksWorld is the primary downtown cityscape where 3rd-person action-shooter combat happens; other scenes are character/level-select menus that transition the player between states *within* PIMLR World (not between separate games).*

---

## 1. Confirmed Scope

**In scope (PIMLR World):**
- Menu/flow scenes: `00_MainMenu`, character selection, level selection, `SceneStaticEU` (customization/avatar home base), `EnterUsername` variants, loading screens
- **YannicksWorld** — the open-world downtown combat space
- Character customization, Ready Player Me avatar creation
- NPC dialogue gate ("Sirihanna") → unlocks water gun → spawns combat wave
- Wave-based combat (`InfiniteMode`), zone progression (`Zone1`, `ZoneBoss1`, `Zone2`, `ZoneBoss2`, `ChatWilly`, `InfiniteMode`)
- Movement leash / area unlock tied to vehicle unlock
- Coin economy, music purchase shop, achievement-gated stat boosts
- Login/auth flow (currently bypassed by `DummyLogin`)

**Out of scope (separate projects, ignore unless shared):**
- Everything under `Helix_game/` (`HelixSocketManager`, `HelixPlayerHealth`, `HelixMiniMapCamera`, `HelixKillInfoObjectPool`, `HelixHealthBar`, `HelixMainMenu`, `HelixGameplayKillInfo/PlayerInfo`, `HelixAllPlayerInfo`, `HelixSocketStaticData`)
- HumanityRocks (`NeonCityScene`) — no dedicated scripts appeared in this drop, just referenced as a `GameMode` target
- `Constant.cs` entries for `Helixlink`, `Humanitylink`, `Helix_Scene_Name`, `Humanity_Scene_Name` — these become dead code once the hub-redirect concept is dropped

---

## 2. Feature Inventory (PIMLR World only)

### Scene / App Flow
- Singleton scene manager (`SceneManagerScript`) — async load, progress bar, post-load fade, optional inter-level FMV hook (off by default)
- `UIManager` — named-screen show/hide array, single shared fader
- `LevelInit` — bootstraps UI/Auth managers into a scene if missing
- Scene-transition triggers: `LoadScene.cs`, `LoadSceneOnClick.cs`, `ApproachTrigger` (proximity-based scene load), `SceneSwitcher` (debug hotkey)
- **Needs confirmation:** `LoadScene.cs`/`LoadSceneOnClick.cs` reference scene names like `"ThirdPerson Shooter Demo"`, `"Path Follow_Ash"`, `"Tuto_01b"`, `"IDEAMainLand"` — are these active PIMLR World scenes, template/demo leftovers from the JUTPS asset, or old test scenes safe to ignore?

### Menus (PMLR namespace)
- `PlmrMainMenuPanel` — main menu, level select (maps to `Zone` enum), avatar-create entry point, coin display, Tab-toggle debug menu that also freezes/unfreezes the character controller
- `PlmrAvatarCreatePanel` — coin display + hands off to `SceneStaticEU_Manager.Panel_Execution()`
- `SceneStaticEU_Manager` — customization scene controller: opens customization camera/canvas, coin display, scene transition back into gameplay
- `PlmrDiractionFollowCharater` — screen-space arrow pointing at nearest tracked objective, clamped to screen bounds
- `PlmrLookAtCamera` — billboard-style UI-in-world facing the camera (likely used for avatar name tags or customization preview)
- `UILevelCompletePopUp` — reads `GameExecutionManager.Instance.currentZoneMode` and swaps title/description per zone, including a dynamic "Wave N begins" string for infinite mode
- `UI_GoalPanel` — toggle-based goal checklist + kill-count readout + a "booster" popup animation when a power-up is granted

### Player Character & Movement (JUTPS-based)
- `JUCharacterController` (third-party JUTPS asset) drives movement, sprint stamina, item holding
- `UIStaminaBar` — custom addition on top of JUTPS's internal sprint-decay value
- `BoundsRestriction` — world-space AABB leash active until `GameExecutionManager.vehicleUnlocked` flips true, then self-disables permanently
- Throwables: `GrenadeThrower` (timed auto-throw toward the player, currently on an Invoke loop rather than player input — worth confirming if that's intentional AI/enemy behavior or debug scaffolding), `Grenade` (explosion force + radius, checks `GameExecutionManager.zone2Finish`), `EnemyWaterBallonController` (JUTPS `HoldableItem`-based throw)

### Vehicles
- **Needs confirmation:** `PoliceCarAI` / `PoliceCarChase` / `PlayerCarFollow` all use `JUTPS.VehicleSystem.CarController` and reference a `"PlayerCar"` tag — is a police-chase vehicle mechanic actually part of PIMLR World's downtown combat loop, or is this leftover from a shared vehicle-asset base also used in Helix?
- `UserCar.cs` sets Cinemachine camera priority based on a `PlayerPrefs["SelectedCar"]` key, with cameras commented as "Helix, Hero, Hype" — **this naming is likely just camera-angle names (like a rig with three named virtual cameras), not a reference to the Helix game.** Worth a one-line confirmation so I don't misfile it.
- `GetAddedAsChild` — auto-parents itself to an `"AdvancedCarSystem"` GameObject at runtime (likely a modular vehicle asset's setup convention)
- Legacy/prototype car scripts present but likely dead: `tryMove.cs`, `AICarController.cs`, `CarFollower.cs` (NavMesh-based) — recommend confirming which (if any) are still wired to active prefabs before I treat them as candidates for porting

### Customization
- `changeColor` (base class) → `Customization_Applier` (loads saved colors on scene start) / `Customization_Handler` (in-scene picker UI, resets to white preview on panel close)
- Persistence via `PlayerPrefs` keyed by renderer name — works today but won't scale cleanly if you add more customization categories
- Ready Player Me integration: `GameReadyPlayerManager` (avatar creation callback → loads avatar with `AvatarObjectLoader`), `UIReadyPlayerMeScrn` (WebGL vs. non-WebGL UI branch)

### NPC Dialogue → Combat Gate
- `AiChatInteraction` — proximity trigger opens chat UI, freezes player input/controller
- `ChatbotUIController` — **originally wired to a live LLM via Flowise** (`FlowiseAPI.cs`, hosted external endpoint + bearer token in source), **now replaced with baked/hardcoded dialogue lines** advanced by button click. Reaching the last line calls `EndDialogBoxAndSpawnZonbies()`, which sets `currentZoneMode = Zone1`, restores player control, and calls `GameExecutionManager.Instance.Zone1Start()` — i.e., **this is the actual entry point into combat**, not just flavor dialogue.
- Open thread from source comments: "OWNER: finalize this copy" — the baked lines are explicitly marked as not-yet-final

### Combat
- `InfiniteMode` (singleton) — alternates "normal enemy" waves and "boss" waves, coins awarded per wave (`50 * currentWave`), enemies spawned via `JUHealth` prefabs into a randomized box volume
- Kill tracking synced into `UI_GoalPanel.SetCurrentKillInfo`
- Generic interaction helpers used throughout the combat space: `ActivateOnApproach`, `ActivateOnCollision` (activates + auto-deactivates after 15s on BoxCollider hit), `DeactivateObjectOnClick`, `ToggleObjects`

### Economy
- `CoinManager` (singleton, referenced everywhere but not in this file export — I don't yet have its implementation)
- Music shop: `MusicPurchasePanel` + `MusicPurchaseEntry`, coin-gated track unlocks, `PlayerPrefs`-tracked ownership, DOTween "not enough coins" popup
- Achievement-gated stat boosts via `AuthManager`: `AchievementType` (EnemyWash, BossWash, PoliceCar — **note: "PoliceCar" achievement type exists even though the police-chase mechanic's relevance is unconfirmed above**), thresholds stored client-side in `PlayerPrefs`, `AchievementReward` enum (MovementSpeed, AmmoSize, VehicleSpeed, Defense, MaxHP, FreezeRay) applied as flat overrides via `AuthManager.PowerChange()`

### Auth
- `AuthManager` (singleton) — real REST login/register against `idea-labs.xyz/api/`, but `Start()` auto-bypasses to `DummyLogin()` (hardcoded `developer`/`developer`) whenever the active scene is `00_MainMenu`
- Plaintext username/password cached in `PlayerPrefs` when "remember me" is on

---

## 3. Technical Debt & Risks (re-scoped)

1. **`GameExecutionManager` is the true center of gravity** for zone state, vehicle-unlock flag, player handler, and car controller references — it's referenced by half the systems above (`BoundsRestriction`, `ChatbotUIController`, `UILevelCompletePopUp`, `Grenade`, `AuthManager.PowerChange`) but wasn't included in this file drop. **I need this file** to actually map zone/state flow accurately.
2. **Two input systems coexisting** — legacy `Input.GetAxis/GetKey` vs. the new Input System's `Mouse.current`/`Keyboard.current`/`Gamepad.current`. Confirm the project's Input Handling is set to "Both," or half these scripts silently no-op.
3. **Client-authoritative economy** — coins and achievement thresholds live in `PlayerPrefs` with no server validation. Fine for solo/offline scope; flag if that ever changes.
4. **Auth bypass + plaintext credential caching** — deliberate for now per your dialogue comment, but worth a conscious "yes, keep this until X" decision rather than leaving it as an accident waiting to ship.
5. **Hardcoded Flowise bearer token in source history** — even with the live AI chat disabled, that token is sitting in git history and should be rotated/scrubbed regardless of whether you revive the live-chat feature.
6. **Uncertain vehicle scope** — see the Police Car question above; this affects whether Chaos Vehicles + AI chase logic is even needed in the Unreal port, or if it's dead weight from a shared asset base.

---

## 4. Updated Questions

1. Can you share `GameExecutionManager.cs`? It's the actual state machine tying zones, vehicle-unlock, and combat spawning together — I can't map the flow accurately without it.
2. Is the **police-car chase mechanic** (`PoliceCarAI`/`PoliceCarChase`) part of PIMLR World's downtown gameplay, or is it a leftover from a shared vehicle asset that Helix also used?
3. What's the actual **name/count of scenes** in the menu flow? I see `00_MainMenu`, `01_Game`, `02_CharacterSelection`, `03_LevelSelection`, `04_Pimlrp`, `SceneStaticEU`, `YannicksWorld`, `DemoBuild` in the `.meta` files — can you confirm which of these are current/shipping vs. old builds I should ignore?
4. For the NPC dialogue gate: keep it as **finalized baked dialogue** (and if so, do you have final copy, or do you want help drafting it), or **restore live LLM chat**? This is blocking because the event that unlocks the water gun and starts combat is currently tied to "last baked line reached."
5. Confirm scope on `CoinManager` — can you share that file too? It's referenced everywhere but wasn't in this drop, and it's central to the whole economy layer.
6. Target Unreal version and Blueprint vs. C++ vs. hybrid preference, so I can start mapping JUTPS's character/item/vehicle systems to concrete Unreal equivalents.
7. For customization: how many categories/slots are planned long-term (shirt/hair/pants/shoes currently) — worth knowing before deciding whether to carry the `PlayerPrefs`-per-renderer approach forward or replace it with a proper save system during the port.

---

*Once `GameExecutionManager` and `CoinManager` are in hand, plus answers on scope items 2–4 above, I can build the actual state-flow diagram (menu → customization → zone → combat → wave progression) and start the Unreal architecture mapping.*

