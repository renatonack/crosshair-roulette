'use strict';
// Paints the crosshair icon pixel by pixel (no image libraries) and writes
// nativo/assets/icon.png plus a multi-size nativo/assets/icon.ico.

const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');

const OUT = path.join(__dirname, '..', 'nativo', 'assets');

const CRC = new Int32Array(256).map((_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c;
});
function crc32(buf) {
  let c = -1;
  for (const b of buf) c = CRC[(c ^ b) & 0xff] ^ (c >>> 8);
  return (c ^ -1) >>> 0;
}
function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length);
  const td = Buffer.concat([Buffer.from(type), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(td));
  return Buffer.concat([len, td, crc]);
}
function png(size, rgba) {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8;
  ihdr[9] = 6;
  const raw = Buffer.alloc((size * 4 + 1) * size);
  for (let y = 0; y < size; y++) rgba.copy(raw, y * (size * 4 + 1) + 1, y * size * 4, (y + 1) * size * 4);
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}

const hex = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16), 255];
const BG = hex('#16181e');
const RIM = hex('#2b2f39');
const LIME = hex('#b6ff3b');
const BLACK = hex('#000000');
const CLEAR = [0, 0, 0, 0];

// colour at point (x, y) in a 256-unit design space centred on 0,0
function sample(x, y) {
  const r = Math.hypot(x, y);
  if (r > 124) return CLEAR;
  if (r > 115) return RIM;
  const arm = (u, v, pad) => Math.abs(u) <= 9 + pad && Math.abs(v) >= 20 - pad && Math.abs(v) <= 82 + pad;
  if (arm(x, y, 0) || arm(y, x, 0)) return LIME;
  if (arm(x, y, 6) || arm(y, x, 6)) return BLACK;
  return BG;
}

function render(size) {
  const buf = Buffer.alloc(size * size * 4);
  const ss = 4;
  for (let py = 0; py < size; py++) {
    for (let px = 0; px < size; px++) {
      let r = 0;
      let g = 0;
      let b = 0;
      let al = 0;
      for (let sy = 0; sy < ss; sy++) {
        for (let sx = 0; sx < ss; sx++) {
          const x = ((px + (sx + 0.5) / ss) / size) * 256 - 128;
          const y = ((py + (sy + 0.5) / ss) / size) * 256 - 128;
          const c = sample(x, y);
          r += c[0] * c[3];
          g += c[1] * c[3];
          b += c[2] * c[3];
          al += c[3];
        }
      }
      const i = (py * size + px) * 4;
      buf[i] = al ? r / al : 0;
      buf[i + 1] = al ? g / al : 0;
      buf[i + 2] = al ? b / al : 0;
      buf[i + 3] = al / (ss * ss);
    }
  }
  return png(size, buf);
}

fs.mkdirSync(OUT, { recursive: true });
const sizes = [16, 24, 32, 48, 64, 128, 256];
const pngs = sizes.map(render);
fs.writeFileSync(path.join(OUT, 'icon.png'), pngs[sizes.indexOf(256)]);

const header = Buffer.alloc(6);
header.writeUInt16LE(1, 2);
header.writeUInt16LE(sizes.length, 4);
let offset = 6 + 16 * sizes.length;
const dir = sizes.map((s, i) => {
  const e = Buffer.alloc(16);
  e[0] = s === 256 ? 0 : s;
  e[1] = s === 256 ? 0 : s;
  e.writeUInt16LE(1, 4);
  e.writeUInt16LE(32, 6);
  e.writeUInt32LE(pngs[i].length, 8);
  e.writeUInt32LE(offset, 12);
  offset += pngs[i].length;
  return e;
});
fs.writeFileSync(path.join(OUT, 'icon.ico'), Buffer.concat([header, ...dir, ...pngs]));
console.log('icon.png + icon.ico written to', OUT);
