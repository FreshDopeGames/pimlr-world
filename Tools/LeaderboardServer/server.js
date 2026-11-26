'use strict';
// PIMLR World leaderboard + analytics API.
// Zero npm dependencies (Node 18+): data lives in JSON / JSON-lines files under DATA_DIR,
// loaded into memory at start. Sized for playtests and weekly contests (tens of thousands of runs).
//
// Engine-agnostic on purpose: Unity (UnityWebRequest) and a future Unreal build (FHttpModule / VaRest)
// talk to the same plain JSON-over-HTTPS endpoints.

const http = require('http');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

// ---------------------------------------------------------------- config
const PORT = parseInt(process.env.PORT || '12100', 10);
const HOST = process.env.HOST || '127.0.0.1';
const DATA_DIR = process.env.DATA_DIR || path.join(__dirname, 'data');
const PUBLIC_DIR = path.join(__dirname, 'public');
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || '';
const TZ = 'America/Los_Angeles';
const ALLOWED_ORIGINS = (process.env.ALLOWED_ORIGINS || 'https://pimlrworld.com,https://www.pimlrworld.com')
  .split(',').map(s => s.trim()).filter(Boolean);
const MIN_STORY_SECONDS = parseFloat(process.env.MIN_STORY_SECONDS || '120');
const CLOCK_SLACK = 30;            // seconds a client time may exceed what the server measured
const RUN_TTL_MS = 6 * 3600e3;     // open runs older than this are dropped from memory

const CATEGORIES = {
  story_time:   { mode: 'story',    field: 'elapsed',  dir: 'asc',  label: 'Story: Fastest Run',      unit: 'time' },
  inf_wave:     { mode: 'infinite', field: 'wave',     dir: 'desc', label: 'Infinite: Most Waves',    unit: 'wave' },
  inf_kills:    { mode: 'infinite', field: 'kills',    dir: 'desc', label: 'Infinite: Most Washes',   unit: 'kills' },
  inf_survival: { mode: 'infinite', field: 'elapsed',  dir: 'desc', label: 'Infinite: Longest Run',   unit: 'time' },
};
const PERIODS = ['today', 'week', 'all'];

// ---------------------------------------------------------------- storage
fs.mkdirSync(DATA_DIR, { recursive: true });
fs.mkdirSync(path.join(DATA_DIR, 'events'), { recursive: true });
const F = {
  players: path.join(DATA_DIR, 'players.json'),
  runs: path.join(DATA_DIR, 'runs.jsonl'),
  moderation: path.join(DATA_DIR, 'moderation.json'),
  winners: path.join(DATA_DIR, 'winners.json'),
};

function readJson(file, fallback) {
  try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch (e) { return fallback; }
}
function writeJsonAtomic(file, obj) {
  const tmp = file + '.tmp';
  fs.writeFileSync(tmp, JSON.stringify(obj));
  fs.renameSync(tmp, file);
}

const players = readJson(F.players, {});          // id -> player
const moderation = readJson(F.moderation, { hiddenRuns: {} });
moderation.hiddenRuns = moderation.hiddenRuns || {};
const winners = readJson(F.winners, {});          // weekStart -> snapshot
const runs = [];                                   // finished runs, append-only
const runsById = new Map();
const nameIndex = new Map();                       // nameKey -> playerId
const openRuns = new Map();                        // runId -> { playerId, mode, serverStart, createdAt }

for (const p of Object.values(players)) nameIndex.set(p.nameKey, p.id);
if (fs.existsSync(F.runs)) {
  for (const line of fs.readFileSync(F.runs, 'utf8').split('\n')) {
    if (!line.trim()) continue;
    try { const r = JSON.parse(line); runs.push(r); runsById.set(r.id, r); } catch (e) { /* skip torn line */ }
  }
}
const savePlayers = () => writeJsonAtomic(F.players, players);
const saveModeration = () => writeJsonAtomic(F.moderation, moderation);
const saveWinners = () => writeJsonAtomic(F.winners, winners);
function appendRun(r) {
  fs.appendFileSync(F.runs, JSON.stringify(r) + '\n');
  runs.push(r); runsById.set(r.id, r);
}

