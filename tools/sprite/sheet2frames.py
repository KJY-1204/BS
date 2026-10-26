"""Cut an AI-made sprite sheet (transparent bg, 8 frames in a row) into pixel-art frames.
usage: sheet2frames.py <sheet.png> <out_prefix> <scale>"""
import sys, json
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

sheet, prefix, S = sys.argv[1], sys.argv[2], float(sys.argv[3])
NOFX = len(sys.argv) > 4 and sys.argv[4] == 'nofx'
NF = 8
OUT = (52, 26, 34)
a = np.array(Image.open(sheet).convert('RGBA'))
H, W = a.shape[:2]
al = a[..., 3]
occ = (al >= 200).sum(0)
cw = W / NF
cuts = [0]
for k in range(1, NF):
    e = int(k * cw)
    seg = occ[e - 35:e + 35]
    cuts.append(e - 35 + int(np.argmin(seg)))
cuts.append(W)

def shrink(cell):
    """area-average downscale of premultiplied RGBA"""
    h, w = cell.shape[:2]
    nh, nw = max(1, round(h * S)), max(1, round(w * S))
    f = cell.astype(float)
    pm = f[..., :3] * (f[..., 3:4] / 255.0)
    im = Image.fromarray(np.dstack([pm, f[..., 3:4]]).astype('uint8').clip(0, 255), 'RGBA') if False else None
    ch = []
    for c in range(4):
        src = pm[..., c] if c < 3 else f[..., 3]
        ch.append(np.array(Image.fromarray(src.astype('float32'), 'F').resize((nw, nh), Image.BOX)))
    pmr = np.dstack(ch[:3]); alr = ch[3]
    rgb = pmr / np.maximum(alr[..., None] / 255.0, 1e-3)
    return np.clip(rgb, 0, 255), alr

# ---- assign every pixel to a frame (components, not fixed cut lines)
mask = al >= 40
lab, k = ndi.label(ndi.binary_dilation(mask, iterations=2), structure=np.ones((3, 3)))
lab = np.where(mask, lab, 0)
solid_all = al >= 200
def cell_of(x): return int(np.clip(np.searchsorted(cuts, x, side='right') - 1, 0, NF - 1))
F = np.full((H, W), -1, int)
for c in range(1, k + 1):
    cm = lab == c
    n = cm.sum()
    if n < 200: continue
    ys, xs = np.where(cm)
    if xs.max() - xs.min() <= 1.3 * cw:
        sy, sx = np.where(cm & solid_all)
        F[cm] = cell_of(sx.mean())
    else:
        sl, sk = ndi.label(cm & solid_all, structure=np.ones((3, 3)))
        sub = np.zeros((H, W), int) - 1
        for j in range(1, sk + 1):
            jm = sl == j
            if jm.sum() < 60: continue
            sy, sx = np.where(jm); sub[jm] = cell_of(sx.mean())
        known = sub >= 0
        _, (iy, ix) = ndi.distance_transform_edt(~known, return_indices=True)
        F[cm] = sub[iy, ix][cm]
known = F >= 0
_, (iy, ix) = ndi.distance_transform_edt(~known, return_indices=True)
F = np.where(mask, F[iy, ix], -1)

frames = []
for i in range(NF):
    full = a.copy(); full[F != i] = 0
    solid = full[..., 3] >= 200
    allm = full[..., 3] >= 40
    ys, xs = np.where(allm)
    m = 6
    box = (max(0, xs.min() - m), max(0, ys.min() - m), min(W, xs.max() + m + 1), min(H, ys.max() + m + 1))
    cell = full[box[1]:box[3], box[0]:box[2]]
    sm = cell[..., 3] >= 200
    lum = cell[..., :3].astype(int).sum(2)
    dark = sm & (lum < 200)
    dy_, dx_ = np.where(dark)
    ymax = dy_.max()
    low = dy_ >= ymax - 14
    fx = dx_[low].mean(); fy = ymax
    rgb, alr = shrink(cell)
    frames.append(dict(rgb=rgb, al=alr, foot=(fx * S, fy * S)))

