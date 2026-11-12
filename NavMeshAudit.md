# NavMesh audit — YannicksWorld
_2026-11-11 21:58_ · PIMLR > Navigation > 1. Audit NavMesh

## Bake settings
- Surface `PIMLR_NavMesh`: collect Volume, layers Default, geometry PhysicsColliders, ignore NavMeshObstacle **False**, min region area 2, voxel auto
- Volume: centre (-81.4, 1.5, -125.4), size (538.5, 7.0, 657.8) (y -2.0 to 5.0)
- Agent: radius 0.5, height 2, step 0.75, slope 45°
- NavMesh data: Assets/_Game/Scene/PIMLR_World/YannicksWorld/NavMesh-PIMLR_NavMesh.asset

## Runtime carving
- NavMeshObstacles in the scene: 5390; carving: **4** (static: 4, moving: 0).
- Static carving obstacles without a collider on the bake layers: 0 (Fix 2 gives these a Not Walkable volume).

| Carving obstacles under | Count |
| --- | --- |
| Street_WidePath | 3 |
| Street_JunctionWiidePath | 1 |

## Gaps (standable street-level ground with no NavMesh)
- 0 m² of gaps in 0 regions of 3 m² or more. Edit-mode bake only; runtime carving adds more.

| # | Centre (x, z) | m² | Likely cause | Ground objects |
| --- | --- | --- | --- | --- |

## Islands (NavMesh you can't walk to from the player start)
- Player start (31, 0, 31). Walkable from there: 163533 m² of 310560 m² total.

- Unreachable islands of 10 m² or more: 368, 146547 m² in total.

| # | Centre (x, z) | m² | Height (y) | Standing on | Layer |
| --- | --- | --- | --- | --- | --- |
| 1 | -263, -237 | 8155 | -0.2 | PlayGround | Default |
| 2 | 183, -119 | 4940 | -0.2 | PlayGround | Default |
| 3 | -113, -385 | 4586 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (12) | Default |
| 4 | -207, -17 | 4583 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (3) | Default |
| 5 | 111, -17 | 4583 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (5) | Default |
| 6 | -48, -123 | 4583 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (6) | Default |
| 7 | -207, -378 | 4581 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (2) | Default |
| 8 | 111, -378 | 4581 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (9) | Default |
| 9 | -113, -25 | 4578 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (4) | Default |
| 10 | 46, -131 | 4578 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (13) | Default |
| 11 | -272, -131 | 4578 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (8) | Default |
| 12 | 112, -230 | 4348 | 0.1 | Plot_6X6/Plot_6X6 (7) | Default |
| 13 | -47, 130 | 4342 | 0.1 | Plot_6X6/Plot_6X6 (8) | Default |
| 14 | -272, -279 | 4342 | 0.1 | Plot_6X6/Plot_6X6 (11) | Default |
| 15 | 46, 82 | 4338 | 0.1 | Plot_6X6/Plot_6X6 (4) | Default |
| 16 | -272, 82 | 4338 | 0.1 | Plot_6X6/Plot_6X6 (10) | Default |
| 17 | -202, -120 | 3528 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (8) | Default |
| 18 | 116, -120 | 3528 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (13) | Default |
| 19 | -43, -14 | 3528 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (4) | Default |
| 20 | -43, -374 | 3523 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (12) | Default |
| 21 | 41, -389 | 3518 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (9) | Default |
| 22 | -277, -389 | 3518 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (2) | Default |
| 23 | -118, -134 | 3516 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (6) | Default |
| 24 | -277, -28 | 3516 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (3) | Default |
| 25 | 41, -29 | 3509 | 0.1 | Plot_4X6 Builds1/Plot_4X6 Builds1 (5) | Default |
| 26 | 115, 93 | 3372 | 0.1 | Plot_6X6/Plot_6X6 (4) | Default |
| 27 | -203, 93 | 3371 | 0.1 | Plot_6X6/Plot_6X6 (10) | Default |
| 28 | -203, -267 | 3370 | 0.1 | Plot_6X6/Plot_6X6 (11) | Default |
| 29 | -116, 119 | 3366 | 0.1 | Plot_6X6/Plot_6X6 (8) | Default |
| 30 | 43, -241 | 3359 | 0.1 | Plot_6X6/Plot_6X6 (7) | Default |
| 31 | -81, 201 | 2453 | -0.2 | PlayGround | Default |
| 32 | 111, -307 | 1086 | 0.1 | Plot_6X6/Plot_6X6 (7) | Default |
| 33 | -271, -201 | 1080 | 0.1 | Plot_6X6/Plot_6X6 (11) | Default |
| 34 | -48, 53 | 1076 | 0.1 | Plot_6X6/Plot_6X6 (8) | Default |
| 35 | 47, 159 | 1073 | 0.1 | Plot_6X6/Plot_6X6 (4) | Default |
| 36 | -271, 159 | 1073 | 0.1 | Plot_6X6/Plot_6X6 (10) | Default |
| 37 | 41, -307 | 898 | 0.1 | Plot_6X6/Plot_6X6 (7) | Default |
| 38 | -202, -201 | 895 | 0.1 | Plot_6X6/Plot_6X6 (11) | Default |
| 39 | 116, 159 | 890 | 0.1 | Plot_6X6/Plot_6X6 (4) | Default |
| 40 | -202, 159 | 890 | 0.1 | Plot_6X6/Plot_6X6 (10) | Default |

## Mission points (walkable from the player start?)
| Point | Position | Status |
| --- | --- | --- |
| Fan Jose | -56, 0.0, -172 | ok |
| Jose spawn 1 | -100, 0.1, -198 | ok |
| Jose spawn 2 | -30, 0.0, -177 | ok |
| Jose spawn 3 | -32, 0.1, -187 | ok |
| Jose spawn 4 | -108, 0.1, -172 | ok |
| Jose spawn 5 | -75, 0.1, -129 | ok |
| Jose spawn 6 | -99, 0.1, -189 | ok |
| Fan Tiana | -56, 0.0, -65 | ok |
| Tiana spawn 1 | -91, 0.0, -65 | ok |
| Tiana spawn 2 | -61, 0.1, -39 | ok |
| Tiana spawn 3 | -81, 0.1, -41 | ok |
| Tiana spawn 4 | -69, 0.1, -33 | ok |
| Tiana spawn 5 | -80, 0.0, -83 | ok |
| Tiana spawn 6 | -30, 0.0, -66 | ok |
| Fan Willie | -27, 0.0, 79 | ok |
| Willie spawn 1 | -4, 0.1, 93 | ok |
| Willie spawn 2 | -2, 0.1, 103 | ok |
| Willie spawn 3 | -13, 0.1, 46 | ok |
| Willie spawn 4 | -14, 0.1, 91 | ok |
| Willie spawn 5 | -12, 0.1, 101 | ok |
| Willie spawn 6 | -13, 0.1, 111 | ok |
- 0 point(s) not reachable.

## Remaining carving obstacles
- `Enviroment/Street_WidePath (31)/StreetBlock2/StreetBlock2` (static, shape Box)
- `Enviroment/Street_WidePath (37)/BenchMetal1 (3)` (static, shape Box)
- `Enviroment/Street_JunctionWiidePath (30)/TrafficLightTwin1NY` (static, shape Box)
- `Enviroment/Street_WidePath (26)/BenchMetal1 (1)` (static, shape Box)

Scene view: red = gaps, orange = unreachable islands (PIMLR > Navigation > Show / Hide Audit Markers).
