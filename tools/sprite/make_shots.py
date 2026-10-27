"""Cuts single projectile sprites out of the exported effect images and the AI sheets, all pointing right (+x).
usage: make_shots.py <scratch dir holding giant_src.png / std_src.png>   -> ArtSource/Projectiles/*.png"""
import os, sys
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ART = r'E:\Unity\BS\ArtSource'
OUT = os.path.join(ART, 'Projectiles')
OL = (52, 26, 34)
os.makedirs(OUT, exist_ok=True)
SCR = sys.argv[1] if len(sys.argv) > 1 else '.'

def load(p, box=None):
    im = Image.open(p).convert('RGBA')
    return np.array(im.crop(box) if box else im)

def trim(a, pad=1):
    ys, xs = np.where(a[..., 3] > 0)
    a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    return np.pad(a, ((pad, pad), (pad, pad), (0, 0)))

def outline(a):
    solid = a[..., 3] > 0
    ol = ndi.binary_dilation(solid, structure=ndi.generate_binary_structure(2, 1)) & ~solid
    a = a.copy(); a[ol] = OL + (255,); return a

def core(a, bright=140, keep=1):
    """Pixel art with a halo around it: keeps the bright body, drops the dim glow, and draws a fresh outline."""
    rgb = a[..., :3].astype(int); lum = rgb.sum(2) / 3
    red = (rgb[..., 0] > 170) & (rgb[..., 1] < 110)
    body = (a[..., 3] > 200) & ((lum >= bright) | red)
    body = ndi.binary_closing(body, structure=np.ones((3, 3)))
    lab, n = ndi.label(body, structure=np.ones((3, 3)))
    if n:
        sizes = ndi.sum(body, lab, range(1, n + 1)); order = np.argsort(sizes)[::-1][:keep]
        body = np.isin(lab, [i + 1 for i in order])
    out = a.copy(); out[..., 3] = np.where(body, 255, 0)
    return outline(trim(out, 2))

def smooth(a, scale, colors=24):
    """A smooth illustration turned into pixel art with a small shared palette and an outline."""
    f = a.astype(float); pm = f[..., :3] * (f[..., 3:4] / 255)
    h, w = a.shape[:2]; nh, nw = max(1, round(h * scale)), max(1, round(w * scale))
    ch = [np.array(Image.fromarray((pm[..., c] if c < 3 else f[..., 3]).astype('float32'), 'F').resize((nw, nh), Image.BOX)) for c in range(4)]
    al = ch[3]; rgb = np.clip(np.dstack(ch[:3]) / np.maximum(al[..., None] / 255, 1e-3), 0, 255)
    solid = al >= 120
    pal = Image.fromarray(rgb[solid].astype('uint8').reshape(-1, 1, 3)).quantize(colors, method=Image.MEDIANCUT, dither=Image.NONE)
    p = np.array(pal.getpalette()[:colors * 3]).reshape(-1, 3)
    idx = ((rgb.reshape(-1, 1, 3).astype(int) - p[None].astype(int)) ** 2).sum(-1).argmin(1)
    out = np.zeros((nh, nw, 4), 'uint8'); out[..., :3] = p[idx].reshape(nh, nw, 3); out[..., 3] = np.where(solid, 255, 0)
    return outline(trim(out, 1))

def save(name, a, rot=0):
    im = Image.fromarray(a)
    if rot: im = im.rotate(rot, expand=True, resample=Image.NEAREST)
    im.save(os.path.join(OUT, name + '.png')); print(name, im.size)

R = lambda *p: os.path.join(ART, *p)
# Reimu: one ofuda, turned so its long side leads
save('shot_reimu', core(load(R('Survivors', 'Reimu', 'Exports', 'reimu_ofuda_2.png'), (46, 38, 80, 84)), 150), rot=90)
# Marisa: the big star with its trail
save('shot_marisa', core(load(R('Survivors', 'Marisa', 'Exports', 'marisa_star_shot.png'), (4, 22, 84, 78)), 190))
# Sakuya: one knife
save('shot_sakuya', outline(trim(load(R('Survivors', 'Sakuya', 'Exports', 'sakuya_knives_shot.png'), (22, 54, 75, 71)), 1)))
# Sanae: the middle amulet of her pillar, long side leading
save('shot_sanae', core(load(R('Survivors', 'Sanae', 'Exports', 'sanae_pillar_fx.png'), (44, 33, 72, 82)), 130), rot=90)
# Sniper: the bullet
b = load(R('Bosses', 'Sniper', 'Exports', 'sniper_beam_shot.png'), (195, 57, 214, 68))
l = b[..., :3].astype(int).sum(2) / 3
b[..., 3] = np.where(l < 125, 255, 0)
save('shot_sniper', outline(trim(b, 1)))
# Giant: the red comet and the seal circle
save('shot_giant', smooth(load(os.path.join(SCR, 'giant_src.png'), (1308, 895, 1450, 928)), 0.45, 20))
save('fx_seal', smooth(load(os.path.join(SCR, 'giant_src.png'), (1212, 872, 1310, 978)), 0.6, 24))
# Standard boss: the crescent
save('shot_standard', smooth(load(os.path.join(SCR, 'std_src.png'), (1078, 504, 1122, 563))[:, ::-1].copy(), 1.0, 20))
# Mob art (the enemy sheet's bottom strip): the ghost's flame shot, tip leading, and the UFO's blast ring
MOB = os.path.join(ART, 'Reference', 'Touhou_Enemies_OriginalFrames_.png')
def alpha_core(a, thr=150):
    a = a.copy(); a[..., 3] = np.where(a[..., 3] >= thr, 255, 0)
    lab, n = ndi.label(a[..., 3] > 0, structure=np.ones((3, 3)))
    if n:
        sizes = ndi.sum(a[..., 3] > 0, lab, range(1, n + 1)); a[..., 3] = np.where(lab == (int(np.argmax(sizes)) + 1), 255, 0)
    return outline(trim(a, 1))
save('shot_mob_ghost', alpha_core(load(MOB, (830, 965, 868, 1012))), rot=-90)
save('fx_blast', alpha_core(load(MOB, (1200, 965, 1246, 1012))))