# shared palette over every frame
pix = np.concatenate([f['rgb'][f['al'] >= 160].reshape(-1, 3) for f in frames]).astype('uint8')
ref = np.array(Image.open('gpt_n196.png').convert('RGBA'))
pix = np.concatenate([pix, ref[ref[..., 3] > 0][:, :3]])
NC = 56
pm = Image.fromarray(pix.reshape(-1, 1, 3)).quantize(NC, method=Image.MEDIANCUT, dither=Image.NONE)
pal = np.array(pm.getpalette()[:NC * 3]).reshape(-1, 3)

def nearest(arr, pal):
    d = ((arr.reshape(-1, 1, 3).astype(int) - pal[None].astype(int)) ** 2).sum(-1)
    return d.argmin(1), d.min(1)

allp = np.concatenate([f['rgb'][f['al'] >= 160].reshape(-1, 3) for f in frames])
_, err = nearest(allp, pal)
bad = allp[err > 1800]
if len(bad) > 30:
    k = Image.fromarray(bad.astype('uint8').reshape(-1, 1, 3)).quantize(10, method=Image.MEDIANCUT, dither=Image.NONE)
    pal = np.vstack([pal, np.array(k.getpalette()[:30]).reshape(-1, 3)])

out_frames = []
for f in frames:
    rgb, alr = f['rgb'], f['al']
    h, w = alr.shape
    solid = alr >= 150
    solid = ndi.binary_opening(solid, structure=np.ones((2, 2))) | (alr >= 235)
    lab, n = ndi.label(solid, structure=np.ones((3, 3)))
    if n:
        sizes = ndi.sum(solid, lab, range(1, n + 1))
        solid = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s >= 12])
    glow = (alr >= 40) & ~solid
    idx, _ = nearest(rgb, pal)
    q = pal[idx].reshape(h, w, 3).astype('uint8')
    body = np.zeros((h, w, 4), 'uint8')
    body[..., :3] = q; body[..., 3] = np.where(solid, 255, 0)
    ol = ndi.binary_dilation(solid, structure=ndi.generate_binary_structure(2, 1)) & ~solid
    body[ol] = OUT + (255,)
    fxl = np.zeros((h, w, 4), 'uint8')
    yy, xx = np.mgrid[0:h, 0:w]
    gl = glow & ((alr >= 110) | ((xx + yy) % 2 == 0))
    gl &= ~ol & ~solid
    if NOFX: gl[:] = False
    else:
        gl2, gn = ndi.label(gl, structure=np.ones((3, 3)))
        if gn:
            gs = ndi.sum(gl, gl2, range(1, gn + 1)); gl = np.isin(gl2, [j + 1 for j, v in enumerate(gs) if v >= 6])
    fxl[..., :3] = q; fxl[gl, 3] = 255
    out_frames.append(dict(body=body, fx=fxl, foot=f['foot']))

# one common canvas, feet pinned to the same spot
L = max(o['foot'][0] for o in out_frames); R = max(o['body'].shape[1] - o['foot'][0] for o in out_frames)
T = max(o['foot'][1] for o in out_frames); B = max(o['body'].shape[0] - o['foot'][1] for o in out_frames)
CWd = int(L + R) + 6; CHt = int(T + B) + 6
px, py = int(L) + 3, int(T) + 3
meta = dict(w=CWd, h=CHt, pivot=[px, py], n=len(out_frames))
for i, o in enumerate(out_frames):
    for key in ('body', 'fx'):
        img = np.zeros((CHt, CWd, 4), 'uint8')
        arr = o[key]
        ox = px - int(round(o['foot'][0])); oy = py - int(round(o['foot'][1]))
        img[oy:oy + arr.shape[0], ox:ox + arr.shape[1]] = arr
        Image.fromarray(img).save(f'{prefix}_{i:02d}_{key}.png')
json.dump(meta, open(f'{prefix}_meta.json', 'w'))
sh = Image.new('RGBA', (CWd * NF, CHt), (110, 150, 110, 255))
for i in range(NF):
    c = Image.open(f'{prefix}_{i:02d}_fx.png'); c.alpha_composite(Image.open(f'{prefix}_{i:02d}_body.png'))
    sh.alpha_composite(c, (i * CWd, 0))
sh.save(f'{prefix}_sheet.png'); print(meta)
