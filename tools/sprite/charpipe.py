"""Turn rows of an AI sprite sheet (transparent bg) into pixel-art frames sharing one palette.
usage: charpipe.py config.json   (see marisa.json)"""
import sys, json, os
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

cfg = json.load(open(sys.argv[1]))
OUT = tuple(cfg.get('outline', (52, 26, 34)))
outdir = cfg['out']; os.makedirs(outdir, exist_ok=True)

def shrink(cell, S):
    h, w = cell.shape[:2]
    if abs(S - 1) < 1e-6:
        f = cell.astype(float); a = f[..., 3]
        return f[..., :3], a
    nh, nw = max(1, round(h * S)), max(1, round(w * S))
    f = cell.astype(float)
    pm = f[..., :3] * (f[..., 3:4] / 255.0)
    ch = [np.array(Image.fromarray((pm[..., c] if c < 3 else f[..., 3]).astype('float32'), 'F').resize((nw, nh), Image.BOX)) for c in range(4)]
    pmr = np.dstack(ch[:3]); alr = ch[3]
    return np.clip(pmr / np.maximum(alr[..., None] / 255.0, 1e-3), 0, 255), alr

def cut_row(row):
    a = np.array(Image.open(row['sheet']).convert('RGBA'))
    x0, y0, x1, y1 = row['box']
    a = a[y0:y1, x0:x1]
    H, W = a.shape[:2]; n = len(row['cuts']) - 1 if 'cuts' in row else row['n']
    al = a[..., 3]
    occ = (al >= 200).sum(0); cw = W / n
    cuts = [0]
    for k in range(1, n):
        e = int(k * cw); lo = max(1, e - int(cw * 0.2)); hi = min(W - 1, e + int(cw * 0.2))
        cuts.append(lo + int(np.argmin(occ[lo:hi])))
    cuts.append(W)
    GM = row.get('glowmin', 40)
    mask = al >= GM
    lab, k = ndi.label(ndi.binary_dilation(mask, iterations=2), structure=np.ones((3, 3)))
    lab = np.where(mask, lab, 0)
    if 'cuts' in row:
        cuts = [c - x0 for c in row['cuts']]; cuts[0] = 0; cuts[-1] = W
        F0 = np.searchsorted(cuts, np.arange(W), side='right') - 1
        k = 0
    solid_all = al >= 200
    cell_of = lambda x: int(np.clip(np.searchsorted(cuts, x, side='right') - 1, 0, n - 1))
    F = np.full((H, W), -1, int)
    for c in range(1, k + 1):
        cm = lab == c
        if cm.sum() < 150: continue
        ys, xs = np.where(cm)
        sy, sx = np.where(cm & solid_all)
        if len(sx) == 0: continue
        if xs.max() - xs.min() <= 1.35 * cw:
            F[cm] = cell_of(sx.mean())
        else:
            sl, sk = ndi.label(cm & solid_all, structure=np.ones((3, 3)))
            sub = np.zeros((H, W), int) - 1
            for j in range(1, sk + 1):
                jm = sl == j
                if jm.sum() < 60: continue
                yy, xx = np.where(jm); sub[jm] = cell_of(xx.mean())
            known = sub >= 0
            if not known.any(): continue
            _, (iy, ix) = ndi.distance_transform_edt(~known, return_indices=True)
            F[cm] = sub[iy, ix][cm]
    if 'cuts' in row:
        F = np.where(mask, np.tile(F0, (H, 1)), -1)
    else:
        known = F >= 0
        _, (iy, ix) = ndi.distance_transform_edt(~known, return_indices=True)
        F = np.where(mask, F[iy, ix], -1)
    frames = []
    S = row.get('scale', cfg['scale'])
    for i in range(n):
        full = a.copy(); full[F != i] = 0
        allm = full[..., 3] >= GM
        if not allm.any(): continue
        ys, xs = np.where(allm); m = 6
        box = (max(0, xs.min() - m), max(0, ys.min() - m), min(W, xs.max() + m + 1), min(H, ys.max() + m + 1))
        cell = full[box[1]:box[3], box[0]:box[2]]
        sm = cell[..., 3] >= 200
        lum = cell[..., :3].astype(int).sum(2)
        dark = sm & (lum < 220)
        dy_, dx_ = np.where(dark)
        if len(dy_):
            ymax = dy_.max(); low = dy_ >= ymax - 12
            foot = (dx_[low].mean() * S, ymax * S)
        else:
            foot = (cell.shape[1] / 2 * S, cell.shape[0] * S); print('  frame', i, 'has no character (fx only)')
        rgb, alr = shrink(cell, S)
        frames.append(dict(rgb=rgb, al=alr, foot=foot, idx=i))
    return frames

