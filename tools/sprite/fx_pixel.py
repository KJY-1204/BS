"""Pixel-art effect rings drawn in code with a small palette (the sheets' own rings could not be cut from their glowing backgrounds).
Writes ArtSource/Effects/<name>.png + .json for: sanae_heal, sanae_push, sakuya_stop, hit_flash."""
import json, math, os
import numpy as np
from PIL import Image

OUT = r'E:\Unity\BS\ArtSource\Effects'
os.makedirs(OUT, exist_ok=True)
S = 64
YY, XX = np.mgrid[0:S, 0:S].astype(float)
C = (S - 1) / 2
D = np.hypot(XX - C, YY - C)
ANG = np.arctan2(YY - C, XX - C)
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
DITH = np.tile(BAYER, (S // 4, S // 4))

def new(): return np.zeros((S, S, 4), 'uint8')
def put(img, mask, col): img[mask] = col + (255,)

def ring_frame(pal, r, spokes, phase, thick=2, glow=5, dashed=False):
    dark, mid, light, white = pal
    img = new()
    put(img, (np.abs(D - r) <= thick / 2 + glow) & (np.abs(D - r) > thick / 2 + 1) & (DITH < (1 - (np.abs(D - r) - thick / 2) / (glow + 1))), mid)
    put(img, np.abs(D - r) <= thick / 2 + 1, dark)
    inner = np.abs(D - r) <= thick / 2
    if dashed:
        inner &= (np.floor((ANG + phase) * spokes / (2 * math.pi)) % 2 == 0)
    put(img, inner, light)
    # four sparkles that turn around the ring
    for k in range(4):
        a = phase + k * math.pi / 2
        cx, cy = C + r * math.cos(a), C + r * math.sin(a)
        for dx, dy, col in ((0, 0, white), (1, 0, light), (-1, 0, light), (0, 1, light), (0, -1, light)):
            x, y = int(round(cx)) + dx, int(round(cy)) + dy
            if 0 <= x < S and 0 <= y < S: img[y, x] = col + (255,)
    return img

def save(name, frames, seconds):
    sheet = Image.new('RGBA', (S * len(frames), S))
    for i, f in enumerate(frames): sheet.paste(Image.fromarray(f), (i * S, 0))
    sheet.save(os.path.join(OUT, name + '.png'))
    json.dump({'frames': [{'frame': {'x': i * S, 'y': 0, 'w': S, 'h': S}, 'duration': int(seconds[i] * 1000)} for i in range(len(frames))],
               'meta': {'size': {'w': S * len(frames), 'h': S}, 'frameTags': [{'name': 'fx', 'from': 0, 'to': len(frames) - 1}]}},
              open(os.path.join(OUT, name + '.json'), 'w'))
    print(name, len(frames))

GREEN = ((22, 90, 52), (70, 190, 100), (170, 255, 170), (255, 255, 240))
BLUE = ((26, 38, 110), (80, 110, 230), (170, 200, 255), (255, 255, 255))
GOLD = ((120, 70, 20), (230, 170, 50), (255, 230, 120), (255, 255, 235))

save('sanae_heal', [ring_frame(GREEN, 27, 12, k * 0.3, thick=2, glow=5) for k in range(8)], [0.06] * 8)
save('sanae_push', [ring_frame(GREEN, 26, 16, -k * 0.25, thick=3, glow=3, dashed=True) for k in range(8)], [0.06] * 8)
save('sakuya_stop', [ring_frame(BLUE, 27, 12, k * 0.35, thick=2, glow=5, dashed=(k % 2 == 0)) for k in range(8)], [0.06] * 8)

def flash(k):
    img = new(); n = 5; r = 6 + k * 4; ln = 3 + k * 3
    for j in range(8):
        a = j * math.pi / 4 + (0.2 if k % 2 else 0)
        for t in range(r, r + ln):
            x, y = int(round(C + t * math.cos(a))), int(round(C + t * math.sin(a)))
            if 0 <= x < S and 0 <= y < S: img[y, x] = (GOLD[3] if t < r + ln // 2 else GOLD[1]) + (255,)
    core = D <= max(1, 7 - k * 1.5)
    put(img, core, GOLD[3])
    return img
save('hit_flash', [flash(k) for k in range(5)], [0.04] * 5)
