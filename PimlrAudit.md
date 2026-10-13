# PIMLR Project Audit
Unity 2022.3.62f3 | 2026-10-13 00:14

> Two kinds of weight: EDITOR weight (import time, Library size, compile time) and BUILD weight.
> Unity only ships assets reachable from build scenes, Resources, StreamingAssets and project settings.
> Unreferenced assets mostly slow the editor; they do not usually bloat the build.

## 1. Folder sizes (source files on disk, excluding .meta)

### Top level
| Folder | MB | Files |
|---|---:|---:|
| Assets/ARC | 2271.0 | 2166 |
| Assets/Reversed Interactive | 1786.6 | 668 |
| Assets/_Game | 1585.9 | 1020 |
| Assets/JaneArtWork | 1019.1 | 429 |
| Assets/AddedImplements | 993.3 | 483 |
| Assets/CarControllerwithShooting | 754.9 | 487 |
| Assets/FORGE3D | 490.4 | 785 |
| Assets/RealisticCarControllerV3 | 400.8 | 1082 |
| Assets/GLB | 296.4 | 6 |
| Assets/Epic Toon FX | 278.1 | 1568 |
| Assets/Textures | 233.4 | 44 |
| Assets/Plugins | 231.8 | 722 |
| Assets/Julhiecio TPS Controller | 228.1 | 766 |
| Assets/Models | 145.0 | 91 |
| Assets | 138.4 | 55 |
| Assets/Simple Music Player | 126.2 | 22 |
| Assets/NamuFX | 123.0 | 57 |
| Assets/AssetsByME | 81.9 | 188 |
| Assets/Videos | 67.8 | 2 |
| Assets/IDEA | 56.8 | 28 |
| Assets/Ready Player Me | 53.9 | 42 |
| Assets/StreamingAssets | 53.2 | 2 |
| Assets/Ash Assets | 46.9 | 217 |
| Assets/Player | 35.2 | 19 |
| Assets/Aquaset | 24.4 | 32 |

### Second level
| Folder | MB | Files |
|---|---:|---:|
| Assets/ARC/Assets | 2220.0 | 2158 |
| Assets/Reversed Interactive/New Gen Urban | 1786.6 | 668 |
| Assets/JaneArtWork/Living Room | 1019.1 | 429 |
| Assets/AddedImplements/Characters | 778.9 | 367 |
| Assets/CarControllerwithShooting/Models | 680.8 | 214 |
| Assets/_Game/Audio | 623.1 | 63 |
| Assets/FORGE3D/Sci-Fi Effects | 490.3 | 773 |
| Assets/_Game/Textures | 368.8 | 554 |
| Assets/_Game/ItemsModel | 315.5 | 24 |
| Assets/RealisticCarControllerV3/Models | 252.8 | 591 |
| Assets/Epic Toon FX/Prefabs | 238.7 | 897 |
| Assets/Plugins/CW | 231.8 | 721 |
| Assets | 138.4 | 55 |
| Assets/AddedImplements/Materials | 132.5 | 13 |
| Assets/Simple Music Player/Audio | 124.5 | 3 |
| Assets/_Game/Fonts | 118.8 | 173 |
| Assets/NamuFX/StylizedWaterEffects | 115.8 | 39 |
| Assets/Textures | 110.3 | 14 |
| Assets/Julhiecio TPS Controller/Models | 81.3 | 145 |
| Assets/Julhiecio TPS Controller/Animations | 74.4 | 120 |
| Assets/_Game/PlayerModel | 71.5 | 8 |
| Assets/AssetsByME/Standard Assets | 70.6 | 129 |
| Assets/Videos | 67.8 | 2 |
| Assets/AddedImplements/Sprites | 65.6 | 63 |
| Assets/RealisticCarControllerV3/Textures | 62.7 | 76 |

## 2. Largest 40 files
| MB | Path |
|---:|---|
| 97.5 | Assets/CarControllerwithShooting/Models/Gun/Weapon_56/Chainsaw_Man_Truck_Body.glb |
| 89.1 | Assets/JaneArtWork/Living Room/Textures/Wicker001_4K_NormalGL.png |
| 88.4 | Assets/AddedImplements/Characters/Christine-20220107T051216Z-001/Christine/CurlyHair01.obj |
| 87.9 | Assets/JaneArtWork/Living Room/Textures/Carpet014_4K_NormalGL.png |
| 87.0 | Assets/JaneArtWork/Living Room/Textures/Leather035D_4K_NormalGL.png |
| 80.9 | Assets/ARC/Assets/Scenes/Tracks/Demo/Track_05/OcclusionCullingData.asset |
| 80.8 | Assets/ARC/Assets/Scenes/Tracks/Demo/Track_04/OcclusionCullingData.asset |
| 80.8 | Assets/ARC/Assets/Scenes/Tracks/Demo/Track_02/OcclusionCullingData.asset |
| 80.7 | Assets/ARC/Assets/Scenes/Tracks/Demo/Track_03/OcclusionCullingData.asset |
| 80.7 | Assets/JaneArtWork/Living Room/Textures/Bricks054_4K_NormalGL.png |
| 80.6 | Assets/ARC/Assets/Scenes/Tracks/Demo/Track_01/OcclusionCullingData.asset |
| 80.6 | Assets/ARC/Assets/Scenes/Tracks/Demo/Track_00/OcclusionCullingData.asset |
| 80.0 | Assets/JaneArtWork/Living Room/Textures/Wood056_4K_NormalGL.png |
| 79.9 | Assets/AddedImplements/Characters/Stryker_files/Stryker_Final.ma |
| 79.0 | Assets/ARC/Assets/Textures/UI/YannickThurz_Blue.psd |
| 77.9 | Assets/Reversed Interactive/New Gen Urban/Textures/NY Path2_Albedo.png |
| 77.4 | Assets/AddedImplements/Characters/Papi-20220107T051220Z-001/Papi/Papi_31.blend |
| 74.7 | Assets/JaneArtWork/Living Room/Textures/Wood067_4K_NormalGL.png |
| 70.3 | Assets/AddedImplements/Characters/Christine-20220107T051216Z-001/Christine/Chris_37.blend |
| 69.8 | Assets/_Game/PlayerModel/Hero_Transformer.glb |
| 68.8 | Assets/ARC/Assets/Textures/UI/YannickThurz_Yellow_01.psd |
| 67.8 | Assets/Videos/Stryker_14.mp4 |
| 65.7 | Assets/AddedImplements/Materials/Space/nebula n0.hdr |
| 64.0 | Assets/NamuFX/StylizedWaterEffects/Textures/Tex_Particles_01.tga |
| 64.0 | Assets/FORGE3D/Sci-Fi Effects/Effects/Explosions/Textures/Explosion_001.tga |
| 64.0 | Assets/Reversed Interactive/New Gen Urban/Textures/PlantBeds_Albedo.tga |
| 64.0 | Assets/Reversed Interactive/New Gen Urban/Textures/Benches_Albedo.tga |
| 64.0 | Assets/Reversed Interactive/New Gen Urban/Textures/StreetProps1_Albedo.tga |
| 64.0 | Assets/Reversed Interactive/New Gen Urban/Textures/TrafficLights2_Albedo.tga |
| 64.0 | Assets/FORGE3D/Sci-Fi Effects/Effects/Explosions/Textures/Explosion_002.tga |
| 62.5 | Assets/JaneArtWork/Living Room/Textures/WoodFloor053_4K_NormalGL.png |
| 56.7 | Assets/_Game/ItemsModel/PIMLR World Clothes/uploads_files_4737628_Textures.7z |
| 55.8 | Assets/Textures/Snow_basecolor.png |
| 54.7 | Assets/AddedImplements/Characters/Yogi-20220107T051226Z-001/Yogi/YogiRef_92.blend |
| 54.4 | Assets/TerrainData_315b7efe-e5aa-415a-b6c6-fbda77154f22.asset |
| 54.4 | Assets/TerrainData_b28250e1-616e-458a-a5b6-941430c494d2.asset |
| 53.3 | Assets/GLB/white/white_shuttle.glb |
| 53.3 | Assets/ARC/Assets/Models/SpaceShip/white/white_shuttle.glb |
| 52.1 | Assets/Reversed Interactive/New Gen Urban/Textures/StreetProps1_Occlusion.tga |
| 52.1 | Assets/GLB/black/black_shuttle.glb |