def single(row):
    a = np.array(Image.open(row['sheet']).convert('RGBA'))
    if 'box' in row:
        x0, y0, x1, y1 = row['box']; a = a[y0:y1, x0:x1]
    solid = a[..., 3] >= 200
    ys, xs = np.where(a[..., 3] >= 40); m = 6
    cell = a[max(0, ys.min() - m):ys.max() + m + 1, max(0, xs.min() - m):xs.max() + m + 1]
    sm = cell[..., 3] >= 200
    dark = sm & (cell[..., :3].astype(int).sum(2) < 220)
    dy_, dx_ = np.where(dark); ymax = dy_.max(); low = dy_ >= ymax - 12
    S = row.get('scale', cfg['scale'])
    rgb, alr = shrink(cell, S)
    return [dict(rgb=rgb, al=alr, foot=(dx_[low].mean() * S, ymax * S))]

rows = {}
for name, row in cfg['rows'].items():
    rows[name] = single(row) if row.get('single') else cut_row(row)
    sk = set(row.get('skip', []))
    if sk: rows[name] = [f for f in rows[name] if f.get('idx') not in sk]
    print(name, len(rows[name]), 'frames')

allp = np.concatenate([f['rgb'][f['al'] >= 160].reshape(-1, 3) for fr in rows.values() for f in fr]).astype('uint8')
NC = cfg.get('colors', 56)
pm = Image.fromarray(allp.reshape(-1, 1, 3)).quantize(NC, method=Image.MEDIANCUT, dither=Image.NONE)
pal = np.array(pm.getpalette()[:NC * 3]).reshape(-1, 3)
def nearest(arr, pal):
    d = ((arr.reshape(-1, 1, 3).astype(int) - pal[None].astype(int)) ** 2).sum(-1); return d.argmin(1), d.min(1)
_, err = nearest(allp, pal)
bad = allp[err > 1800]
if len(bad) > 30:
    k = Image.fromarray(bad.reshape(-1, 1, 3)).quantize(10, method=Image.MEDIANCUT, dither=Image.NONE)
    pal = np.vstack([pal, np.array(k.getpalette()[:30]).reshape(-1, 3)])

meta = {}
for name, fr in rows.items():
    row = cfg['rows'][name]; nofx = row.get('nofx', False)
    outs = []
    for f in fr:
        rgb, alr = f['rgb'], f['al']; h, w = alr.shape
        solid = (alr >= 150)
        solid = ndi.binary_opening(solid, structure=np.ones((2, 2))) | (alr >= 235)
        lab, n = ndi.label(solid, structure=np.ones((3, 3)))
        if n:
            sizes = ndi.sum(solid, lab, range(1, n + 1)); solid = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s >= 12])
        glow = (alr >= row.get('glowmin', 40)) & ~solid
        idx, _ = nearest(rgb, pal); q = pal[idx].reshape(h, w, 3).astype('uint8')
        body = np.zeros((h, w, 4), 'uint8'); body[..., :3] = q; body[..., 3] = np.where(solid, 255, 0)
        ol = ndi.binary_dilation(solid, structure=ndi.generate_binary_structure(2, 1)) & ~solid
        body[ol] = OUT + (255,)
        fx = np.zeros((h, w, 4), 'uint8'); yy, xx = np.mgrid[0:h, 0:w]
        gl = glow & ((alr >= 110) | ((xx + yy) % 2 == 0)) & ~ol & ~solid
        if nofx: gl[:] = False
        else:
            g2, gn = ndi.label(gl, structure=np.ones((3, 3)))
            if gn:
                gs = ndi.sum(gl, g2, range(1, gn + 1)); gl = np.isin(g2, [j + 1 for j, v in enumerate(gs) if v >= 6])
        fx[..., :3] = q; fx[gl, 3] = 255
        outs.append(dict(body=body, fx=fx, foot=f['foot']))
    L = max(o['foot'][0] for o in outs); R = max(o['body'].shape[1] - o['foot'][0] for o in outs)
    T = max(o['foot'][1] for o in outs); B = max(o['body'].shape[0] - o['foot'][1] for o in outs)
    CWd = int(L + R) + 6; CHt = int(T + B) + 6; px, py = int(L) + 3, int(T) + 3
    for i, o in enumerate(outs):
        for key in ('body', 'fx'):
            img = np.zeros((CHt, CWd, 4), 'uint8'); arr = o[key]
            ox = px - int(round(o['foot'][0])); oy = py - int(round(o['foot'][1]))
            img[oy:oy + arr.shape[0], ox:ox + arr.shape[1]] = arr
            Image.fromarray(img).save(f'{outdir}/{name}_{i:02d}_{key}.png')
    meta[name] = dict(w=CWd, h=CHt, pivot=[px, py], n=len(outs))
    sh = Image.new('RGBA', (CWd * len(outs), CHt), (110, 150, 110, 255))
    for i in range(len(outs)):
        c = Image.open(f'{outdir}/{name}_{i:02d}_fx.png'); c.alpha_composite(Image.open(f'{outdir}/{name}_{i:02d}_body.png')); sh.alpha_composite(c, (i * CWd, 0))
    sh.save(f'{outdir}/{name}_sheet.png')
json.dump(meta, open(f'{outdir}/meta.json', 'w'), indent=1); print(meta)
