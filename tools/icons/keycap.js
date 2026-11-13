// The keycuts keycap, drawn as real geometry: a rounded rectangle base, a smaller rounded top, side facets lit
// by one light, and the legend drawn on the top plane through its exact affine projection
// (orthographic view, so a plane maps affinely). Used by app-icon.html to draw the app icons, and by the
// mockups in docs/mockups (logo-concepts-v*.html), where it was designed.
const rad = d => d * Math.PI / 180;
const hex = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));
const rgb = c => `rgb(${c.map(v => Math.round(Math.max(0, Math.min(255, v)))).join(',')})`;
const scale = (c, s) => c.map(v => v * s);
const mix = (a, b, t) => a.map((v, i) => v + (b[i] - v) * t);
let uid = 0;

function roundedRect(w, d, r, n, oy) {
  const pts = [];
  const corners = [[w/2 - r, d/2 - r, 0], [-w/2 + r, d/2 - r, 90], [-w/2 + r, -d/2 + r, 180], [w/2 - r, -d/2 + r, 270]];
  for (const [cx, cy, start] of corners)
    for (let i = 0; i <= n; i++) {
      const a = rad(start + 90 * i / n);
      pts.push([cx + r * Math.cos(a), cy + oy + r * Math.sin(a)]);
    }
  return pts;
}

