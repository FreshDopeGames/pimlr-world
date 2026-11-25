# PIMLR UI Theme Audit (2026-11-25 01:20)

## 00_MainMenu

| Check | Count |
| --- | --- |
| Images themed / total | 0 / 827 |
| 9-slice sprites drawn as Simple (stretched corners) | 142 |
| Unity default sprites (UISprite, Background, ...) | 152 |
| TMP texts themed / total | 0 / 574 |
| TMP texts using a font outside the theme | 295 |
| TMP texts with no font | 2 |
| Legacy UnityEngine.UI.Text | 123 |
| Buttons themed / total | 0 / 323 |

Screen-space canvases:
- FIX `Global UI/Canvas` (ScaleWithScreenSize 1280x720 match 0.5)
- FIX `Global UI/Canvas/PLMRMainMenuPanel/BgImage/Music2/Canvas` (ConstantPixelSize 800x600 match 0)

## SceneStaticEU

| Check | Count |
| --- | --- |
| Images themed / total | 0 / 533 |
| 9-slice sprites drawn as Simple (stretched corners) | 142 |
| Unity default sprites (UISprite, Background, ...) | 151 |
| TMP texts themed / total | 0 / 381 |
| TMP texts using a font outside the theme | 248 |
| TMP texts with no font | 1 |
| Legacy UnityEngine.UI.Text | 16 |
| Buttons themed / total | 0 / 305 |

Screen-space canvases:
- FIX `DIALOG_SYSTEM/Canvas` (ScaleWithScreenSize 1920x1080 match 1)
- FIX `Music2/Canvas` (ConstantPixelSize 800x600 match 0)
- FIX `Living Room UI/Canvas` (ScaleWithScreenSize 1280x720 match 0.5)
- FIX `Living Room UI/Canvas/PLMRMainMenuPanel/BG Shade/Level Select Music/Canvas` (ConstantPixelSize 800x600 match 0)

## YannicksWorld

| Check | Count |
| --- | --- |
| Images themed / total | 0 / 239 |
| 9-slice sprites drawn as Simple (stretched corners) | 0 |
| Unity default sprites (UISprite, Background, ...) | 63 |
| TMP texts themed / total | 0 / 29 |
| TMP texts using a font outside the theme | 29 |
| TMP texts with no font | 0 |
| Legacy UnityEngine.UI.Text | 61 |
| Buttons themed / total | 0 / 58 |

Screen-space canvases:
- FIX `JUTPS User Interface` (ScaleWithScreenSize 1920x1280 match 0.5)
- FIX `DIALOG_SYSTEM/Canvas` (ScaleWithScreenSize 1920x1080 match 0)
- FIX `PersistentAudioManager/MusicPlayerCanvas` (ScaleWithScreenSize 1080x1920 match 0.5)

## Code-built UI (hardcoded colors)

| Script | new Color literals | Uses theme |
| --- | --- | --- |
| PimlrArtistSelect.cs | 10 | no |
| PimlrMenuNavigator.cs | 3 | no |
| FanHealthBar.cs | 3 | no |
| SaveTheFansHUD.cs | 13 | no |
| PimlrMinimapEnemyBlips.cs | 3 | no |
| PimlrSettingsMenu.cs | 10 | no |
| TutorialPromptUI.cs | 4 | no |
| PimlrMinimap2D.cs | 0 | no |
| PimlrBoostHUD.cs | 3 | no |
| PimlrDeathSequence.cs | 4 | no |
| PimlrTooltip.cs | 2 | no |
| PimlrUISpriteFix.cs | 0 | no |
| PimlrVictorySequence.cs | 3 | no |
| PlayerNameBadge.cs | 3 | no |
| HaterFreeCinematic.cs | 2 | no |
| ScreenFader.cs | 0 | no |