// ---------------------------------------------------------------- time (Pacific)
const ptFmt = new Intl.DateTimeFormat('en-US', { timeZone: TZ, year: 'numeric', month: '2-digit', day: '2-digit', weekday: 'short' });
const WD = { Mon: 0, Tue: 1, Wed: 2, Thu: 3, Fri: 4, Sat: 5, Sun: 6 };
function ptDay(ms) {
  const p = Object.fromEntries(ptFmt.formatToParts(new Date(ms)).map(x => [x.type, x.value]));
  return { key: `${p.year}-${p.month}-${p.day}`, wd: WD[p.weekday] };
}
function addDays(key, n) {
  const d = new Date(key + 'T12:00:00Z'); d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
}
// Weeks run Monday 00:00 PT -> Sunday 11:59:59 PM PT, keyed by the Monday's date.
function weekKeyOf(ms) { const d = ptDay(ms); return addDays(d.key, -d.wd); }
function dayKeyOf(ms) { return ptDay(ms).key; }

// ---------------------------------------------------------------- helpers
const newId = (n = 12) => crypto.randomBytes(n).toString('hex');
const sha256 = s => crypto.createHash('sha256').update(String(s)).digest('hex');
function hashPin(pin, salt) { return crypto.scryptSync(String(pin), salt, 32).toString('hex'); }
function safeEq(a, b) {
  const A = Buffer.from(String(a)), B = Buffer.from(String(b));
  return A.length === B.length && crypto.timingSafeEqual(A, B);
}
function cleanName(raw) {
  let s = String(raw == null ? '' : raw).replace(/[<>]/g, '').replace(/[\u0000-\u001f\u007f]/g, '').replace(/\s+/g, ' ').trim();
  if (s.length < 2 || s.length > 12) return null;
  if (!/^[\p{L}\p{N} _.\-]+$/u.test(s)) return null;
  return s;
}
const nameKeyOf = n => n.toLowerCase().replace(/\s+/g, '');
function cleanHandle(raw) {
  if (raw == null) return '';
  const s = String(raw).replace(/[<>\u0000-\u001f\u007f]/g, '').trim().slice(0, 60);
  return s;
}
function cleanPin(raw) {
  if (raw == null || raw === '') return '';
  const s = String(raw).trim();
  return /^\d{4}$/.test(s) ? s : null;
}
// Missing fields count as 0 (Story runs don't send wave, etc.); present-but-garbage values are rejected.
const num = (v, min, max) => { if (v === undefined || v === null || v === '') v = 0; const n = Number(v); return Number.isFinite(n) ? Math.min(max, Math.max(min, n)) : NaN; };

function authPlayer(body) {
  const p = players[String(body.playerId || '')];
  if (!p || !body.secret) return null;
  const h = sha256(body.secret);
  return p.secretHashes.some(x => safeEq(x, h)) ? p : null;
}
function publicPlayer(p) { return { playerId: p.id, name: p.name, hasHandle: !!p.handle, hasPin: !!p.pinHash }; }

// ---------------------------------------------------------------- rate limiting
const buckets = new Map();
function limited(key, max, windowMs) {
  const now = Date.now();
  let b = buckets.get(key);
  if (!b || now - b.start > windowMs) { b = { start: now, n: 0 }; buckets.set(key, b); }
  b.n++;
  return b.n > max;
}
setInterval(() => {
  const now = Date.now();
  for (const [k, b] of buckets) if (now - b.start > 3600e3) buckets.delete(k);
  for (const [k, r] of openRuns) if (now - r.createdAt > RUN_TTL_MS) openRuns.delete(k);
}, 60e3).unref();

