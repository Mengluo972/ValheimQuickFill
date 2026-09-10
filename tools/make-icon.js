// 生成 Thunderstore 要求的 256x256 图标（无第三方依赖，直接用 zlib 编码 PNG）。
// 图案：深色背景 + 石制炉体 + 橙色炉火 + 金色向上箭头（一键装满的意象）。
const zlib = require("zlib");
const fs = require("fs");

const W = 256, H = 256;
const px = Buffer.alloc(W * H * 4);

function set(x, y, r, g, b, a = 255) {
  if (x < 0 || y < 0 || x >= W || y >= H) return;
  const i = (y * W + x) * 4;
  // 简单 alpha 混合到已有像素上
  const na = a / 255;
  px[i] = Math.round(px[i] * (1 - na) + r * na);
  px[i + 1] = Math.round(px[i + 1] * (1 - na) + g * na);
  px[i + 2] = Math.round(px[i + 2] * (1 - na) + b * na);
  px[i + 3] = 255;
}

function rect(x0, y0, x1, y1, c) {
  for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) set(x, y, c[0], c[1], c[2], c[3] ?? 255);
}

function disc(cx, cy, r, c) {
  for (let y = -r; y <= r; y++) for (let x = -r; x <= r; x++) {
    if (x * x + y * y <= r * r) set(cx + x, cy + y, c[0], c[1], c[2], c[3] ?? 255);
  }
}

function tri(ax, ay, bx, by, cx, cy, col) {
  const minX = Math.floor(Math.min(ax, bx, cx)), maxX = Math.ceil(Math.max(ax, bx, cx));
  const minY = Math.floor(Math.min(ay, by, cy)), maxY = Math.ceil(Math.max(ay, by, cy));
  const sign = (x1, y1, x2, y2, x3, y3) => (x1 - x3) * (y2 - y3) - (x2 - x3) * (y1 - y3);
  for (let y = minY; y <= maxY; y++) for (let x = minX; x <= maxX; x++) {
    const d1 = sign(x, y, ax, ay, bx, by), d2 = sign(x, y, bx, by, cx, cy), d3 = sign(x, y, cx, cy, ax, ay);
    const hasNeg = d1 < 0 || d2 < 0 || d3 < 0, hasPos = d1 > 0 || d2 > 0 || d3 > 0;
    if (!(hasNeg && hasPos)) set(x, y, col[0], col[1], col[2], col[3] ?? 255);
  }
}

// 背景：垂直渐变深棕
for (let y = 0; y < H; y++) {
  const t = y / (H - 1);
  const r = Math.round(38 + (18 - 38) * t), g = Math.round(29 + (14 - 29) * t), b = Math.round(22 + (12 - 22) * t);
  row(y, r, g, b);
}
function row(y, r, g, b) { for (let x = 0; x < W; x++) set(x, y, r, g, b); }

// 炉体外发光
for (let y = 130; y <= 232; y++) for (let x = 52; x <= 204; x++) set(x, y, 90, 60, 20, 40);
// 石制炉体
rect(60, 128, 196, 228, [122, 112, 96]);
rect(60, 128, 196, 132, [150, 140, 122]); // 顶沿高光
rect(52, 228, 204, 236, [70, 62, 52]);     // 底座
// 炉膛
rect(96, 150, 160, 210, [40, 26, 16]);
rect(102, 156, 154, 204, [255, 120, 20]);
rect(102, 186, 154, 204, [255, 190, 70]);
// 火焰
tri(128, 150, 112, 196, 144, 196, [255, 214, 110]);
tri(128, 166, 118, 196, 138, 196, [255, 240, 170]);
// 金色向上箭头
rect(121, 52, 135, 112, [255, 210, 74]);
tri(128, 22, 98, 62, 158, 62, [255, 210, 74]);
// 箭头高光
rect(121, 52, 126, 112, [255, 235, 150]);
tri(128, 30, 112, 58, 128, 58, [255, 235, 150]);
// 圆角遮罩（四角削成背景色，做出圆角矩形观感）
const R = 34;
for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
  const cx = Math.min(Math.max(x, R), W - 1 - R), cy = Math.min(Math.max(y, R), H - 1 - R);
  const dx = x - cx, dy = y - cy;
  if (dx * dx + dy * dy > R * R) { const i = (y * W + x) * 4; px[i] = 0; px[i + 1] = 0; px[i + 2] = 0; px[i + 3] = 0; }
}

// ---- PNG 编码 ----
function crc32(buf) {
  let c, table = crc32.t;
  if (!table) { table = crc32.t = []; for (let n = 0; n < 256; n++) { c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; table[n] = c >>> 0; } }
  let crc = 0xffffffff;
  for (let i = 0; i < buf.length; i++) crc = table[(crc ^ buf[i]) & 0xff] ^ (crc >>> 8);
  return (crc ^ 0xffffffff) >>> 0;
}
function chunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length, 0);
  const td = Buffer.concat([Buffer.from(type, "ascii"), data]);
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(td), 0);
  return Buffer.concat([len, td, crc]);
}
const ihdr = Buffer.alloc(13);
ihdr.writeUInt32BE(W, 0); ihdr.writeUInt32BE(H, 4); ihdr[8] = 8; ihdr[9] = 6; // 8bit RGBA
const raw = Buffer.alloc((W * 4 + 1) * H);
for (let y = 0; y < H; y++) { raw[y * (W * 4 + 1)] = 0; px.copy(raw, y * (W * 4 + 1) + 1, y * W * 4, (y + 1) * W * 4); }
const png = Buffer.concat([
  Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
  chunk("IHDR", ihdr), chunk("IDAT", zlib.deflateSync(raw, { level: 9 })), chunk("IEND", Buffer.alloc(0)),
]);
fs.writeFileSync(process.argv[2] || "icon.png", png);
console.log("wrote", process.argv[2] || "icon.png", png.length, "bytes");