## 3. Scenes
Enabled in build: Assets/_Game/Scene/PIMLR_World/00_MainMenu.unity, Assets/_Game/Scene/PIMLR_World/SceneStaticEU.unity, Assets/_Game/Scene/PIMLR_World/YannicksWorld.unity

Scenes NOT in the build (archive candidates):
- Assets/_Game/CC-CharaterCustomization/Scenes/CharacterCreatorScene_Empty.unity (2.1 MB)
- Assets/_Game/Scene/PIMLR_World/DoNotUse/01_Game 1.unity (0.4 MB)
- Assets/_Game/Scene/PIMLR_World/DoNotUse/01_Game.unity (0.4 MB)
- Assets/_Game/Scene/PIMLR_World/DoNotUse/02_CharacterSelection 1.unity (1.1 MB)
- Assets/_Game/Scene/PIMLR_World/DoNotUse/02_CharacterSelection.unity (1.1 MB)
- Assets/_Game/Scene/PIMLR_World/DoNotUse/03_LevelSelection.unity (7.4 MB)
- Assets/_Game/Scene/PIMLR_World/DoNotUse/03_LevelSelection_Old.unity (6.4 MB)
- Assets/_Game/Scene/PIMLR_World/DoNotUse/04_Pimlrp.unity (11.3 MB)
- Assets/_Game/Scene/PIMLR_World/DoNotUse/DemoBuild.unity (0.0 MB)
- Assets/_Game/Scene/PIMLR_World/DoNotUse/YannicksWorld backup 15_08.unity (12.1 MB)
- Assets/AddedImplements/coinmanager/CoinManager.unity (0.0 MB)
- Assets/AddedImplements/Prefabs/Common/New Scene.unity (0.0 MB)
- Assets/AddedImplements/Scenes/IframeLoader.unity (0.0 MB)
- Assets/AddedImplements/Scenes/ObjectSpawn.unity (1.2 MB)
- Assets/AddedImplements/Scenes/Path Follow_Ash_Multiplayer_NewTrack.unity (9.3 MB)
- Assets/AddedImplements/Scenes/Path Follow_Ash_Multiplayer_RCC.unity (2.5 MB)
- Assets/Aquaset/Scenes/DemoScene.unity (0.1 MB)
- Assets/ARC/Assets/Scenes/MainMenu/01_MM.unity (1.8 MB)
- Assets/ARC/Assets/Scenes/MainMenu/CutScene.unity (0.0 MB)
- Assets/ARC/Assets/Scenes/MainMenu/Demo.unity (1.4 MB)
- Assets/ARC/Assets/Scenes/MainMenu/HumanityRocks_Login.unity (0.0 MB)
- Assets/ARC/Assets/Scenes/MainMenu/NeonCityScene - Env.unity (15.2 MB)
- Assets/ARC/Assets/Scenes/MainMenu/NeonCityScene - new Env - Copy.unity (7.5 MB)
- Assets/ARC/Assets/Scenes/MainMenu/NeonCityScene - new Env.unity (17.6 MB)
- Assets/ARC/Assets/Scenes/MainMenu/NeonCityScene.unity (6.7 MB)
- Assets/ARC/Assets/Scenes/MainMenu/NeonScene01Old.unity (9.5 MB)
- Assets/ARC/Assets/Scenes/MainMenu/SpaceshipMenuScreen.unity (3.3 MB)
- Assets/ARC/Assets/Scenes/Showroom/Showroom.unity (0.6 MB)
- Assets/ARC/Assets/Scenes/Tracks/Default/StarterKit.unity (0.8 MB)
- Assets/ARC/Assets/Scenes/Tracks/Demo/Sci-Fi_Track_06.unity (15.5 MB)
- Assets/ARC/Assets/Scenes/Tracks/Demo/Track_00.unity (9.1 MB)
- Assets/ARC/Assets/Scenes/Tracks/Demo/Track_01.unity (9.0 MB)
- Assets/ARC/Assets/Scenes/Tracks/Demo/Track_02.unity (9.1 MB)
- Assets/ARC/Assets/Scenes/Tracks/Demo/Track_03.unity (8.7 MB)
- Assets/ARC/Assets/Scenes/Tracks/Demo/Track_04.unity (8.9 MB)
- Assets/ARC/Assets/Scenes/Tracks/Demo/Track_05.unity (9.3 MB)
- Assets/ARC/Assets/Scenes/Tutorials/Tuto_01/Tuto_01.unity (1.0 MB)
- Assets/ARC/Assets/Scenes/Tutorials/Tuto_01/Tuto_01b.unity (1.0 MB)
- Assets/ARC/Demo/SpaceshipMainLoader.unity (1.4 MB)
- Assets/ARC/Demo/Track_00_demo.unity (9.3 MB)
- Assets/Ash Assets/Ash Vehicle AI/Demo scene/ConfiguratorGenerationScene.unity (1.2 MB)
- Assets/Ash Assets/Ash Vehicle AI/Demo scene/Path Follow_Ash Helix.unity (1.2 MB)
- Assets/Ash Assets/Ash Vehicle AI/Demo scene/Path Follow_Ash.unity (1.3 MB)
- Assets/Ash Assets/Ash Vehicle AI/Demo scene/Path Follow_Ash_LoadedVehicle.unity (2.3 MB)
- Assets/Ash Assets/Ash Vehicle AI/Demo scene/Path Follow_Ash_Multiplayer.unity (1.3 MB)
- Assets/Ash Assets/Ash Vehicle AI/Demo scene/Path Follow_Ash_Multiplayer_NewTrack2.unity (0.0 MB)
- Assets/Ash Assets/Ash Vehicle AI/Demo scene/Path Follow_Ash_Multiplayer_NewTrack3Colosseum.unity (0.2 MB)
- Assets/Ash Assets/Ash Vehicle AI/Demo scene/RussiaBraviaScene.unity (6.2 MB)
- Assets/Ash Assets/Ash Vehicle AI/Demo scene/Target Follow_Ash.unity (0.8 MB)
- Assets/Ash Assets/New Folder/New Scene.unity (0.0 MB)
- Assets/Ball Pack/Showcase.unity (0.1 MB)
- Assets/Car_Vs_Ai/Scene/Player_VS_Ai.unity (0.4 MB)
- Assets/CarControllerwithShooting/Scenes/00_MainMenu - Helix.unity (0.0 MB)
- Assets/CarControllerwithShooting/Scenes/AIBot.unity (5.5 MB)
- Assets/CarControllerwithShooting/Scenes/Car vs AI/Player_VS_Ai(Map2).unity (1.7 MB)
- Assets/CarControllerwithShooting/Scenes/Colloseum.unity (0.1 MB)
- Assets/CarControllerwithShooting/Scenes/FirstScene.unity (0.9 MB)
- Assets/CarControllerwithShooting/Scenes/Gameplay(colloseum) - Copy.unity (0.1 MB)
- Assets/CarControllerwithShooting/Scenes/Gameplay(colloseum) - optimization before.unity (0.3 MB)
- Assets/CarControllerwithShooting/Scenes/Gameplay(colloseum).unity (0.4 MB)
- Assets/CarControllerwithShooting/Scenes/Gameplay(terrain) - Copy.unity (2.7 MB)
- Assets/CarControllerwithShooting/Scenes/Gameplay(terrain).unity (0.6 MB)
- Assets/CarControllerwithShooting/Scenes/Gameplay.unity (0.6 MB)
- Assets/CarControllerwithShooting/Scenes/New Scene.unity (0.1 MB)
- Assets/CarControllerwithShooting/Scenes/Path Follow_Ash_Multiplayer_NewTrack/Path Follow_Ash_Multiplayer_NewTrack.unity (9.3 MB)
- Assets/CarControllerwithShooting/Scenes/SampleScene(Main).unity (1.7 MB)
- Assets/CarControllerwithShooting/Scenes/SampleScene.unity (0.1 MB)
- Assets/CarControllerwithShooting/Scenes/SocketScene/FirstSceneBackUp.unity (0.4 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_2ddemo.unity (0.1 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_cards.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_combat.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_decals.unity (1.8 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_decals2.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_emojis.unity (0.1 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_emojis2.unity (0.1 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_explosions.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_explosions2.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_fire.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_fire2.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_fire3.unity (0.1 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_fireworks.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_flares.unity (0.1 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_healing.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_magic.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_magic2.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_magic3.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_maindemo.unity (0.1 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_missiles.unity (0.3 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_money.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_portals.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_portals2.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_powerups.unity (0.1 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_powerups2.unity (0.1 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_sparkles.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_swordcombat.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_swordcombat2.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/etfx_wind.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/Extras/etfx_decal_test.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/Extras/etfx_flatground.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/Extras/etfx_library.unity (0.2 MB)
- Assets/Epic Toon FX/Demo/Scenes/Extras/etfx_singleeffect.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/Extras/etfx_singleeffect_render.unity (0.0 MB)
- Assets/Epic Toon FX/Demo/Scenes/Extras/etfx_videopreview.unity (0.3 MB)
- Assets/Extensions/Best HTTP/Examples/SampleSelector.unity (0.2 MB)
- Assets/FORGE3D/Sci-Fi Effects/Burnout.unity (1.1 MB)
- Assets/FORGE3D/Sci-Fi Effects/Debris Field.unity (0.2 MB)
- Assets/FORGE3D/Sci-Fi Effects/Effects.unity (0.5 MB)
- Assets/FORGE3D/Sci-Fi Effects/Holographic.unity (0.2 MB)
- Assets/FORGE3D/Sci-Fi Effects/Misc/Nebula.unity (0.0 MB)
- Assets/FORGE3D/Sci-Fi Effects/Missiles.unity (0.1 MB)
- Assets/FORGE3D/Sci-Fi Effects/Turrets.unity (0.2 MB)
- Assets/FORGE3D/Sci-Fi Effects/Warp Jump.unity (0.0 MB)
- Assets/FORGE3D/Sci-Fi Effects/Warp Tunnel.unity (0.7 MB)
- Assets/FrostweepGames/MicrophonePro/Examples/Basic_Example/Example.unity (0.1 MB)
- Assets/FrostweepGames/MicrophonePro/Examples/LeapSync_Example/LeapSync_Example.unity (0.0 MB)
- Assets/IDEA/Helix/EnterUsername Helix.unity (0.2 MB)
- Assets/IDEA/Helix/MainMenu.unity (0.0 MB)
- Assets/IDEA/Helix/Menu 3D Helix.unity (0.2 MB)
- Assets/IDEA/Helix/RaceArea01.unity (1.8 MB)
- Assets/IDEA/Helix/RaceArea02.unity (1.4 MB)
- Assets/IDEA/Helix/RaceArea03.unity (1.0 MB)
- Assets/IDEA/Helix/RaceArea04.unity (1.2 MB)
- Assets/IDEA/Helix/SceneStaticEU Helix.unity (0.6 MB)
- Assets/IDEA/Helix/TrackSelection.unity (0.0 MB)
- Assets/IDEA/IDEAMainLand.unity (6.2 MB)
- Assets/IDEA/PIMLR/Ending Video.unity (0.0 MB)
- Assets/IDEA/PIMLR/EnterUsername.unity (0.2 MB)
- Assets/IDEA/PIMLR/JUTPS SetUp.unity (1.3 MB)
- Assets/IDEA/PIMLR/Lobby 3D.unity (0.2 MB)
- Assets/IDEA/PIMLR/Menu 3D.unity (0.2 MB)
- Assets/IDEA/PIMLR/Room 3D.unity (0.0 MB)
- Assets/IDEA/PIMLR/SceneStaticEU 1.unity (2.8 MB)
- Assets/IDEA/PIMLR/YannicksWorld - Copy.unity (10.1 MB)
- Assets/IDEA/PIMLR/YannicksWorldNew.unity (11.1 MB)
- Assets/JaneArtWork/Living Room/Scenes/Demo.unity (0.7 MB)
- Assets/Julhiecio TPS Controller/Demo Scenes/FPS Sample.unity (0.4 MB)
- Assets/Julhiecio TPS Controller/Demo Scenes/Gravity Switch Demo.unity (0.1 MB)
- Assets/Julhiecio TPS Controller/Demo Scenes/Sidescroller Demo.unity (0.1 MB)
- Assets/Julhiecio TPS Controller/Demo Scenes/Sidescroller Mobile Demo.unity (0.1 MB)
- Assets/Julhiecio TPS Controller/Demo Scenes/ThirdPerson Shooter Demo.unity (10.2 MB)
- Assets/Julhiecio TPS Controller/Demo Scenes/Top Down Demo.unity (0.5 MB)
- Assets/Julhiecio TPS Controller/Demo Scenes/Vehicle AI Demo.unity (0.4 MB)
- Assets/NamuFX/StylizedWaterEffects/Scene/DemoScene.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Atmosphere/Examples/01 Corona.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Atmosphere/Examples/02 Atmosphere.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Atmosphere/Examples/03    Sunset.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Atmosphere/Examples/04    Scattering.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Atmosphere/Examples/05    Customize.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Aurora/Examples/01 Aurora.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Backdrop/Examples/01 Backdrop.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Backdrop/Examples/02    PowerRGB.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Backdrop/Examples/03    ClampSizeMin.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Backdrop/Examples/04    Customize.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Backdrop/Examples/05    Pulse.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Belt/Examples/01 Belt.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Belt/Examples/02    Lighting.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Belt/Examples/03    Colors.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Belt/Examples/04 Additive.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Belt/Examples/05    PowerRGB.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Belt/Examples/06 Custom.unity (0.3 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Billboard/Examples/01 Sprite Billboard Flare.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Billboard/Examples/02    Overlay.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Billboard/Examples/03    Depth.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Billboard/Examples/04    Pointer.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Cloudsphere/Examples/01 Cloudsphere.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Cloudsphere/Examples/02    Lighting.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Cloudsphere/Examples/03    Sunset.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Cloudsphere/Examples/04    Detail.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Cloudsphere/Examples/05    Rim.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Debris/Examples/01 Debris Spawner.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Debris/Examples/02    Shape.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Debris/Examples/03 Debris Grid.unity (1.9 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Debris/Examples/04    Shape.unity (0.2 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Flare/Examples/01 Flares.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Jovian/Examples/01 Jovian.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Jovian/Examples/02    Rim.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Jovian/Examples/03    Lighting.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Jovian/Examples/04    Sunset.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Jovian/Examples/05    Scattering.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Jovian/Examples/06    Flow.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Lightning/Examples/01 Lightning Spawner.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Lightning/Examples/02    Order.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Lightning/Examples/03    Sequence.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Lightning/Examples/Lightning.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Examples/01 Planet.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Examples/02    Displace.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Examples/03    Detail.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Examples/04       Secondary.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Examples/05    Water.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Examples/06       Clamp.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Examples/07       Gradient.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Examples/08       Texture.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Examples/09       Coast.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Examples/10       Emission.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Examples/11    Atmosphere.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Examples/12    Collider.unity (3.3 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Prominence/Examples/01 Prominence.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Prominence/Examples/02    Fade Edge.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Prominence/Examples/03    Clip Near.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Prominence/Examples/04    Distortion.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Prominence/Examples/05    Detail.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Ring/Examples/01 Accretion Disk.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Ring/Examples/02    Animated Noise.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Ring/Examples/03 Planetary Ring.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Ring/Examples/04    Lighting.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Ring/Examples/05    Scattering.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Ring/Examples/06    Planet Shadow.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Ring/Examples/07    Ring Shadow.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Ring/Examples/08    Detail.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Ring/Examples/09    Near Fade.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Ring/Examples/10    Customize.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Shared/Examples/01 Stars.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Shared/Examples/02 Clouds.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Shared/Examples/03 Camera Controls.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Shared/Examples/04 Rigidbody Controls.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Shared/Examples/05 Procedural Spin + Scale.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Shared/Examples/06 Newtonian Gravity.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Shared/Examples/07 Planet Gravity.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Shared/Examples/08 Simple Orbit.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Shared/Examples/09 Shadow Layer.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Singularity/Examples/01 Lens.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Singularity/Examples/02    Warp Outer.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Singularity/Examples/03    Warp Strength.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Singularity/Examples/04    Hole Size.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Singularity/Examples/05    Hole Edge.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Singularity/Examples/06 Black Hole.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Spacetime/Examples/01 Spacetime.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Spacetime/Examples/02 Gaussian Well.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Spacetime/Examples/03    Orbit.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Spacetime/Examples/04 Ripple Well.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Spacetime/Examples/05    Animation.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Spacetime/Examples/06 Twist Well.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Spacetime/Examples/07    Animation.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Spacetime/Examples/08 Pinch Well.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Spacetime/Examples/Styles.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Star/Examples/01 Star Shader.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Star/Examples/02    Flow.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Star/Examples/03    Sunspots.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Star/Examples/04    Rim.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/01 Box.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/02    Offset.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/03    Colors.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/04    Clouds.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/05    Soft.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/06    PowerRGB.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/07    ClampSizeMin.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/08    Layering.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/09 Elliptical.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/10    Symmetry.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/11    Offset.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/12    Bias.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/13    Layering.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/14 Spiral.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/15    Rotation.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/16    ArmCount.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/17    Twist.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/18    ThicknessInner.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/19    ThicknessOuter.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/20    Layering.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/21 Nebula.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/22    HeightSource.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/23    ScaleSource.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/24    Jitter.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/25    HorizontalBrightness.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/26    Layering.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/27 Infinite.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/28    Far.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/29    Warp.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/30    Dust.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/31    Dust Near.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/32    Floating Camera.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/33 Dome.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/34    Bias.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/35 Custom.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Starfield/Examples/36 Pulse.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/00 Burst.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/01 Terrain Planet.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/02    Atmosphere.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/03    Heightmap.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/04    Simplex.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/05    Collider.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/06    Spawner.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/07    Areas.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/08    Feature.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/09 Terrain Ocean.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/10    Atmosphere.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/11    Depth Color.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/12    Detail.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/13    Animation.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/14    Waves.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Terrain/Examples/15    Shore.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Thruster/Examples/00 Basic Thruster.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Thruster/Examples/01 Controls.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/00 Camera.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/01 Floating Camera.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/02 Floating Object.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/03 Floating Speedometer.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/04 Floating Orbit.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/05 Floating Orbit Visual.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/06 Floating Spawner (Sphere).unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/07 Floating Spawner (Ring).unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/08 Floating Spawner (Orbit).unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/09 Floating Root.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/10 Floating Warp.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/11 Floating Scaler.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/12 Star Cluster.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/13 Nested Spawner.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/14 Nested Orbits.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/15 Third Person.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Universe/Examples/16 Floating Massive.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Accretion Disk.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Asteroid Belt.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Astrophysical Jet.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Aurora.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Continuum.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Dark Star.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Earth Sized Planet.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Elliptical Galaxy.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Gravitational Waves.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Jovian.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Nebula.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Obsidian Planet.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Planet Surfing.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Space Probe.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Spiral Galaxy.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Star.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Storm Planet.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Terrestrial Planet + Ring.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Terrestrial Planet.unity (0.1 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Vortex.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/VR Stars.unity (0.0 MB)
- Assets/Plugins/CW/SpaceGraphicsToolkit/Packs/Basic Pack/Water Level.unity (0.1 MB)
- Assets/RealisticCarControllerV3/Demo Scenes/RCC Blank API Test Scene.unity (0.1 MB)
- Assets/RealisticCarControllerV3/Demo Scenes/RCC Blank Test Scene.unity (0.0 MB)
- Assets/RealisticCarControllerV3/Demo Scenes/RCC City (Car Selection with Load Next Scene).unity (1.0 MB)
- Assets/RealisticCarControllerV3/Demo Scenes/RCC City (Car Selection with Loaded Scene).unity (0.9 MB)
- Assets/RealisticCarControllerV3/Demo Scenes/RCC City (Car Selection).unity (0.9 MB)
- Assets/RealisticCarControllerV3/Demo Scenes/RCC City AIO.unity (1.1 MB)
- Assets/RealisticCarControllerV3/Demo Scenes/RCC City.unity (0.6 MB)
- Assets/RealisticCarControllerV3/Demo Scenes/RCC Damage Test Scene.unity (0.1 MB)
- Assets/RealisticCarControllerV3/Demo Scenes/RCC Multiple Terrain Test Scene.unity (0.1 MB)
- Assets/Reversed Interactive/New Gen Urban/Scenes/DayScene.unity (2.4 MB)
- Assets/Reversed Interactive/New Gen Urban/Scenes/NightScene.unity (2.4 MB)
- Assets/Simple Music Player/Scenes/Music Player Landscape.unity (0.0 MB)
- Assets/Simple Music Player/Scenes/Music Player Portrait.unity (0.0 MB)
- Assets/SocketDemo/Scenes/SampleScene.unity (0.3 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/01-  Single Line TextMesh Pro.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/02 - Multi-line TextMesh Pro.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/03 - Line Justification.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/04 - Word Wrapping.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/05 - Style Tags.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/06 - Extra Rich Text Examples.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/07 - Superscript & Subscript Example.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/08 - Improved Text Alignment.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/09 - Margin Tag Example.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/10 - Bullets & Numbered List Example.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/11 - The Style Tag.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/12 - Link Example.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/12a - Text Interactions.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/13 - Soft Hyphenation.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/14 - Multi Font & Sprites.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/15 - Inline Graphics & Sprites.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/16 - Linked text overflow mode example.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/17 - Old Computer Terminal.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/18 - ScrollRect & Masking & Layout.unity (0.3 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/19 - Masking Texture & Soft Mask.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/20 - Input Field with Scrollbar.unity (0.1 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/21 - Script Example.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/22 - Basic Scripting Example.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/23 - Animating Vertex Attributes.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/24 - Surface Shader Example.unity (0.1 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/25 - Sunny Days Example.unity (0.0 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/26 - Dropdown Placeholder Example.unity (0.1 MB)
- Assets/TextMesh Pro/Examples & Extras/Scenes/Benchmark (Floating Text).unity (0.0 MB)
- Assets/Vuplex/WebView/Demos/Scenes/2_CanvasWebViewDemo.unity (0.0 MB)
- Assets/WebGLInput-master/Assets/Scenes/SampleScene.unity (0.1 MB)

## 4. Assets not reachable from build scenes / Resources / project settings
Scripts, asmdefs and text files are excluded (static usage is invisible to AssetDatabase).
Verify before deleting: assets loaded by string path, Addressables, or code-only references will show up here.

Total unreachable: 7261.6 MB across 7482 files

| Folder (2 levels) | MB | Files |
|---|---:|---:|
| Assets/ARC/Assets | 2193.3 | 1829 |
| Assets/AddedImplements/Characters | 778.9 | 367 |
| Assets/CarControllerwithShooting/Models | 680.8 | 214 |
| Assets/_Game/ItemsModel | 315.5 | 24 |
| Assets/_Game/Textures | 286.2 | 435 |
| Assets/FORGE3D/Sci-Fi Effects | 267.5 | 511 |
| Assets/Epic Toon FX/Prefabs | 237.2 | 890 |
| Assets/RealisticCarControllerV3/Models | 201.0 | 376 |
| Assets | 138.3 | 23 |
| Assets/AddedImplements/Materials | 132.5 | 13 |
| Assets/Simple Music Player/Audio | 124.5 | 3 |
| Assets/Textures | 110.3 | 12 |
| Assets/JaneArtWork/Living Room | 93.6 | 183 |
| Assets/_Game/Fonts | 82.5 | 139 |
| Assets/_Game/PlayerModel | 71.5 | 8 |
| Assets/AssetsByME/Standard Assets | 70.5 | 95 |
| Assets/Videos | 67.8 | 2 |
| Assets/Julhiecio TPS Controller/Models | 64.4 | 85 |
| Assets/AddedImplements/Sprites | 61.7 | 57 |
| Assets/_Game/Audio | 61.1 | 32 |
| Assets/GLB/white | 53.3 | 1 |
| Assets/GLB/black | 52.1 | 1 |
| Assets/GLB/Gold | 48.1 | 1 |
| Assets/GLB/metallic_purple | 48.1 | 1 |
| Assets/GLB/Green | 47.6 | 1 |
| Assets/GLB/lava | 47.2 | 1 |
| Assets/Ready Player Me/Avatars | 46.3 | 26 |
| Assets/_Game/Scene | 44.0 | 11 |
| Assets/ARC/Documentation | 40.4 | 6 |
| Assets/RealisticCarControllerV3/Textures | 38.1 | 22 |
| Assets/IDEA/PIMLR | 37.0 | 11 |
| Assets/Textures/asphalt_02_2k | 36.8 | 7 |
| Assets/Models/max | 35.7 | 23 |
| Assets/Textures/forest_leaves_02_2k | 34.3 | 7 |
| Assets/Models | 32.0 | 5 |
| Assets/CarControllerwithShooting/Prefabs | 31.4 | 105 |
| Assets/Textures/aerial_grass_rock_2k | 31.1 | 7 |
| Assets/Reversed Interactive/New Gen Urban | 29.5 | 155 |
| Assets/Ash Assets/Ash Vehicle AI | 26.1 | 37 |
| Assets/CarControllerwithShooting/Scenes | 24.4 | 18 |

Script counts by folder (for spotting out-of-scope game code, e.g. Helix / Humanity Rocks):
- Assets/Extensions/Best HTTP: 1935 scripts
- Assets/ARC/Assets: 310 scripts
- Assets/Julhiecio TPS Controller/Scripts: 128 scripts
- Assets/Vuplex/WebView: 92 scripts
- Assets/RealisticCarControllerV3/Scripts: 81 scripts
- Assets/_Game/Scripts: 77 scripts
- Assets/CarControllerwithShooting/Scripts: 55 scripts
- Assets/DialogueSysyem/EventSystem: 46 scripts
- Assets/_Game/CC-CharaterCustomization: 37 scripts
- Assets/TextMesh Pro/Examples & Extras: 34 scripts
- Assets/FORGE3D/Sci-Fi Effects: 34 scripts
- Assets: 32 scripts
- Assets/AssetsByME/Standard Assets: 30 scripts
- Assets/SocketDemo/Script: 21 scripts
- Assets/Ash Assets/Ash Vehicle AI: 15 scripts

## 5. Packages (Packages/manifest.json)
| Package | Version | Used by build content | Code refs | Verdict | Note |
|---|---|:-:|---:|---|---|
| com.unify.unified-universal-blur | https://github.com/lukakldiashvili/Unified-Universal-Blur.git | yes | 0 | in use |  |
| com.unity.2d.sprite | 1.0.0 | no | 0 | review |  |
| com.unity.ai.navigation | 1.1.6 | yes | 1 | in use |  |
| com.unity.cinemachine | 2.10.3 | yes | 10 | in use | Keep if virtual cameras are used |
| com.unity.collab-proxy | 2.7.1 | no | 0 | REMOVE CANDIDATE | Unity Version Control UI (editor only); unnecessary if you use GitHub |
| com.unity.ide.rider | 3.0.36 | no | 0 | core/editor |  |
| com.unity.ide.visualstudio | 2.0.22 | no | 0 | core/editor |  |
| com.unity.ide.vscode | 1.2.5 | no | 0 | core/editor |  |
| com.unity.inputsystem | 1.14.0 | yes | 20 | in use |  |
| com.unity.nuget.newtonsoft-json | 3.2.1 | no | 0 | review |  |
| com.unity.postprocessing | 3.4.0 | yes | 1 | in use |  |
| com.unity.recorder | 4.0.3 | no | 0 | review |  |
| com.unity.render-pipelines.universal | 14.0.12 | yes | 0 | core/editor |  |
| com.unity.test-framework | 1.1.33 | no | 0 | core/editor |  |
| com.unity.textmeshpro | 3.0.7 | yes | 89 | in use |  |
| com.unity.timeline | 1.7.7 | no | 1 | in use | Keep only if Timeline assets/Playables are used |
| com.unity.ugui | 1.0.0 | yes | 0 | core/editor |  |
| com.unity.visualeffectgraph | 14.0.12 | no | 0 | review |  |
| com.unity.visualscripting | 1.9.4 | no | 3 | in use | Remove if no Visual Scripting graphs exist |

Built-in modules enabled (disable any you don't need, e.g. vr, xr, ai, terrain, cloth, wind, unityanalytics): ai, androidjni, animation, assetbundle, audio, cloth, director, imageconversion, imgui, jsonserialize, particlesystem, physics, physics2d, screencapture, terrain, terrainphysics, tilemap, ui, uielements, umbra, unityanalytics, unitywebrequest, unitywebrequestassetbundle, unitywebrequestaudio, unitywebrequesttexture, unitywebrequestwww, vehicles, video, vr, wind, xr

Also check Assets/Samples/ (package samples imported into the project) and Assets/TextMesh Pro/Examples & Extras (demo scripts, safe to delete).

## 6. Import settings on assets that DO ship (largest offenders first)

### Textures with max size >= 4096 (WebGL override respected) (1)
- 18.9 MB  Assets/NamuFX/StylizedWaterEffects/Textures/PSD/Tex_Particles_01_PSD.psd

### Textures with Read/Write enabled (doubles memory) (91)
- 4.5 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Flames/Textures/flames_red_sprites.tga
- 4.0 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Heat/Textures/heat_005.tga
- 1.5 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Vulcan/Textures/vulcan_heavy_impact.tga
- 1.4 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Warp Jump/Textures/warpjump_ring.psd
- 1.0 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Solo Gun/Textures/Debris_003.tga
- 1.0 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Heat/Textures/heat_001.tga
- 0.5 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Missiles/Textures/missile_001_color.tga
- 0.5 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Missiles/Textures/missile_001_normal.tga
- 0.4 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Laser Impulse/Textures/laser_impulse_impact_star_001.tga
- 0.4 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Missiles/Textures/missile_001_glow.tga
- 0.4 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Plasma Beam/Textures/plasma_beam_heavy_red.tga
- 0.4 MB  Assets/Vuplex/WebView/Core/Editor/Textures/Resources/MockWebViewTexture.png
- 0.3 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Sniper/Textures/sniper_smoke.tga
- 0.3 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Missiles/Textures/missile_004_color.tga
- 0.2 MB  Assets/FORGE3D/Sci-Fi Effects/Effects/Plasma Beam/Textures/plasma_beam_hit_blue.tga

### Textures > 512 KB with compression OFF (2)
- 18.8 MB  Assets/Reversed Interactive/New Gen Urban/Textures/SkyBox.exr
- 0.7 MB  Assets/Plugins/CW/SpaceGraphicsToolkit/Features/Planet/Required/Shaders/PlanetWaterOffset.png

### Models with Read/Write enabled (27)
- 10.1 MB  Assets/RealisticCarControllerV3/Models/Vehicles/Buggy/Buggy.FBX
- 9.2 MB  Assets/Player/Vehicle.fbx
- 4.7 MB  Assets/RealisticCarControllerV3/Models/Vehicles/M3_E46/M3_E46.FBX
- 3.9 MB  Assets/RealisticCarControllerV3/Models/Vehicles/Bus/Bus.FBX
- 3.5 MB  Assets/RealisticCarControllerV3/Models/Vehicles/T-100 Truck/T-100 Truck.FBX
- 3.1 MB  Assets/RealisticCarControllerV3/Models/Vehicles/Sedan/Sedan.FBX
- 2.6 MB  Assets/RealisticCarControllerV3/Models/Vehicles/M3_E36/M3_E36.FBX
- 2.3 MB  Assets/RealisticCarControllerV3/Models/Vehicles/Skala/Skala.FBX
- 2.1 MB  Assets/RealisticCarControllerV3/Models/Model_Skyline by BUMSTRUM.fbx
- 1.2 MB  Assets/RealisticCarControllerV3/Models/Model_Sofie@Driving by BUMSTRUM.fbx
- 0.5 MB  Assets/Julhiecio TPS Controller/Animations/Humanoid Animations/Idle Animations/Neutral Idle.fbx
- 0.5 MB  Assets/Julhiecio TPS Controller/JU Foot Placement/Models/Characters/Old JU Robot/JU Robot.fbx
- 0.3 MB  Assets/Julhiecio TPS Controller/Animations/Humanoid Animations/Walk Animations/Walking.fbx
- 0.2 MB  Assets/Julhiecio TPS Controller/Resources/Editor Resources/Models/Humanoid Visualizer Model.fbx
- 0.1 MB  Assets/RealisticCarControllerV3/Models/Model_Police_Siren.FBX

### Models > 500 KB with mesh compression OFF (4)
- 4.7 MB  Assets/RealisticCarControllerV3/Models/Vehicles/M3_E46/M3_E46.FBX
- 4.2 MB  Assets/Ready Player Me/Avatars/65093c5ef980b990251961de/e7b442390ee9d8733cd7070af1a160c9/Idle.fbx
- 0.7 MB  Assets/Julhiecio TPS Controller/Models/Characters/Cyber Robot/CyberRobotCharacter.fbx
- 0.6 MB  Assets/Julhiecio TPS Controller/Animations/Humanoid Animations/Getting Up/Getting Up From Belly.fbx

### Audio clips > 1 MB (music should Stream; long SFX should be Compressed In Memory) (35)
- 49.2 MB  Assets/_Game/Audio/Thurz x SNDTRAK/Pieces.wav
- 47.8 MB  Assets/_Game/Audio/Yannick Koffi in Time - PIMLR World/05 Mad Max.wav
- 45.4 MB  Assets/_Game/Audio/Thurz x SNDTRAK/type a way w-verse.wav
- 40.1 MB  Assets/_Game/Audio/Yannick Koffi in Time - PIMLR World/13 BITCH IM ME.wav
- 38.2 MB  Assets/_Game/Audio/Thurz x SNDTRAK/Humble Dont Work.wav
- 32.0 MB  Assets/_Game/Audio/Yannick Koffi in Time - PIMLR World/04 Outside (Dirty).wav
- 31.7 MB  Assets/_Game/Audio/Yannick Koffi in Time - PIMLR World/12 Clearly.wav
- 28.6 MB  Assets/_Game/Audio/Thurz x SNDTRAK/093022 THURZ SNDTRAK 2 WORDS r2.wav
- 27.3 MB  Assets/_Game/Audio/Yannick Koffi in Time - PIMLR World/08 Love Don't Live Here.wav
- 27.3 MB  Assets/_Game/Audio/Thurz x SNDTRAK/Love Dont.wav
- 25.0 MB  Assets/_Game/Audio/Yannick Koffi in Time - PIMLR World/14 Deserve.wav
- 21.2 MB  Assets/_Game/Audio/Thurz x SNDTRAK/Untitled.wav
- 19.2 MB  Assets/_Game/Audio/Thurz x SNDTRAK/Situations (Unfinished).wav
- 11.6 MB  Assets/_Game/Audio/Yannick Koffi in Time - PIMLR World/11 Feelin Me.mp3
- 10.1 MB  Assets/_Game/Audio/Yannick Koffi in Time - PIMLR World/NewSound/Thurz - FEEL ME.mp3

## 7. Folders that always ship
- Resources: 41.7 MB in 193 files
- StreamingAssets: 53.2 MB in 2 files
- StreamingAssets video is a common WebGL download-size culprit; check bitrate/resolution.

## 8. Byte-identical duplicate files (>= 100 KB)
Wasted by duplicates: 590.9 MB
- 53.3 MB x 2
    - Assets/GLB/white/white_shuttle.glb
    - Assets/ARC/Assets/Models/SpaceShip/white/white_shuttle.glb
- 52.1 MB x 2
    - Assets/GLB/black/black_shuttle.glb
    - Assets/ARC/Assets/Models/SpaceShip/black/black_shuttle.glb
- 48.1 MB x 2
    - Assets/GLB/Gold/gold_shuttle.glb
    - Assets/ARC/Assets/Models/SpaceShip/Gold/gold_shuttle.glb
- 48.1 MB x 2
    - Assets/GLB/metallic_purple/purple_shuttle.glb
    - Assets/ARC/Assets/Models/SpaceShip/metallic_purple/purple_shuttle.glb
- 47.6 MB x 2
    - Assets/ARC/Assets/Models/SpaceShip/Green/shuttle_green.glb
    - Assets/GLB/Green/shuttle_green.glb
- 47.2 MB x 2
    - Assets/GLB/lava/shuttle_lava.glb
    - Assets/ARC/Assets/Models/SpaceShip/lava/shuttle_lava.glb
- 3.9 MB x 13
    - Assets/ARC/Assets/Textures/UI/Slicing/Graphics/Main_BG.png
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/Graphics/Main_BG.png
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/Game Option/Main_BG.png
    - Assets/ARC/Assets/Textures/UI/Slicing/Loading/Main_BG.png
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/Setting/BG.png
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/Loading/Main_BG.png
    - Assets/ARC/Assets/Textures/UI/Slicing/Setting/BG.png
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/Game play/BG.png
    - Assets/ARC/Assets/Textures/UI/Slicing/Audio/Main_BG.png
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/Audio/Main_BG.png
    - Assets/ARC/Assets/Textures/UI/Slicing/Game Option/Main_BG.png
    - Assets/ARC/Assets/Textures/UI/Slicing/Game play/BG.png
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/Inputs/Main_BG.png
- 5.1 MB x 5
    - Assets/Resources/external 1.mp3
    - Assets/Resources/external.mp3
    - Assets/Resources/external 2.mp3
    - Assets/Resources/external 3.mp3
    - Assets/_Game/Audio/Thurz x Iman Omari LP/Back It Up (mix2).mp3
- 5.0 MB x 5
    - Assets/ARC/Assets/Audio/AudioClips/Music/Music_Gameplay_03.wav
    - Assets/ARC/Assets/Audio/AudioClips/Music/Music_Gameplay_02.wav
    - Assets/ARC/Assets/Audio/AudioClips/Music/Music_RaceComplete.wav
    - Assets/ARC/Assets/Audio/AudioClips/Music/Music_Menu.wav
    - Assets/ARC/Assets/Audio/AudioClips/Music/Music_Gameplay_01.wav
- 13.6 MB x 2
    - Assets/CarControllerwithShooting/Models/Gun/Weapon_56/Zombie_Theme.fbm/Helix_Military_Truck_Weapons_Weapon1_BaseColor.png
    - Assets/CarControllerwithShooting/Models/Gun/Weapons_12/Weapon1.fbm/Helix_Military_Truck_Weapons_Weapon1_BaseColor.png
- 8.8 MB x 2
    - Assets/ARC/Assets/Textures/Env/Misc/Background_01_Albedo.png
    - Assets/ARC/Assets/Terrain/TerrainLayer_Textures/Ground_Text_02_BaseMap.png
- 8.7 MB x 2
    - Assets/ARC/Assets/Textures/Env/Rock/Rock_05_Albedo.png
    - Assets/ARC/Assets/Terrain/TerrainLayer_Textures/Ground_Text_04_BaseMap.png
- 4.1 MB x 3
    - Assets/ARC/Assets/Textures/UI/Slicing/All Images/5.jpg
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/All Images/5.jpg
    - Assets/ARC/Assets/Textures/UI/5.jpg
- 4.0 MB x 3
    - Assets/ARC/Assets/Textures/UI/Slicing/All Images/3.jpg
    - Assets/ARC/Assets/Textures/UI/3.jpg
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/All Images/3.jpg
- 3.9 MB x 3
    - Assets/ARC/Assets/Textures/UI/6.jpg
    - Assets/ARC/Assets/Textures/UI/Slicing/All Images/6.jpg
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/All Images/6.jpg
- 3.9 MB x 3
    - Assets/ARC/Assets/Textures/UI/2.jpg
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/All Images/2.jpg
    - Assets/ARC/Assets/Textures/UI/Slicing/All Images/2.jpg
- 3.8 MB x 3
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/All Images/1.jpg
    - Assets/ARC/Assets/Textures/UI/1.jpg
    - Assets/ARC/Assets/Textures/UI/Slicing/All Images/1.jpg
- 3.8 MB x 3
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/All Images/7.jpg
    - Assets/ARC/Assets/Textures/UI/Slicing/All Images/7.jpg
    - Assets/ARC/Assets/Textures/UI/7.jpg
- 3.7 MB x 3
    - Assets/ARC/Assets/Textures/UI/4.jpg
    - Assets/AddedImplements/Sprites/SpaceshipFlyingGameScene/All Images/4.jpg
    - Assets/ARC/Assets/Textures/UI/Slicing/All Images/4.jpg
- 7.3 MB x 2
    - Assets/Models/Texture.png
    - Assets/Models/max/Texture.png
- 7.3 MB x 2
    - Assets/WebGLTemplates/Better2020/Stryker_14.mp4
    - Assets/StreamingAssets/Loadingvideo.mp4
- 7.0 MB x 2
    - Assets/RealisticCarControllerV3/Models/Highway/Tex/Road_N.tif
    - Assets/RealisticCarControllerV3/Models/Highway/Tex/Road.tif
- 5.4 MB x 2
    - Assets/ARC/Assets/Models/SpaceShip/textures/purple_rock.png
    - Assets/ARC/Assets/Models/SpaceShip/textures/Black rock.png
- 5.1 MB x 2
    - Assets/CarControllerwithShooting/Models/Gun/Weapons_12/Weapon1.fbm/Helix_Military_Truck_Weapons_Weapon1_Normal.png
    - Assets/CarControllerwithShooting/Models/Gun/Weapon_56/Zombie_Theme.fbm/Helix_Military_Truck_Weapons_Weapon1_Normal.png
- 2.5 MB x 3
    - Assets/RealisticCarControllerV3/Models/City/City.fbm/Skyscrapper_6.bmp
    - Assets/RealisticCarControllerV3/Models/City/Tex/N/Skyscrapper_6_N.bmp
    - Assets/RealisticCarControllerV3/Models/City/Tex/Skyscrapper_6.bmp
- 4.3 MB x 2
    - Assets/RealisticCarControllerV3/Models/Vehicles/Buggy/Tex/Buggy_Body_N.png
    - Assets/RealisticCarControllerV3/Models/Vehicles/Buggy/Tex/Buggy_Body.png
- 4.2 MB x 2
    - Assets/RealisticCarControllerV3/Models/Environment/Tree/Tex/Tree_Atlas_Normal.tga
    - Assets/RealisticCarControllerV3/Models/Environment/Textures/Broadleaf_Mobile_Atlas_Normal.tga
- 4.1 MB x 2
    - Assets/ARC/Assets/Textures/UI/Slicing/All Images/9.jpg
    - Assets/ARC/Assets/Textures/UI/9.jpg
- 4.0 MB x 2
    - Assets/RealisticCarControllerV3/Models/Environment/Tree/Tex/TreeBark_Normal.tga
    - Assets/RealisticCarControllerV3/Models/Environment/Textures/BroadleafBark_Normal.tga
- 4.0 MB x 2
    - Assets/RealisticCarControllerV3/Models/Environment/Textures/BroadleafBark.tga
    - Assets/RealisticCarControllerV3/Models/Environment/Tree/Tex/Tree_Bark.tga