// ---------------------------------------------------------------- boards
function visibleRun(r) {
  if (moderation.hiddenRuns[r.id]) return false;
  const p = players[r.playerId];
  return !!p && !p.banned;
}
function inPeriod(r, period, nowMs, weekKey) {
  if (period === 'all') return true;
  if (period === 'today') return r.day === dayKeyOf(nowMs);
  return r.week === (weekKey || weekKeyOf(nowMs));
}
// Best run per player for a category; ties go to the earlier finish.
function board(cat, period, opts = {}) {
  const c = CATEGORIES[cat];
  const now = opts.now || Date.now();
  const best = new Map();
  for (const r of runs) {
    if (r.mode !== c.mode || !inPeriod(r, period, now, opts.weekKey)) continue;
    if (!opts.includeHidden && !visibleRun(r)) continue;
    const cur = best.get(r.playerId);
    if (!cur || better(r, cur, c)) best.set(r.playerId, r);
  }
  const list = [...best.values()].sort((a, b) => better(a, b, c) ? -1 : better(b, a, c) ? 1 : 0);
  return list;
}
function better(a, b, c) {
  const av = a[c.field], bv = b[c.field];
  if (av !== bv) return c.dir === 'asc' ? av < bv : av > bv;
  return a.finishedAt < b.finishedAt; // earlier score wins ties
}
function rowOut(r, rank, c, extra) {
  const p = players[r.playerId];
  const o = { rank, name: p ? p.name : '?', value: r[c.field], at: r.finishedAt };
  if (c.mode === 'infinite') Object.assign(o, { wave: r.wave, kills: r.kills, survival: r.elapsed });
  else Object.assign(o, { kills: r.kills });
  if (extra) Object.assign(o, extra(r, p));
  return o;
}
function boardResponse(cat, period, limit, playerId) {
  const c = CATEGORIES[cat];
  const list = board(cat, period);
  const rows = list.slice(0, limit).map((r, i) => rowOut(r, i + 1, c, (rr) => ({ you: rr.playerId === playerId })));
  let you = null;
  if (playerId) {
    const i = list.findIndex(r => r.playerId === playerId);
    if (i >= 0) you = rowOut(list[i], i + 1, c);
  }
  return { cat, label: c.label, unit: c.unit, period, total: list.length, rows, you };
}
function ranksFor(playerId) {
  const out = {};
  for (const cat of Object.keys(CATEGORIES)) {
    out[cat] = {};
    for (const period of PERIODS) {
      const list = board(cat, period);
      const i = list.findIndex(r => r.playerId === playerId);
      out[cat][period] = i >= 0 ? { rank: i + 1, of: list.length } : null;
    }
  }
  return out;
}

// ---------------------------------------------------------------- weekly winners
function snapshotWeek(weekKey, reason) {
  const snap = { week: weekKey, endsPT: addDays(weekKey, 6) + ' 11:59 PM PT', computedAt: new Date().toISOString(), reason, categories: {} };
  for (const [cat, c] of Object.entries(CATEGORIES)) {
    snap.categories[cat] = board(cat, 'week', { weekKey }).slice(0, 3).map((r, i) =>
      rowOut(r, i + 1, c, () => ({ runId: r.id, playerId: r.playerId })));
  }
  winners[weekKey] = snap;
  saveWinners();
  return snap;
}
function lockFinishedWeeks() {
  const current = weekKeyOf(Date.now());
  const weeks = new Set(runs.map(r => r.week));
  for (const w of weeks) if (w < current && !winners[w]) {
    snapshotWeek(w, 'auto');
    console.log(`[weekly] locked week ${w}`);
  }
}
setInterval(lockFinishedWeeks, 60e3).unref();
function publicWinners(limit) {
  return Object.values(winners).sort((a, b) => b.week.localeCompare(a.week)).slice(0, limit).map(w => ({
    week: w.week, endsPT: w.endsPT,
    categories: Object.fromEntries(Object.entries(w.categories).map(([k, rows]) =>
      [k, rows.map(r => ({ rank: r.rank, name: r.name, value: r.value }))])),
  }));
}

