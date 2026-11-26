# PIMLR Leaderboard Server

Node 18+ API with no npm dependencies, running on the GCP VM as systemd service `pimlr-leaderboard` (127.0.0.1:12100, behind nginx).

- Public: `/api/*`, `/leaderboard` (live board; good on a TV or for weekly screenshots), `/rules`, `/privacy`
- Admin: `/admin` (HTTP Basic auth; any username + `ADMIN_PASSWORD` from `/etc/pimlr-leaderboard.env` on the VM)
- Data: `/var/lib/pimlr-leaderboard` (`players.json`, `runs.jsonl`, `moderation.json`, `winners.json`, `events/*.jsonl`); daily tar backups go to `/var/backups/pimlr-leaderboard`
- Weeks run Monday 12:00 AM through Sunday 11:59 PM Pacific and lock automatically. Ties go to the earlier score.

Test locally: `node test.js`

Deploy or update: upload this folder to `gs://pimlr-world-ops/leaderboard/` (Tools/Deploy/deploy-leaderboard-server.ps1), then on the VM:
```
rm -rf /tmp/pimlr-lb && mkdir -p /tmp/pimlr-lb && gcloud storage cp -r 'gs://pimlr-world-ops/leaderboard/*' /tmp/pimlr-lb/ && sudo bash /tmp/pimlr-lb/install.sh
```

## API (JSON)
| Method | Path | Body / query | Notes |
|---|---|---|---|
| POST | /api/player/register | name, handle?, pin? | 409 `name_taken` (hasPin) |
| POST | /api/player/claim | name, pin | new device key for an existing name |
| POST | /api/player/update | playerId, secret, handle?, pin?, name? | |
| POST | /api/run/start | playerId, secret, mode (`story`/`infinite`) | returns runId; server starts its clock |
| POST | /api/run/clock | playerId, secret, runId | tutorial finished: restart the server clock |
| POST | /api/run/finish | playerId, secret, runId, mode, elapsed, wave, kills, bossKills, coins, startZone, endReason, build | 422 `implausible`; returns ranks |
| GET | /api/leaderboard | cat, period (today/week/all), limit, playerId | cats: story_time, inf_wave, inf_kills, inf_survival |
| GET | /api/leaderboard/all | period, limit, playerId | all four boards at once |
| GET | /api/winners | limit | locked weekly results (names only) |
| POST | /api/events | playerId?, secret?, sessionId, build, events[{name,t,data}] | analytics, max 200 per batch |

Unreal note: same endpoints from `FHttpModule` (C++) or the VaRest plugin (Blueprint). Nothing on the server is Unity-specific.
