# PIMLR World

Standalone **PIMLR** game, isolated from the original multi-game "YannickThurz" project.
**Unity 6000.6.0f1 (Unity 6.6)**, URP, WebGL target.

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
- Unity **6000.6.0f1 (Unity 6.6)** (URP) with the **WebGL Build Support** module.
- Open the project; first import builds `Library/`.
- Build: **File → Build Settings → WebGL → Build**.
  (A headless helper exists at `Assets/Editor/PimlrBuildScript.cs` — `PimlrBuildScript.BuildWebGL` — but for
  correct rendering use the GUI build per the issue above.)
- Live build scenes: `Assets/_Game/Scene/Pimlr/00_MainMenu` → `SceneStaticEU` (living room) → `YannicksWorld` (downtown).

## External dependencies (verify before shipping)
- **Ending video** streams from `idea-nfts.com` — consider bundling locally for reliability.
- **Login API**: `idea-labs.xyz`.
- **Local avatars**: GLB avatar files are stored under `Assets/Avatars` and loaded without an online avatar service.
- Legacy Sirihanna chat used Flowise / ngrok endpoints — **now bypassed** by the baked dialogue; the API
  tokens have been redacted from `FlowiseAPI.cs`. Rotate them if they were ever live.

## Architecture
See **`docs/ARCHITECTURE.md`** — deep technical reference (scenes, systems, data flow, build/deploy).

----

# PIMLR World — Feature Inventory & Migration Planning Doc (v6)

*Corrected pass sequence per your note: Playtest 1 is the ongoing, cumulative QA list (spans builds dated 8/28 through 12/4). Polish Pass is a deliberately narrow, curated slice of it — 16 priority items pulled out for a fast sprint ahead of the next monthly demo, not a comprehensive follow-up. This changes how I should read overlap between the two.*

---

## 0. Corrected Pass Relationship

- **Playtest 1** = the living/cumulative bug tracker. Most of its 46 items are already marked `Done`, reflecting fixes applied across several builds over roughly four months.
- **Polish Pass** = a curated 16-item subset pulled from that larger list specifically to ship before the next demo, deliberately *not* attempting everything Playtest 1 had flagged. The `PIMLR #N` code comments map to this curated subset, not to the full Playtest 1 list.
- The README's "Fixed" / "Not done" split reflects the **outcome of the Polish Pass sprint** specifically — which is why it lines up so cleanly with Polish Pass's 16 items and doesn't mention most of Playtest 1's other 30.

**What this means practically:** the correct way to read "is X actually fixed" is: **README > Polish Pass > Playtest 1**, in that order of recency — not Playtest-1-then-Polish-Pass as I'd assumed. Re-reading with that order:

| Item | Playtest 1 (cumulative) | Polish Pass (curated sprint) | README (most current) | Current read |
|---|---|---|---|---|
| Water gun unequips in vehicle | QA Review (#18) | Open (#12) | **Fixed** (`DriveVehicles`) | Treat as resolved — README postdates both |
| Punch damage to enemies | QA Review (#19) | Open (#13, spec: 5 dmg) | **Fixed** (`JUCharacterControllerCore`) | Treat as resolved — README postdates both |
| Attribute boosts don't deactivate on pause | QA Review (#44) | **Open, Critical** (#4) | *Not mentioned* | **Still genuinely open** — never appears as fixed anywhere. Design the Unreal boost system with explicit pause/stop/skip teardown from the start. |
| Sirihanna outfit recolor (blue/pink) | *(not a Playtest 1 item)* | Done (#15) | Listed as "not done — texture work" | **Confirmed fixed by you directly** — current build has the blue/pink holographic mesh filter live in YannicksWorld. README's "not done" note is simply stale. |

## 1. Three Playtest-1 items that never made it into the Polish Pass sprint

Since Polish Pass was a deliberate subset, these three genuinely-unresolved Playtest 1 items (`Cannot Reproduce` status — meaning QA couldn't consistently trigger them, not that they were fixed) weren't part of that curated cleanup and may still be lurking:

- **#2** — Truck can get stuck on pole/streetlight/sign collision near intersections
- **#11** — Police car sometimes reroutes away from the player and doesn't find its way back after being alerted
- **#12** — Character model missing in the customization screen; camera can get stuck locked in that position afterward

**Resolved per your confirmation — treat as closed.** Not expecting equivalents in Unreal (different physics/nav stack entirely), but flagging here as a known pattern to watch for if anything similar crops up during the vehicle-AI or customization-camera work.

---

## 2. Everything Else From v5 Still Holds

- Full 3-level story arc (Thug → Shooter → Bomber) → police chase → Infinite Mode unlock, with confirmed final copy
- Attribute boost system ("Boom Bye Bye," "Cold Shoulders," Freeze Ray as an active vehicle-chase weapon mode) tied to specific music tracks
- PIMLR Coin economy with per-track pricing
- Full controller input scheme (Polish Pass #8) — only the music-control subset is actually implemented today
- Map boundary — three proposed approaches (background plane / volumetric fog / extended 3D geometry), still an open design decision
- **Leaderboard system (Polish Pass #16)** — fully spec'd, zero implementation footprint anywhere reviewed so far. Still my recommendation: build this once, directly in Unreal, rather than in Unity first.

---

## 3. Updated Questions

1. Leaderboard system — build fresh in Unreal, or worth prototyping in Unity first? (Still leaning toward building it once, directly in Unreal, absent a reason to validate the design earlier.)
2. Still outstanding: `docs/ARCHITECTURE.md`, `GameExecutionManager.cs`, `CoinManager.cs`, `DriveVehicles.cs`, `JUCharacterControllerCore.cs`, the "PIMLR World Achievement Menu" doc, and any Pause Menu/Music Overlay UI/UX design doc.
3. Still outstanding: target platform (web vs. PC/console/mobile) and Unreal version / Blueprint vs. C++ preference.

---

*Content and design scope is now solid. The remaining blockers are the handful of missing implementation files (mainly `GameExecutionManager` and `CoinManager`, since they're the connective tissue for zone state and economy) plus a few product-level decisions (platform, leaderboard build order, engine language).*
*Once `GameExecutionManager` and `CoinManager` are in hand, plus answers on scope items 2–4 above, I can build the actual state-flow diagram (menu → customization → zone → combat → wave progression) and start the Unreal architecture mapping.*
