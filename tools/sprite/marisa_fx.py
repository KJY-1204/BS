"""Marisa's skill art from the user's sheets.
usage: marisa_fx.py <scratch dir holding m1_src.png (= Reference/마스터 스파크 빔 시전.png) and m2_src.png (= Reference/별빛 마녀의 가속 오라 스프라이트 시트.png)>
Writes m1_clean.png (the Master Spark sheet without card borders, badges and label plates; charpipe cuts the cast frames from it)
and the effect strips ArtSource/Effects/marisa_beam and marisa_stardust."""
import os, sys, json
import numpy as np
from PIL import Image

SCR = sys.argv[1] if len(sys.argv) > 1 else '.'
OUT = r'E:\Unity\BS\ArtSource\Effects'
os.makedirs(OUT, exist_ok=True)

m1 = np.array(Image.open(os.path.join(SCR, 'm1_src.png')).convert('RGBA'))
keep = np.zeros(m1.shape[:2], bool)
# the picture area of each card, inside its gold border, above its label plate
for x0, x1 in ((20, 308), (328, 611), (631, 905), (926, 1207), (1227, 1515)): keep[229:516, x0:x1] = True
for x0, x1 in ((20, 296), (317, 594), (616, 966), (985, 1258), (1278, 1515)): keep[604:938, x0:x1] = True
# the number badge in each card's top-left corner
for x0, y0 in ((20, 224), (328, 224), (631, 224), (926, 224), (1227, 224), (20, 599), (317, 599), (616, 599), (985, 599), (1278, 599)):
    keep[y0:y0 + 62, x0:x0 + 62] = False
m1[~keep, 3] = 0
Image.fromarray(m1).save(os.path.join(SCR, 'm1_clean.png'))

def shrink(a, S, floor=40):
    f = a.astype(float); pm = f[..., :3] * (f[..., 3:4] / 255)
    h, w = a.shape[:2]; nh, nw = round(h * S), round(w * S)
    ch = [np.array(Image.fromarray((pm[..., c] if c < 3 else f[..., 3]).astype('float32'), 'F').resize((nw, nh), Image.BOX)) for c in range(4)]
    al = ch[3]; rgb = np.clip(np.dstack(ch[:3]) / np.maximum(al[..., None] / 255, 1e-3), 0, 255)
    return np.dstack([rgb, np.where(al < floor, 0, al)]).astype('uint8')

def save(name, frames, seconds):
    h, w = frames[0].shape[:2]
    sheet = Image.new('RGBA', (w * len(frames), h))
    for i, f in enumerate(frames): sheet.paste(Image.fromarray(f), (i * w, 0))
    sheet.save(os.path.join(OUT, name + '.png'))
    json.dump({'frames': [{'frame': {'x': i * w, 'y': 0, 'w': w, 'h': h}, 'duration': int(seconds[i] * 1000)} for i in range(len(frames))],
               'meta': {'size': {'w': w * len(frames), 'h': h}, 'frameTags': [{'name': 'fx', 'from': 0, 'to': len(frames) - 1}]}},
              open(os.path.join(OUT, name + '.json'), 'w'))
    print(name, len(frames), (w, h))

# the beam: frame 8 (full power) from just past its magic circle, fading in at the near end, and the same beam turned over for a shimmer
beam = m1[605:936, 745:966].copy()
ramp = np.clip(np.arange(beam.shape[1]) / 28.0, 0, 1)
vy = np.arange(beam.shape[0]); edge = np.clip(np.minimum(vy, beam.shape[0] - 1 - vy) / (0.3 * beam.shape[0]), 0, 1)
beam[..., 3] = (beam[..., 3] * ramp[None, :] * edge[:, None]).astype('uint8')
a = shrink(beam, 0.5)
save('marisa_beam', [a, a[::-1].copy()], [0.06, 0.06])

# the stardust ring: the ring of stars from the element row, turned in 30 degree steps
m2 = np.array(Image.open(os.path.join(SCR, 'm2_src.png')).convert('RGBA'))
ring = Image.fromarray(m2[765:965, 5:255].copy())
ring = ring.resize((ring.size[0], round(ring.size[1] * 1.25)), Image.BICUBIC)  # the picture's ring is a little flat
side = 340
canvas = Image.new('RGBA', (side, side)); canvas.alpha_composite(ring, ((side - ring.size[0]) // 2, (side - ring.size[1]) // 2))
frames = [shrink(np.array(canvas.rotate(-30 * k, resample=Image.BICUBIC)), 0.5) for k in range(12)]
save('marisa_stardust', frames, [0.07] * 12)
