"""Put the walk / attack frames cut by sheet2frames.py on one canvas and add idle + skill.
Writes frames/NN_<layer>.png and manifest.json for build.lua"""
import numpy as np, math, json, os
from PIL import Image

wm = json.load(open('nf/walk_meta.json')); am = json.load(open('nf/atk_meta.json'))
PX = max(wm['pivot'][0], am['pivot'][0]) + 8
PY = max(wm['pivot'][1], am['pivot'][1]) + 6
CW = PX + max(wm['w'] - wm['pivot'][0], am['w'] - am['pivot'][0]) + 8
CH = PY + 10
print('canvas', CW, CH, 'pivot', PX, PY)
YY, XX = np.mgrid[0:CH, 0:CW].astype(float)

def load(prefix, meta, i, key):
    img = np.zeros((CH, CW, 4), 'uint8')
    a = np.array(Image.open(f'nf/{prefix}_{i:02d}_{key}.png'))
    ox = PX - meta['pivot'][0]; oy = PY - meta['pivot'][1]
    img[oy:oy + a.shape[0], ox:ox + a.shape[1]] = a
    return img
def empty(): return np.zeros((CH, CW, 4), 'uint8')
def over(base, top):
    m = top[..., 3] > 0; o = base.copy(); o[m] = top[m]; return o
def hexc(s): return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))
def put(a, x, y, col):
    x = int(round(x)); y = int(round(y))
    if 0 <= x < CW and 0 <= y < CH: a[y, x] = hexc(col) + (255,)
def blob(cx, cy, rx, ry):
    d2 = ((XX - (PX + cx)) / rx) ** 2 + ((YY - (PY + cy)) / ry) ** 2
    return np.exp(-d2 ** 2)
def warp(a, dx, dy):
    sx = np.rint(XX - dx).astype(int); sy = np.rint(YY - dy).astype(int)
    ok = (sx >= 0) & (sx < CW) & (sy >= 0) & (sy < CH)
    out = np.zeros_like(a); out[ok] = a[sy[ok], sx[ok]]; return out

RED = "E8283A"; PINK = "FF9AA6"; WHITE = "FFFFFF"
def ring(a, cx, cy, r, col, dither=False, thick=1):
    n = int(r * 8) + 8
    for k in range(n):
        ang = 2 * math.pi * k / n
        for t in range(thick):
            x = cx + (r + t) * math.cos(ang); y = cy + (r + t) * math.sin(ang) * 0.85
            if dither and (int(round(x)) + int(round(y))) % 2: continue
            put(a, x, y, col)
def star(a, x, y, s, col):
    put(a, x, y, col)
    for i in range(1, s + 1):
        for dx, dy in ((i, 0), (-i, 0), (0, i), (0, -i)): put(a, x + dx, y + dy, col if i < s else PINK)
def disc(a, cx, cy, r):
    for yy in range(-int(r) - 3, int(r) + 4):
        for xx in range(-int(r) - 3, int(r) + 4):
            d = math.hypot(xx, yy)
            if d <= r: put(a, cx + xx, cy + yy, WHITE)
            elif d <= r + 1.6 and (xx + yy) % 2 == 0: put(a, cx + xx, cy + yy, PINK)

frames = []
def add(tag, dur, body, fxb=None, fxf=None):
    frames.append(dict(tag=tag, dur=dur, body=body, fxb=fxb if fxb is not None else empty(), fxf=fxf if fxf is not None else empty()))

W_ = lambda i: (load('walk', wm, i, 'body'), load('walk', wm, i, 'fx'))
A_ = lambda i: (load('atk', am, i, 'body'), load('atk', am, i, 'fx'))

# ---- idle: the relaxed pose of the attack sheet, a small breath + hair / streamer sway
base, basefx = A_(7)
for t in range(6):
    p = 2 * math.pi * t / 6
    dx = np.zeros((CH, CW)); dy = np.zeros((CH, CW))
    up = np.clip((PY - YY) / 130.0, 0, 1)
    dy += round(1.0 * math.sin(p)) * up
    dx += 0.0 * up
    w = blob(-30, -95, 32, 18); dx += round(-1.5 * math.sin(p - 0.8)) * w; dy += round(2 * math.sin(p - 0.8)) * w
    w = blob(-48, -22, 22, 26); dx += round(1.6 * math.sin(p - 1.2)) * w
    w = blob(34, -70, 24, 26); dy += round(1.5 * math.sin(p - 1.0)) * w
    add('idle', 120, warp(base, dx, dy), None, None)

# ---- walk
for i in range(8): add('walk', 90, W_(i)[0])

# ---- attack
dur = [90, 90, 70, 60, 60, 80, 80, 100]
for i in range(8):
    b, f = A_(i); add('attack', dur[i], b, f)

# ---- skill: raise, aura and burst built from the attack poses
cx0, cy0 = PX, PY - 75
rng = np.random.RandomState(7)
spark = [(rng.uniform(-70, 70), rng.uniform(-80, 40), rng.randint(2, 4)) for _ in range(10)]
plan = [(1, 100, -1), (2, 110, 0), (6, 110, 1), (2, 110, 2), (6, 110, 3), (2, 100, 4), (5, 100, 5), (7, 100, -1)]
for n, (src, d, k) in enumerate(plan):
    b, f = A_(src)
    fxb = empty(); fxf = empty()
    if k >= 0:
        for j, r in enumerate((16 + k * 11, 30 + k * 11)):
            if r < 78: ring(fxb, cx0, cy0, r, RED if j == 0 else PINK, dither=(j == 1), thick=2 if j == 0 else 1)
        if k >= 1: ring(fxb, cx0, cy0, 10 + k * 6, WHITE, dither=True)
        for (sx, sy, sz) in spark[:2 + k * 2]:
            star(fxf, cx0 + sx, cy0 + sy, sz - 1 if (k + int(sx)) % 2 else sz, WHITE)
        if k >= 2 and src in (2, 6):
            ys, xs = np.where(b[..., 3] > 0)
            top = ys.min(); tx = int(xs[ys == top].mean())
            disc(fxf, tx, top + 4, 4 + (k - 2) * 2)
    add('skill', d, b, fxb if k >= 0 else f, fxf if k >= 0 else None)

# ---- export
os.makedirs('frames', exist_ok=True)
for fn in os.listdir('frames'): os.remove('frames/' + fn)
manifest = dict(w=CW, h=CH, pivot=[PX, PY], frames=[], tags=[])
cur = None
for idx, f in enumerate(frames, 1):
    d = {}
    for name, key in (('fx_back', 'fxb'), ('body', 'body'), ('fx_front', 'fxf')):
        p = f'frames/{idx:02d}_{name}.png'; Image.fromarray(f[key]).save(p); d[name] = os.path.abspath(p).replace('\\', '/')
    manifest['frames'].append(dict(dur=f['dur'], layers=d))
    if cur is None or cur['name'] != f['tag']:
        cur = dict(name=f['tag'], start=idx, end=idx); manifest['tags'].append(cur)
    else: cur['end'] = idx
json.dump(manifest, open('manifest.json', 'w'), indent=1)
cols = 8; rows = (len(frames) + cols - 1) // cols
sheet = Image.new('RGBA', (cols * CW, rows * CH), (110, 150, 110, 255))
for i, f in enumerate(frames):
    c = over(over(f['fxb'].copy(), f['body']), f['fxf'])
    sheet.alpha_composite(Image.fromarray(c), ((i % cols) * CW, (i // cols) * CH))
sheet.save('sheet2.png'); print(len(frames), 'frames', sheet.size)