function renderKey(o) {
  const id = 'k' + (uid++);
  const az = rad(o.azimuth), el = rad(o.elevation);
  const H = o.height ?? 8.5, topW = o.topW ?? 12.6, topD = o.topD ?? 13.2, yo = o.topShift ?? 0.9;
  const rot = ([x, y, z]) => [x * Math.cos(az) - y * Math.sin(az), x * Math.sin(az) + y * Math.cos(az), z];
  const proj = p => { const [x, y, z] = rot(p); return [x, -(y * Math.sin(el) + z * Math.cos(el))]; };
  const V = [0, -Math.cos(el), Math.sin(el)];
  const Lr = (() => { const l = [-0.5, -0.45, 0.75]; const m = Math.hypot(...l); return l.map(v => v / m); })();
  const dot = (a, b) => a[0]*b[0] + a[1]*b[1] + a[2]*b[2];
  const sub = (a, b) => [a[0]-b[0], a[1]-b[1], a[2]-b[2]];
  const cross = (a, b) => [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]];
  const norm = a => { const m = Math.hypot(...a); return a.map(v => v / m); };

  const N = 22;
  const base = roundedRect(18, 18, o.baseR ?? 2.2, N, 0).map(([x, y]) => [x, y, 0]);
  const top = roundedRect(topW, topD, o.topR ?? 2.4, N, yo).map(([x, y]) => [x, y, H]);
  const side = hex(o.side), topC = hex(o.top);
  let defs = '', quads = '';

  for (let i = 0; i < base.length; i++) {
    const j = (i + 1) % base.length;
    const b0 = rot(base[i]), b1 = rot(base[j]), t0 = rot(top[i]), t1 = rot(top[j]);
    const n = norm(cross(sub(b1, b0), sub(t0, b0)));
    if (dot(n, V) <= 0.0001) continue;
    let lit = (o.ambient ?? 0.62) + (o.diffuse ?? 0.48) * Math.max(0, dot(n, Lr));
    if (o.flat) lit = dot(n, Lr) > 0.12 ? 1.0 : 0.8;
    const cTop = scale(side, lit), cBot = scale(side, lit * (o.flat ? 1 : 0.8));
    const P = [base[i], base[j], top[j], top[i]].map(proj);
    const pts = P.map(p => p.map(v => v.toFixed(3)).join(',')).join(' ');
    if (o.flat) {
      quads += `<polygon points="${pts}" fill="${rgb(cTop)}" stroke="${rgb(cTop)}" stroke-width="0.14"/>`;
    } else {
      const g = `${id}q${i}`;
      const mt = [(P[2][0] + P[3][0]) / 2, (P[2][1] + P[3][1]) / 2], mb = [(P[0][0] + P[1][0]) / 2, (P[0][1] + P[1][1]) / 2];
      defs += `<linearGradient id="${g}" gradientUnits="userSpaceOnUse" x1="${mt[0]}" y1="${mt[1]}" x2="${mb[0]}" y2="${mb[1]}"><stop offset="0" stop-color="${rgb(cTop)}"/><stop offset="1" stop-color="${rgb(cBot)}"/></linearGradient>`;
      // The stroke overlaps the next facet, so antialiasing leaves no light seam between them
      quads += `<polygon points="${pts}" fill="url(#${g})" stroke="url(#${g})" stroke-width="0.14"/>`;
    }
  }

  // Affine map from the top plane (x right, y down toward the viewer) to the screen.
  const tp = (x, y) => proj([x, yo - y, H]);
  const p0 = tp(0, 0), px = tp(1, 0), py = tp(0, 1);
  const M = `matrix(${px[0]-p0[0]} ${px[1]-p0[1]} ${py[0]-p0[0]} ${py[1]-p0[1]} ${p0[0]} ${p0[1]})`;

  // Shadow on the table, pushed away from the light.
  const sh = base.map(p => { const r = rot(p); return [r[0] + 1.6, r[1] + 1.9, 0]; })
                 .map(([x, y, z]) => [x, -(y * Math.sin(el) + z * Math.cos(el))]);
  const contact = base.map(proj);
  const poly = P => P.map(p => p.map(v => v.toFixed(3)).join(',')).join(' ');

  const tight = o.tight;
  const all = tight ? [...contact, ...top.map(proj)] : [...sh, ...contact, ...top.map(proj)];
  const pad = tight ? 0.5 : 2.6;
  let minX = Math.min(...all.map(p => p[0])) - pad, maxX = Math.max(...all.map(p => p[0])) + pad;
  let minY = Math.min(...all.map(p => p[1])) - pad, maxY = Math.max(...all.map(p => p[1])) + pad;
  // tight (no shadow) and square (with it) center the key in a square box, which an icon needs
  if (tight || o.square) { const w = maxX - minX, h = maxY - minY, m = Math.max(w, h); minX -= (m - w) / 2; maxX = minX + m; minY -= (m - h) / 2; maxY = minY + m; }

  const tw = topW, td = topD;
  const dishHi = rgb(mix(topC, [255,255,255], o.dishLight ?? 0.10)), dishLo = rgb(scale(topC, o.dishDark ?? 0.93));
  let topFace;
  if (o.flat) {
    topFace = `<rect x="${-tw/2}" y="${-td/2}" width="${tw}" height="${td}" rx="${o.topR ?? 2.4}" fill="${o.top}"/>`;
  } else {
    defs += `<linearGradient id="${id}t" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${dishHi}"/><stop offset="1" stop-color="${dishLo}"/></linearGradient>
      <radialGradient id="${id}d" cx="0.5" cy="0.52" r="0.55"><stop offset="0" stop-color="#000" stop-opacity="${o.dishShade ?? 0.07}"/><stop offset="0.75" stop-color="#000" stop-opacity="0"/></radialGradient>`;
    topFace = `<rect x="${-tw/2}" y="${-td/2}" width="${tw}" height="${td}" rx="${o.topR ?? 2.4}" fill="url(#${id}t)"/>
      <rect x="${-tw/2}" y="${-td/2}" width="${tw}" height="${td}" rx="${o.topR ?? 2.4}" fill="url(#${id}d)"/>
      <rect x="${-tw/2 + 0.2}" y="${-td/2 + 0.2}" width="${tw - 0.4}" height="${td - 0.4}" rx="${(o.topR ?? 2.4) - 0.2}" fill="none" stroke="#fff" stroke-opacity="${o.rim ?? 0.45}" stroke-width="0.35"/>`;
  }

  const shadow = o.flat || o.noShadow || o.tight ? '' :
    `<filter id="${id}b1" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="1.3"/></filter>
     <filter id="${id}b2" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="0.35"/></filter>
     <polygon points="${poly(sh)}" fill="#0b1220" opacity="${o.shadowOpacity ?? 0.22}" filter="url(#${id}b1)"/>
     <polygon points="${poly(contact)}" fill="#0b1220" opacity="0.35" filter="url(#${id}b2)"/>`;

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${minX} ${minY} ${maxX - minX} ${maxY - minY}">
    <defs>${defs}</defs>${shadow}${quads}<g transform="${M}">${topFace}${o.legend(tw, td)}</g></svg>`;
}

// v5: center the K by its ink (the glyph's measured bounds), not by the font's em box,
// so it sits in the middle of the top face both ways.
const inkBox = (() => {
  const ctx = document.createElement('canvas').getContext('2d');
  return weight => {
    ctx.font = `${weight} 100px "Segoe UI"`;
    const m = ctx.measureText('K');
    return { l: m.actualBoundingBoxLeft, r: m.actualBoundingBoxRight, a: m.actualBoundingBoxAscent, d: m.actualBoundingBoxDescent };
  };
})();
const K = (color, size = 8.2, weight = 600, x = 0, y = 0) => (tw, td) => {
  const b = inkBox(weight), s = size / 100;
  const tx = x + (b.l - b.r) / 2 * s, ty = y + (b.a - b.d) / 2 * s;
  return `<text x="${tx}" y="${ty}" font-family="Segoe UI" font-weight="${weight}" font-size="${size}" fill="${color}">K</text>`;
};

