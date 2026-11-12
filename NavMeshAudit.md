# NavMesh audit — YannicksWorld
_2026-11-12 11:58_ · PIMLR > Navigation > 1. Audit NavMesh

## Bake settings
- Surface `PIMLR_NavMesh`: collect Volume, layers Default, geometry PhysicsColliders, ignore NavMeshObstacle **False**, min region area 2, voxel auto
- Volume: centre (-81.4, 1.5, -125.4), size (538.5, 7.0, 657.8) (y -2.0 to 5.0)
- Agent: radius 0.5, height 2, step 0.75, slope 45°
- NavMesh data: Assets/_Game/Scene/PIMLR_World/YannicksWorld/NavMesh-PIMLR_NavMesh.asset

## Runtime carving
- NavMeshObstacles in the scene: 5390; carving: **1** (static: 1, moving: 0).
- Static carving obstacles without a collider on the bake layers: 1 (Fix 2 gives these a Not Walkable volume).

| Carving obstacles under | Count |
| --- | --- |
| Big Boss | 1 |

## Gaps (standable street-level ground with no NavMesh)
- 0 m² of gaps in 0 regions of 3 m² or more. Edit-mode bake only; runtime carving adds more.

| # | Centre (x, z) | m² | Likely cause | Ground objects |
| --- | --- | --- | --- | --- |

## Islands (NavMesh you can't walk to from the player start)
- Player start (12, 0, 39). Walkable from there: 163542 m² of 163997 m² total.

- Unreachable islands of 10 m² or more: 0, 0 m² in total.

| # | Centre (x, z) | m² | Height (y) | Standing on | Layer |
| --- | --- | --- | --- | --- | --- |

## Mission points (walkable from the player start?)
| Point | Position | Status |
| --- | --- | --- |
| Fan Willie | -27, 0.0, 79 | ok |
| Willie spawn 1 | -4, 0.1, 93 | ok |
| Willie spawn 2 | -2, 0.1, 103 | ok |
| Willie spawn 3 | -13, 0.1, 46 | ok |
| Willie spawn 4 | -14, 0.1, 91 | ok |
| Willie spawn 5 | -12, 0.1, 101 | ok |
| Willie spawn 6 | -13, 0.1, 111 | ok |
| Fan Tiana | -56, 0.0, -65 | ok |
| Tiana spawn 1 | -96, 0.0, -72 | ok |
| Tiana spawn 2 | -52, 0.1, -39 | ok |
| Tiana spawn 3 | -67, 0.1, -28 | ok |
| Tiana spawn 4 | -12, 0.1, -77 | ok |
| Tiana spawn 5 | -80, 0.0, -83 | ok |
| Tiana spawn 6 | -30, 0.0, -66 | ok |
| Fan Jose | -56, 0.0, -172 | ok |
| Jose spawn 1 | -100, 0.1, -198 | ok |
| Jose spawn 2 | -30, 0.0, -177 | ok |
| Jose spawn 3 | -32, 0.1, -187 | ok |
| Jose spawn 4 | -108, 0.1, -172 | ok |
| Jose spawn 5 | -75, 0.1, -129 | ok |
| Jose spawn 6 | -99, 0.1, -189 | ok |
- 0 point(s) not reachable.

## Remaining carving obstacles
- `Big Boss` (static, shape Box)

Scene view: red = gaps, orange = unreachable islands (PIMLR > Navigation > Show / Hide Audit Markers).
