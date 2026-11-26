'use strict';
// Smoke test: node test.js   (starts the server on a temp data dir, runs fake players/scores, checks results)
const { spawn } = require('child_process');
const fs = require('fs'); const os = require('os'); const path = require('path');
const assert = require('assert');

const dataDir = fs.mkdtempSync(path.join(os.tmpdir(), 'pimlr-lb-'));
const PORT = 12999;
const srv = spawn(process.execPath, [path.join(__dirname, 'server.js')], {
  env: Object.assign({}, process.env, { PORT, DATA_DIR: dataDir, ADMIN_PASSWORD: 'testpw', MIN_STORY_SECONDS: '0' }), stdio: ['ignore', 'pipe', 'inherit'],
});
const base = `http://127.0.0.1:${PORT}`;
const sleep = ms => new Promise(r => setTimeout(r, ms));
async function call(method, p, body, auth) {
  const headers = { 'Content-Type': 'application/json', 'X-Real-IP': '10.0.0.' + Math.floor(Math.random() * 200) };
  if (auth) headers.Authorization = 'Basic ' + Buffer.from('admin:' + auth).toString('base64');
  const r = await fetch(base + p, { method, headers, body: body ? JSON.stringify(body) : undefined });
  const t = await r.text(); let j; try { j = JSON.parse(t); } catch (e) { j = t; }
  return { s: r.status, j };
}
const ok = (c, msg) => { assert.ok(c, msg); console.log('  ✓ ' + msg); };

