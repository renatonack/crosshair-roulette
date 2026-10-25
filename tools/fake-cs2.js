'use strict';
// Pretends to be CS2: POSTs Game State Integration payloads to Crosshair Roulette so
// you can test without launching the game.
//
//   node tools/fake-cs2.js            interactive: k = kill, h = headshot, d = double,
//                                     a = ace, n = knife kill, s = shoot, x = die, q = quit
//   node tools/fake-cs2.js kill hs    one-shot: send these events and exit
//
// Reads the port and token from the app's settings file.

const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');

function settingsFile() {
  const base = process.env.APPDATA || path.join(os.homedir(), 'AppData', 'Roaming');
  for (const name of ['crosshair-roulette', 'Crosshair Roulette']) {
    const f = path.join(base, name, 'settings.json');
    if (fs.existsSync(f)) return f;
  }
  return null;
}

const file = settingsFile();
const cfg = file ? JSON.parse(fs.readFileSync(file, 'utf8')) : {};
const PORT = cfg.gsiPort || 3748;
const TOKEN = cfg.gsiToken || '';
const ME = '76561198000000001';

const st = { kills: 0, rk: 0, rhs: 0, health: 100, clip: 30, weapon: 'weapon_ak47', type: 'Rifle', round: 1 };

function payload() {
  return {
    provider: { name: 'Counter-Strike: Global Offensive', appid: 730, version: 14000, steamid: ME, timestamp: Math.floor(Date.now() / 1000) },
    map: { mode: 'competitive', name: 'de_nuke', phase: 'live', round: st.round },
    round: { phase: 'live' },
    player: {
      steamid: ME,
      name: 'You',
      team: 'CT',
      activity: 'playing',
      state: { health: st.health, armor: 100, helmet: true, flashed: 0, smoked: 0, burning: 0, money: 4000, round_kills: st.rk, round_killhs: st.rhs },
      weapons: {
        weapon_0: { name: 'weapon_knife', type: 'Knife', state: st.type === 'Knife' ? 'active' : 'holstered' },
        weapon_1: { name: st.weapon === 'weapon_knife' ? 'weapon_ak47' : st.weapon, type: 'Rifle', ammo_clip: st.clip, ammo_clip_max: 30, state: st.type === 'Knife' ? 'holstered' : 'active' },
      },
      match_stats: { kills: st.kills, assists: 0, deaths: 0, mvps: 0, score: st.kills * 2 },
    },
    auth: { token: TOKEN },
  };
}

async function send() {
  try {
    await fetch(`http://127.0.0.1:${PORT}/gsi`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload()) });
  } catch (err) {
    console.error(`App not reachable on port ${PORT}: ${err.cause ? err.cause.code : err.message}`);
  }
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const ACTIONS = {
  async kill() {
    st.clip = Math.max(0, st.clip - 3);
    await send();
    st.kills++;
    st.rk++;
    await send();
  },
  async hs() {
    st.clip = Math.max(0, st.clip - 1);
    await send();
    st.kills++;
    st.rk++;
    st.rhs++;
    await send();
  },
  async double() {
    st.kills += 2;
    st.rk += 2;
    st.rhs += 1;
    await send();
  },
  async ace() {
    while (st.rk < 5) {
      st.kills++;
      st.rk++;
      await send();
      await sleep(400);
    }
  },
  async knife() {
    st.type = 'Knife';
    st.weapon = 'weapon_knife';
    await send();
    st.kills++;
    st.rk++;
    await send();
    st.type = 'Rifle';
    st.weapon = 'weapon_ak47';
  },
  async shoot() {
    st.clip = Math.max(0, st.clip - 1);
    if (st.clip === 0) st.clip = 30;
    await send();
  },
  async die() {
    st.health = 0;
    await send();
    st.round++;
    st.rk = 0;
    st.rhs = 0;
    st.health = 100;
    await sleep(300);
    await send();
  },
};

const KEYS = { k: 'kill', h: 'hs', d: 'double', a: 'ace', n: 'knife', s: 'shoot', x: 'die' };
const ALIASES = { headshot: 'hs', dbl: 'double' };

(async () => {
  console.log(`fake CS2 → 127.0.0.1:${PORT} ${file ? '(token from ' + file + ')' : '(no settings found, sending without token)'}`);
  await send(); // first payload primes the app's baseline
  await sleep(150);
  const args = process.argv.slice(2);
  if (args.length) {
    for (const a of args) {
      const name = ALIASES[a] || a;
      if (!ACTIONS[name]) {
        console.error('unknown action', a);
        continue;
      }
      await ACTIONS[name]();
      console.log('sent', name);
      await sleep(250);
    }
    return;
  }
  console.log('k kill · h headshot · d double kill · a ace · n knife kill · s shoot · x die · q quit');
  setInterval(send, 8000); // heartbeat
  process.stdin.setRawMode && process.stdin.setRawMode(true);
  process.stdin.resume();
  process.stdin.on('data', async (b) => {
    const ch = b.toString().toLowerCase();
    if (ch === 'q' || ch === '\u0003') process.exit(0);
    const name = KEYS[ch];
    if (name) {
      await ACTIONS[name]();
      console.log('sent', name, `(kills ${st.kills}, round kills ${st.rk})`);
    }
  });
})();
