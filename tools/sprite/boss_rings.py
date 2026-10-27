"""Boss warning rings, one picture per boss kind: ArtSource/Effects/boss_ring_<kind>.png + .json (a hollow ring squashed to a circle).
usage: boss_rings.py <scratch dir with h1_src.png (거대 붉은 폭발 시트) and i1_src.png (보라빛 무녀 시트)>"""
import json, os, sys
import numpy as np
from PIL import Image
SCR = sys.argv[1] if len(sys.argv) > 1 else '.'
OUT = r'E:\Unity\BS\ArtSource\Effects'
os.makedirs(OUT, exist_ok=True)
def ring(f, box, size):
    im = Image.open(os.path.join(SCR, f)).convert('RGBA').crop(box)
    a = np.array(im); a[..., 3] = np.where(a[..., 3] < 40, 0, a[..., 3])
    return Image.fromarray(a).resize((size, size), Image.LANCZOS)
def save(name, img):
    img.save(os.path.join(OUT, name + '.png'))
    w, h = img.size
    json.dump({'frames': [{'frame': {'x': 0, 'y': 0, 'w': w, 'h': h}, 'duration': 1000}],
               'meta': {'size': {'w': w, 'h': h}, 'frameTags': [{'name': 'fx', 'from': 0, 'to': 0}]}}, open(os.path.join(OUT, name + '.json'), 'w'))
red = ring('h1_src.png', (8, 625, 128, 682), 120)
save('boss_ring_giant', red); save('boss_ring_sniper', red)
save('boss_ring_standard', ring('i1_src.png', (309, 746, 697, 886), 140))
