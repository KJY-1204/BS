"""Common canvas + tags for one character.  usage: assemble_char.py spec.json"""
import sys, json, os, math
import numpy as np
from PIL import Image
spec = json.load(open(sys.argv[1]))
nf = spec['nf']; meta = json.load(open(f'{nf}/meta.json'))
used = {t['row'] for t in spec['tags']}
PX = max(meta[r]['pivot'][0] for r in used) + 8
PY = max(meta[r]['pivot'][1] for r in used) + 6
CW = PX + max(meta[r]['w'] - meta[r]['pivot'][0] for r in used) + 8
CH = PY + max(meta[r]["h"] - meta[r]["pivot"][1] for r in used) + 4
print('canvas', CW, CH)
YY, XX = np.mgrid[0:CH, 0:CW].astype(float)
def load(row, i, key):
    img = np.zeros((CH, CW, 4), 'uint8'); a = np.array(Image.open(f'{nf}/{row}_{i:02d}_{key}.png'))
    m = meta[row]; ox = PX - m['pivot'][0]; oy = PY - m['pivot'][1]
    img[oy:oy + a.shape[0], ox:ox + a.shape[1]] = a; return img
def empty(): return np.zeros((CH, CW, 4), 'uint8')
def blob(cx, cy, rx, ry):
    return np.exp(-((((XX - (PX + cx)) / rx) ** 2 + ((YY - (PY + cy)) / ry) ** 2) ** 2))
def warp(a, dx, dy):
    sx = np.rint(XX - dx).astype(int); sy = np.rint(YY - dy).astype(int)
    ok = (sx >= 0) & (sx < CW) & (sy >= 0) & (sy < CH); out = np.zeros_like(a); out[ok] = a[sy[ok], sx[ok]]; return out
def over(base, top):
    m = top[..., 3] > 0; o = base.copy(); o[m] = top[m]; return o

frames = []
for t in spec['tags']:
    row = t['row']
    idxs = t.get('frames', list(range(meta[row]['n'])))
    durs = t['dur'] if isinstance(t['dur'], list) else [t['dur']] * (len(idxs) if 'breath' not in t else t['breath']['n'])
    if 'breath' in t:
        b = t['breath']; body = load(row, 0, 'body'); fx = load(row, 0, 'fx')
        for k in range(b['n']):
            p = 2 * math.pi * k / b['n']
            dx = np.zeros((CH, CW)); dy = np.zeros((CH, CW))
            dy += round(1.0 * math.sin(p)) * np.clip((PY - YY) / 120.0, 0, 1)
            for (cx, cy, rx, ry, ax, ay, ph) in b['blobs']:
                w = blob(cx, cy, rx, ry); dx += round(ax * math.sin(p - ph)) * w; dy += round(ay * math.sin(p - ph)) * w
            frames.append(dict(tag=t['name'], dur=durs[k], body=warp(body, dx, dy), fxb=warp(fx, dx, dy)))
    else:
        for n, i in enumerate(idxs):
            frames.append(dict(tag=t['name'], dur=durs[n], body=load(row, i, 'body'), fxb=load(row, i, 'fx')))

out = spec['out']; os.makedirs('frames', exist_ok=True)
for fn in os.listdir('frames'): os.remove('frames/' + fn)
manifest = dict(w=CW, h=CH, pivot=[PX, PY], frames=[], tags=[]); cur = None
for idx, f in enumerate(frames, 1):
    d = {}
    for name, key in (('fx_back', 'fxb'), ('body', 'body'), ('fx_front', None)):
        p = f'frames/{idx:02d}_{name}.png'; Image.fromarray(f[key] if key else empty()).save(p); d[name] = os.path.abspath(p).replace('\\', '/')
    manifest['frames'].append(dict(dur=f['dur'], layers=d))
    if cur is None or cur['name'] != f['tag']: cur = dict(name=f['tag'], start=idx, end=idx); manifest['tags'].append(cur)
    else: cur['end'] = idx
json.dump(manifest, open('manifest.json', 'w'), indent=1)
def lua(v):
    if isinstance(v, dict): return "{" + ",".join(f'{"end_" if k == "end" else k}={lua(x)}' for k, x in v.items()) + "}"
    if isinstance(v, list): return "{" + ",".join(lua(x) for x in v) + "}"
    if isinstance(v, str): return json.dumps(v)
    return str(v)
open('manifest.lua', 'w').write("return " + lua(manifest))
cols = 10; rows = (len(frames) + cols - 1) // cols
sheet = Image.new('RGBA', (cols * CW, rows * CH), (110, 150, 110, 255))
for i, f in enumerate(frames):
    c = over(f['fxb'].copy(), f['body']); sheet.alpha_composite(Image.fromarray(c), ((i % cols) * CW, (i // cols) * CH))
sheet.save('sheet_char.png'); print(len(frames), 'frames', sheet.size)