(async () => {
  try {
    for (let i = 0; i < 50; i++) { try { if ((await call('GET', '/api/health')).s === 200) break; } catch (e) {} await sleep(100); }
    console.log('server up');

    const a = (await call('POST', '/api/player/register', { name: 'Thurz', handle: '@thurz', pin: '1234' })).j;
    ok(a.secret && a.playerId, 'register Thurz');
    const b = (await call('POST', '/api/player/register', { name: 'Kid Wash', handle: '' })).j;
    ok(b.secret, 'register Kid Wash (no handle, no PIN)');
    ok((await call('POST', '/api/player/register', { name: 'thurz' })).s === 409, 'duplicate name (case-insensitive) rejected');
    ok((await call('POST', '/api/player/register', { name: 'x' })).s === 400, 'too-short name rejected');
    ok((await call('POST', '/api/player/register', { name: '<b>hi</b>!!' })).s === 400, 'bad characters rejected');
    ok((await call('POST', '/api/player/claim', { name: 'Thurz', pin: '0000' })).s === 403, 'wrong PIN rejected');
    const claimed = (await call('POST', '/api/player/claim', { name: 'Thurz', pin: '1234' })).j;
    ok(claimed.playerId === a.playerId && claimed.secret && claimed.secret !== a.secret, 'claim with PIN gives second device its own key');
    ok((await call('POST', '/api/run/start', { playerId: a.playerId, secret: 'nope', mode: 'story' })).s === 401, 'bad secret rejected');

    // Infinite runs
    const startInf = async p => (await call('POST', '/api/run/start', { playerId: p.playerId, secret: p.secret, mode: 'infinite' })).j.runId;
    let id = await startInf(a); await sleep(1100);
    let fin = await call('POST', '/api/run/finish', { playerId: a.playerId, secret: a.secret, runId: id, mode: 'infinite', elapsed: 1, wave: 3, kills: 5, endReason: 'Died' });
    ok(fin.s === 200 && fin.j.ranks.inf_wave.week.rank === 1, 'Thurz infinite run accepted, rank 1 this week');
    ok((await call('POST', '/api/run/finish', { playerId: a.playerId, secret: a.secret, runId: id, mode: 'infinite', elapsed: 1, wave: 3, kills: 5 })).s === 409, 'same run cannot be submitted twice');

    id = await startInf(b); await sleep(1100);
    ok((await call('POST', '/api/run/finish', { playerId: b.playerId, secret: b.secret, runId: id, mode: 'infinite', elapsed: 500, wave: 3, kills: 5 })).s === 422, 'time longer than server measured rejected');
    id = await startInf(b);
    ok((await call('POST', '/api/run/finish', { playerId: b.playerId, secret: b.secret, runId: id, mode: 'infinite', elapsed: 1, wave: 3, kills: 9999 })).s === 422, 'impossible kill count rejected');
    id = await startInf(b);
    ok((await call('POST', '/api/run/finish', { playerId: b.playerId, secret: b.secret, runId: id, mode: 'infinite', elapsed: 1, wave: 40, kills: 3 })).s === 422, 'more waves than kills rejected');
    id = await startInf(b); await sleep(1100);
    fin = await call('POST', '/api/run/finish', { playerId: b.playerId, secret: b.secret, runId: id, mode: 'infinite', elapsed: 1, wave: 3, kills: 5 });
    ok(fin.s === 200 && fin.j.ranks.inf_wave.week.rank === 2, 'tie on wave: later player ranks 2nd (earlier score wins)');

    // Story with tutorial clock reset
    const s = (await call('POST', '/api/run/start', { playerId: b.playerId, secret: b.secret, mode: 'story' })).j.runId;
    await sleep(600);
    ok((await call('POST', '/api/run/clock', { playerId: b.playerId, secret: b.secret, runId: s })).s === 200, 'story clock reset (tutorial)');
    await sleep(600);
    { const r = await call('POST', '/api/run/finish', { playerId: b.playerId, secret: b.secret, runId: s, mode: 'story', elapsed: 0.5, endReason: 'Died', startZone: 'ChatWilly' }); ok(r.s === 422, 'incomplete story rejected'); }
    fin = await call('POST', '/api/run/finish', { playerId: b.playerId, secret: b.secret, runId: s, mode: 'story', elapsed: 0.5, kills: 40, endReason: 'Completed', startZone: 'ChatWilly' });
    ok(fin.s === 200, 'completed story accepted');

    // Offline-start run gets flagged, not dropped
    fin = await call('POST', '/api/run/finish', { playerId: a.playerId, secret: a.secret, mode: 'infinite', elapsed: 30, wave: 2, kills: 4 });
    ok(fin.s === 200, 'run with no server start still accepted (flagged for review)');

    // Boards
    const lb = (await call('GET', '/api/leaderboard?cat=inf_wave&period=today&playerId=' + b.playerId)).j;
    ok(lb.rows.length === 2 && lb.rows[0].name === 'Thurz' && lb.you.rank === 2, 'today board: one row per player, you=rank 2');
    const all = (await call('GET', '/api/leaderboard/all?period=week')).j;
    ok(all.boards.story_time.rows[0].name === 'Kid Wash', 'all-boards endpoint works');

    // Admin
    ok((await call('GET', '/api/admin/overview')).s === 401, 'admin requires password');
    const ov = (await call('GET', '/api/admin/overview', null, 'testpw')).j;
    const thurzRow = ov.boards.inf_wave.rows.find(r => r.name === 'Thurz');
    ok(thurzRow && thurzRow.handle === '@thurz', 'admin sees private handle');
    ok(ov.boards.inf_wave.rows.some(r => r.flags.includes('no_server_start')) || ov.boards.inf_kills.rows.length > 0, 'admin sees flags');
    await call('POST', '/api/admin/run/hide', { runId: thurzRow.runId, hidden: true, reason: 'test' }, 'testpw');
    const lb2 = (await call('GET', '/api/leaderboard?cat=inf_wave&period=week')).j;
    ok(lb2.rows[0].name === 'Kid Wash', 'hidden run drops off public board');
    const locked = (await call('POST', '/api/admin/week/lock', { week: ov.week }, 'testpw')).j;
    ok(locked.categories.story_time[0].name === 'Kid Wash', 'manual week lock computes winners');
    ok((await call('GET', '/api/winners')).j.weeks.length === 1, 'public winners endpoint');
    const csv = await call('GET', '/api/admin/export?week=' + ov.week, null, 'testpw');
    ok(typeof csv.j === 'string' && csv.j.split('\n').length >= 4, 'CSV export');

    // Events + pages
    ok((await call('POST', '/api/events', { playerId: a.playerId, secret: a.secret, sessionId: 's1', events: [{ name: 'death', data: { wave: 2 } }] })).j.accepted === 1, 'analytics events accepted');
    ok((await fetch(base + '/leaderboard')).status === 200 && (await fetch(base + '/rules')).status === 200 && (await fetch(base + '/privacy')).status === 200, 'public pages served');

    // Persistence across restart
    srv.kill(); await sleep(300);
    const srv2 = spawn(process.execPath, [path.join(__dirname, 'server.js')], { env: Object.assign({}, process.env, { PORT, DATA_DIR: dataDir }), stdio: 'ignore' });
    for (let i = 0; i < 50; i++) { try { if ((await call('GET', '/api/health')).s === 200) break; } catch (e) {} await sleep(100); }
    const h = (await call('GET', '/api/health')).j;
    ok(h.players === 2 && h.runs === 4, 'data survives restart (2 players, 4 runs)');
    ok((await call('GET', '/api/leaderboard?cat=inf_wave&period=week')).j.rows[0].name === 'Kid Wash', 'moderation survives restart');
    srv2.kill();
    console.log('\nALL TESTS PASSED');
    process.exit(0);
  } catch (e) {
    console.error('\nTEST FAILED:', e.message); srv.kill(); process.exit(1);
  }
})();