// ---------------------------------------------------------------- HTTP plumbing
function send(res, status, obj, headers = {}) {
  const body = typeof obj === 'string' ? obj : JSON.stringify(obj);
  res.writeHead(status, Object.assign({
    'Content-Type': typeof obj === 'string' ? 'text/plain; charset=utf-8' : 'application/json; charset=utf-8',
    'Cache-Control': 'no-store',
  }, headers));
  res.end(body);
}
function readBody(req, max = 65536) {
  return new Promise((resolve, reject) => {
    let size = 0; const chunks = [];
    req.on('data', c => { size += c.length; if (size > max) { reject(new Error('too_large')); req.destroy(); } else chunks.push(c); });
    req.on('end', () => {
      if (!chunks.length) return resolve({});
      try { resolve(JSON.parse(Buffer.concat(chunks).toString('utf8'))); } catch (e) { reject(new Error('bad_json')); }
    });
    req.on('error', reject);
  });
}
function clientIp(req) { return String(req.headers['x-real-ip'] || req.socket.remoteAddress || ''); }
function cors(req, res) {
  const o = req.headers.origin;
  if (o && ALLOWED_ORIGINS.includes(o)) {
    res.setHeader('Access-Control-Allow-Origin', o);
    res.setHeader('Vary', 'Origin');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
  }
}
function isAdmin(req) {
  if (!ADMIN_PASSWORD) return false;
  const h = req.headers.authorization || '';
  if (!h.startsWith('Basic ')) return false;
  const [, pass] = Buffer.from(h.slice(6), 'base64').toString('utf8').split(/:(.*)/s);
  return pass != null && safeEq(sha256(pass), sha256(ADMIN_PASSWORD));
}
const MIME = { '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8', '.js': 'application/javascript; charset=utf-8', '.png': 'image/png', '.svg': 'image/svg+xml' };
function servePage(res, file) {
  try {
    const buf = fs.readFileSync(path.join(PUBLIC_DIR, file));
    res.writeHead(200, { 'Content-Type': MIME[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-cache' });
    res.end(buf);
  } catch (e) { send(res, 404, 'not found'); }
}

// ---------------------------------------------------------------- routes
const routes = {};
const route = (method, p, fn) => { routes[method + ' ' + p] = fn; };

route('GET', '/api/health', () => [200, { ok: true, players: Object.keys(players).length, runs: runs.length, week: weekKeyOf(Date.now()), today: dayKeyOf(Date.now()) }]);

route('POST', '/api/player/register', (b, req) => {
  if (limited('reg:' + clientIp(req), 20, 3600e3)) return [429, { error: 'rate_limited' }];
  const name = cleanName(b.name);
  if (!name) return [400, { error: 'bad_name', message: 'Names are 2-12 letters, numbers, spaces, _ . -' }];
  const pin = cleanPin(b.pin);
  if (pin === null) return [400, { error: 'bad_pin', message: 'PIN must be 4 digits.' }];
  const key = nameKeyOf(name);
  const existing = players[nameIndex.get(key)];
  if (existing) return [409, { error: 'name_taken', hasPin: !!existing.pinHash, message: existing.pinHash ? 'That name is taken. Enter its PIN to use it here.' : 'That name is taken. Pick another.' }];
  const id = newId(8), secret = newId(24), salt = newId(8);
  players[id] = {
    id, name, nameKey: key, handle: cleanHandle(b.handle),
    pinSalt: pin ? salt : '', pinHash: pin ? hashPin(pin, salt) : '',
    secretHashes: [sha256(secret)], createdAt: new Date().toISOString(), banned: false,
  };
  nameIndex.set(key, id);
  savePlayers();
  return [200, Object.assign({ secret }, publicPlayer(players[id]))];
});

route('POST', '/api/player/claim', (b, req) => {
  const name = cleanName(b.name);
  if (!name) return [400, { error: 'bad_name' }];
  const key = nameKeyOf(name);
  if (limited('claim-ip:' + clientIp(req), 10, 15 * 60e3) || limited('claim-name:' + key, 5, 15 * 60e3))
    return [429, { error: 'rate_limited', message: 'Too many tries. Wait 15 minutes.' }];
  const p = players[nameIndex.get(key)];
  const pin = cleanPin(b.pin);
  if (!p || !p.pinHash || !pin || !safeEq(hashPin(pin, p.pinSalt), p.pinHash))
    return [403, { error: 'wrong_pin', message: 'Name or PIN is wrong.' }];
  if (p.banned) return [403, { error: 'banned' }];
  const secret = newId(24);
  p.secretHashes.push(sha256(secret));
  if (p.secretHashes.length > 20) p.secretHashes.shift();
  savePlayers();
  return [200, Object.assign({ secret }, publicPlayer(p))];
});

route('POST', '/api/player/update', (b) => {
  const p = authPlayer(b);
  if (!p) return [401, { error: 'auth' }];
  if (b.handle !== undefined) p.handle = cleanHandle(b.handle);
  if (b.pin !== undefined) {
    const pin = cleanPin(b.pin);
    if (pin === null) return [400, { error: 'bad_pin', message: 'PIN must be 4 digits.' }];
    p.pinSalt = pin ? newId(8) : ''; p.pinHash = pin ? hashPin(pin, p.pinSalt) : '';
  }
  if (b.name !== undefined) {
    const name = cleanName(b.name);
    if (!name) return [400, { error: 'bad_name' }];
    const key = nameKeyOf(name);
    if (key !== p.nameKey) {
      if (nameIndex.has(key)) return [409, { error: 'name_taken' }];
      nameIndex.delete(p.nameKey); p.name = name; p.nameKey = key; nameIndex.set(key, p.id);
    } else p.name = name;
  }
  savePlayers();
  return [200, publicPlayer(p)];
});

route('POST', '/api/run/start', (b, req) => {
  const p = authPlayer(b);
  if (!p) return [401, { error: 'auth' }];
  if (p.banned) return [403, { error: 'banned' }];
  if (limited('start:' + p.id, 60, 3600e3) || limited('start-ip:' + clientIp(req), 300, 3600e3)) return [429, { error: 'rate_limited' }];
  const mode = b.mode === 'story' ? 'story' : b.mode === 'infinite' ? 'infinite' : null;
  if (!mode) return [400, { error: 'bad_mode' }];
  const runId = newId(10);
  openRuns.set(runId, { playerId: p.id, mode, serverStart: Date.now(), createdAt: Date.now() });
  return [200, { runId }];
});

route('POST', '/api/run/clock', (b) => {
  const p = authPlayer(b);
  if (!p) return [401, { error: 'auth' }];
  const r = openRuns.get(String(b.runId || ''));
  if (!r || r.playerId !== p.id) return [404, { error: 'no_run' }];
  r.serverStart = Date.now(); r.clockRestarted = true;
  return [200, { ok: true }];
});

route('POST', '/api/run/finish', (b, req) => {
  const p = authPlayer(b);
  if (!p) return [401, { error: 'auth' }];
  if (p.banned) return [403, { error: 'banned' }];
  if (limited('finish:' + p.id, 30, 3600e3)) return [429, { error: 'rate_limited' }];
  const runId = String(b.runId || '');
  if (runId && runsById.has(runId)) return [409, { error: 'duplicate' }];
  const mode = b.mode === 'story' ? 'story' : b.mode === 'infinite' ? 'infinite' : null;
  if (!mode) return [400, { error: 'bad_mode' }];

  const open = runId ? openRuns.get(runId) : null;
  const now = Date.now();
  const flags = [];
  let serverElapsed = null;
  if (open && open.playerId === p.id && open.mode === mode) {
    serverElapsed = (now - open.serverStart) / 1000;
  } else {
    flags.push('no_server_start'); // offline start, server restart, or crafted request: keep it, flag it for review
  }

  const elapsed = num(b.elapsed, 0, 24 * 3600);
  const wave = Math.round(num(b.wave, 0, 100000));
  const kills = Math.round(num(b.kills, 0, 1000000));
  const bossKills = Math.round(num(b.bossKills, 0, 100000));
  const coins = Math.round(num(b.coins, 0, 100000000));
  if ([elapsed, wave, kills].some(Number.isNaN)) return [400, { error: 'bad_numbers' }];

  const reject = msg => { console.log(`[reject] ${p.name} ${mode}: ${msg}`); return [422, { error: 'implausible', message: msg }]; };
  if (serverElapsed !== null && elapsed > serverElapsed + CLOCK_SLACK) return reject('time longer than the server measured');
  if (mode === 'story') {
    if (b.endReason !== 'Completed') return reject('story run not completed');
    if (b.startZone && b.startZone !== 'ChatWilly') return reject('story run did not start at chapter 1');
    if (elapsed < MIN_STORY_SECONDS) return reject('story time below minimum');
    if (serverElapsed !== null && elapsed < serverElapsed * 0.6 - 30) flags.push('fast_vs_server');
  } else {
    if (wave < 1) return reject('wave < 1');
    if (wave > kills + 2) return reject('more waves than kills allow');
    const window = serverElapsed !== null ? serverElapsed : elapsed;
    if (kills > window * 3 + 20) return reject('kills faster than possible');
  }

  const id = runId || ('x' + newId(10));
  if (open) openRuns.delete(runId);
  const rec = {
    id, playerId: p.id, mode, elapsed: Math.round(elapsed * 100) / 100, wave, kills, bossKills, coins,
    startZone: String(b.startZone || '').slice(0, 32), endReason: String(b.endReason || '').slice(0, 16),
    build: String(b.build || '').slice(0, 40), serverElapsed: serverElapsed === null ? null : Math.round(serverElapsed),
    clockRestarted: !!(open && open.clockRestarted), flags,
    finishedAt: new Date(now).toISOString(), day: dayKeyOf(now), week: weekKeyOf(now), ipHash: sha256(clientIp(req)).slice(0, 12),
  };
  appendRun(rec);
  return [200, { ok: true, runId: id, ranks: ranksFor(p.id) }];
});

route('GET', '/api/leaderboard', (q) => {
  const cat = CATEGORIES[q.get('cat')] ? q.get('cat') : 'story_time';
  const period = PERIODS.includes(q.get('period')) ? q.get('period') : 'week';
  const limit = Math.max(1, Math.min(100, parseInt(q.get('limit') || '10', 10) || 10));
  return [200, Object.assign(boardResponse(cat, period, limit, q.get('playerId') || ''), periodInfo())];
});

route('GET', '/api/leaderboard/all', (q) => {
  const period = PERIODS.includes(q.get('period')) ? q.get('period') : 'week';
  const limit = Math.max(1, Math.min(50, parseInt(q.get('limit') || '10', 10) || 10));
  const boards = {};
  for (const cat of Object.keys(CATEGORIES)) boards[cat] = boardResponse(cat, period, limit, q.get('playerId') || '');
  return [200, Object.assign({ period, boards }, periodInfo())];
});

route('GET', '/api/winners', (q) => [200, { weeks: publicWinners(Math.min(20, parseInt(q.get('limit') || '4', 10) || 4)) }]);

function periodInfo() {
  const now = Date.now(); const w = weekKeyOf(now);
  return { today: dayKeyOf(now), weekStart: w, weekEndsPT: addDays(w, 6) + ' 11:59 PM PT', serverTime: new Date(now).toISOString() };
}

route('POST', '/api/events', (b, req) => {
  if (limited('ev-ip:' + clientIp(req), 120, 60e3)) return [429, { error: 'rate_limited' }];
  const list = Array.isArray(b.events) ? b.events.slice(0, 200) : [];
  if (!list.length) return [400, { error: 'no_events' }];
  const p = authPlayer(b);
  const now = Date.now();
  const lines = list.map(e => JSON.stringify({
    rx: new Date(now).toISOString(), pid: p ? p.id : null, sid: String(b.sessionId || '').slice(0, 40),
    build: String(b.build || '').slice(0, 40), name: String((e && e.name) || '').slice(0, 64),
    t: (e && e.t) || null, data: e && typeof e.data === 'object' ? e.data : undefined,
  })).join('\n') + '\n';
  fs.appendFileSync(path.join(DATA_DIR, 'events', dayKeyOf(now) + '.jsonl'), lines);
  return [200, { ok: true, accepted: list.length }];
});

// ---- admin (HTTP Basic auth; any username, password = ADMIN_PASSWORD)
function adminRows(cat, weekKey) {
  const c = CATEGORIES[cat];
  return board(cat, 'week', { weekKey, includeHidden: true }).slice(0, 25).map((r, i) => {
    const p = players[r.playerId] || {};
    return {
      rank: i + 1, runId: r.id, playerId: r.playerId, name: p.name, handle: p.handle || '', banned: !!p.banned,
      value: r[c.field], wave: r.wave, kills: r.kills, elapsed: r.elapsed, serverElapsed: r.serverElapsed,
      clockRestarted: r.clockRestarted, flags: r.flags, hidden: moderation.hiddenRuns[r.id] || null, at: r.finishedAt,
    };
  });
}
route('GET', '/api/admin/overview', (q) => {
  const weekKey = /^\d{4}-\d{2}-\d{2}$/.test(q.get('week') || '') ? q.get('week') : weekKeyOf(Date.now());
  const boards = {};
  for (const cat of Object.keys(CATEGORIES)) boards[cat] = { label: CATEGORIES[cat].label, unit: CATEGORIES[cat].unit, rows: adminRows(cat, weekKey) };
  const weeks = [...new Set(runs.map(r => r.week))].sort().reverse();
  return [200, { week: weekKey, current: weekKeyOf(Date.now()), weeks, boards, snapshot: winners[weekKey] || null,
    winnersHandles: winners[weekKey] ? Object.fromEntries(Object.entries(winners[weekKey].categories).map(([k, rows]) =>
      [k, rows.map(r => ({ rank: r.rank, name: r.name, handle: (players[r.playerId] || {}).handle || '' }))])) : null,
    totals: { players: Object.keys(players).length, runs: runs.length } }];
}, true);
route('POST', '/api/admin/run/hide', (b) => {
  const id = String(b.runId || '');
  if (!runsById.has(id)) return [404, { error: 'no_run' }];
  if (b.hidden) moderation.hiddenRuns[id] = { reason: String(b.reason || '').slice(0, 200), at: new Date().toISOString() };
  else delete moderation.hiddenRuns[id];
  saveModeration();
  return [200, { ok: true }];
}, true);
route('POST', '/api/admin/player/ban', (b) => {
  const p = players[String(b.playerId || '')];
  if (!p) return [404, { error: 'no_player' }];
  p.banned = !!b.banned; savePlayers();
  return [200, { ok: true }];
}, true);
route('POST', '/api/admin/player/rename', (b) => {
  const p = players[String(b.playerId || '')];
  const name = cleanName(b.name);
  if (!p || !name) return [400, { error: 'bad_request' }];
  const key = nameKeyOf(name);
  if (key !== p.nameKey && nameIndex.has(key)) return [409, { error: 'name_taken' }];
  nameIndex.delete(p.nameKey); p.name = name; p.nameKey = key; nameIndex.set(key, p.id); savePlayers();
  return [200, { ok: true }];
}, true);
route('POST', '/api/admin/week/lock', (b) => {
  const w = String(b.week || '');
  if (!/^\d{4}-\d{2}-\d{2}$/.test(w)) return [400, { error: 'bad_week' }];
  return [200, snapshotWeek(w, 'manual')];
}, true);
route('GET', '/api/admin/export', (q) => {
  const w = q.get('week');
  const list = runs.filter(r => !w || r.week === w);
  const head = 'runId,week,day,finishedAt,player,handle,mode,elapsed,wave,kills,bossKills,coins,serverElapsed,flags,hidden';
  const esc = v => { const s = v == null ? '' : String(v); return /[",\n]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s; };
  const lines = list.map(r => { const p = players[r.playerId] || {}; return [r.id, r.week, r.day, r.finishedAt, p.name, p.handle, r.mode, r.elapsed, r.wave, r.kills, r.bossKills, r.coins, r.serverElapsed, (r.flags || []).join('|'), moderation.hiddenRuns[r.id] ? 'yes' : ''].map(esc).join(','); });
  return [200, [head, ...lines].join('\n'), { 'Content-Type': 'text/csv; charset=utf-8', 'Content-Disposition': `attachment; filename="pimlr-runs${w ? '-' + w : ''}.csv"` }];
}, true);

const PAGES = { '/leaderboard': 'leaderboard.html', '/leaderboard/': 'leaderboard.html', '/rules': 'rules.html', '/privacy': 'privacy.html' };
const ADMIN_ROUTES = new Set(Object.keys(routes).filter(k => k.includes('/api/admin/')));

// ---------------------------------------------------------------- server
const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://x');
  cors(req, res);
  if (req.method === 'OPTIONS') return send(res, 204, '');
  try {
    if (req.method === 'GET' && PAGES[url.pathname]) return servePage(res, PAGES[url.pathname]);
    if (req.method === 'GET' && (url.pathname === '/admin' || url.pathname === '/admin/')) {
      if (!isAdmin(req)) return send(res, 401, 'admin login required', { 'WWW-Authenticate': 'Basic realm="PIMLR admin"' });
      return servePage(res, 'admin.html');
    }
    const key = req.method + ' ' + url.pathname;
    const fn = routes[key];
    if (!fn) return send(res, 404, { error: 'not_found' });
    if (url.pathname.startsWith('/api/admin/') && !isAdmin(req)) return send(res, 401, { error: 'admin' }, { 'WWW-Authenticate': 'Basic realm="PIMLR admin"' });
    if (limited('ip:' + clientIp(req), 300, 60e3)) return send(res, 429, { error: 'rate_limited' });
    const input = req.method === 'POST' ? await readBody(req) : url.searchParams;
    const [status, out, headers] = fn(input, req);
    send(res, status, out, headers);
  } catch (e) {
    if (e.message === 'too_large' || e.message === 'bad_json') return send(res, 400, { error: e.message });
    console.error(e);
    send(res, 500, { error: 'server' });
  }
});

lockFinishedWeeks();
server.listen(PORT, HOST, () => console.log(`PIMLR leaderboard listening on ${HOST}:${PORT} (data: ${DATA_DIR}, week ${weekKeyOf(Date.now())}, admin ${ADMIN_PASSWORD ? 'on' : 'OFF - set ADMIN_PASSWORD'})`));

module.exports = { weekKeyOf, dayKeyOf, server };
