"""Softens the hard straight cut a sheet cell leaves on a frame's effect layer (fx): effect pixels within FADE px of a long
straight edge are thinned out with an ordered dither, so the effect fades out instead of ending in a ruler line.
usage: fixfx.py <nf dir> <row> [<row> ...]   (rewrites the <row>_NN_fx.png files in place)"""
import sys, glob
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

FADE = 14
MIN_RUN = 30
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0

def seeds(mask):
    s = np.zeros_like(mask)
    for axis in (0, 1):
        m = mask if axis == 0 else mask.T
        out = np.zeros_like(m)
        for shift in (1, -1):
            nb = np.roll(m, shift, axis=1)
            nb[:, 0 if shift == 1 else -1] = False
            edge = m & ~nb  # opaque with empty neighbour along the row
            for x in range(m.shape[1]):
                lab, n = ndi.label(edge[:, x])
                for k in range(1, n + 1):
                    run = lab == k
                    if run.sum() >= MIN_RUN: out[run, x] = True
        s |= out if axis == 0 else out.T
    return s

def fix(base):
    """base is the path of <row>_NN (no suffix); the body and fx layers are faded together along the cuts found in their union."""
    body = np.array(Image.open(base + '_body.png')); fx = np.array(Image.open(base + '_fx.png'))
    m = (body[..., 3] > 0) | (fx[..., 3] > 0)
    if not m.any(): return 0
    sd = seeds(m)
    if not sd.any(): return 0
    dist = ndi.distance_transform_edt(~sd)
    h, w = m.shape
    thr = np.tile(BAYER, (h // 4 + 1, w // 4 + 1))[:h, :w]
    keep = np.clip(dist / FADE, 0, 1) > thr
    drop = m & (dist < FADE) & ~keep
    body[drop, 3] = 0; fx[drop, 3] = 0
    Image.fromarray(body).save(base + '_body.png'); Image.fromarray(fx).save(base + '_fx.png')
    return int(drop.sum())
nf = sys.argv[1]
for row in sys.argv[2:]:
    for p in sorted(glob.glob(f'{nf}/{row}_*_fx.png')):
        n = fix(p[:-7])
        if n: print('fixed', p.split('/')[-1], n)

