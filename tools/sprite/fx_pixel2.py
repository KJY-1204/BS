"""More pixel-art effects drawn in code: beams for Marisa's Final Spark and the boss's aimed shot, and the puff a device makes when it produces mobs.
Writes ArtSource/Effects/marisa_final_beam, boss_beam, puff (.png + .json)."""
import json, math, os
import numpy as np
from PIL import Image
OUT = r'E:\Unity\BS\ArtSource\Effects'
os.makedirs(OUT, exist_ok=True)
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
rng = np.random.RandomState(5)

def save(name, frames, seconds):
    h, w = frames[0].shape[:2]
    sheet = Image.new('RGBA', (w * len(frames), h))
    for i, f in enumerate(frames): sheet.paste(Image.fromarray(f), (i * w, 0))
    sheet.save(os.path.join(OUT, name + '.png'))
    json.dump({'frames': [{'frame': {'x': i * w, 'y': 0, 'w': w, 'h': h}, 'duration': int(seconds[i] * 1000)} for i in range(len(frames))],
               'meta': {'size': {'w': w * len(frames), 'h': h}, 'frameTags': [{'name': 'fx', 'from': 0, 'to': len(frames) - 1}]}},
              open(os.path.join(OUT, name + '.json'), 'w'))
    print(name, len(frames))

W, H = 96, 32
YY, XX = np.mgrid[0:H, 0:W].astype(float)
streak = rng.rand(W * 2, H)  # fixed noise the streaks are read from

def beam(pal, k, wobble):
    dark, mid, light, white = pal
    img = np.zeros((H, W, 4), 'uint8')
    c = (H - 1) / 2
    # the beam breathes a little along its length and streaks slide along it
    half = 12 + 1.2 * np.sin(XX / 9.0 + k * 1.6) * wobble
    d = np.abs(YY - c) / half
    sx = (XX.astype(int) + k * 11) % (W * 2)
    noise = streak[sx, YY.astype(int)]
    dith = np.tile(BAYER, (H // 4, W // 4))
    edge = d <= 1.0
    img[edge] = dark + (255,)
    img[d <= 0.86] = mid + (255,)
    m = (d <= 0.62) & ((noise > 0.25) | (dith < 0.6)); img[m] = light + (255,)
    img[d <= 0.34] = light + (255,)
    img[(d <= 0.2) | ((d <= 0.4) & (noise > 0.8))] = white + (255,)
    # fade in at the caster's end and out at the far end
    fade = np.clip(XX / 14.0, 0, 1) * np.clip((W - 1 - XX) / 10.0, 0, 1)
    keep = fade > dith
    img[..., 3] = np.where(keep, img[..., 3], 0)
    return img

GOLD = ((150, 80, 20), (240, 160, 40), (255, 225, 110), (255, 255, 240))
RED = ((90, 10, 30), (210, 40, 60), (255, 130, 130), (255, 240, 235))
save('marisa_final_beam', [beam(GOLD, k, 1.0) for k in range(4)], [0.06] * 4)
save('boss_beam', [beam(RED, k, 0.8) for k in range(4)], [0.05] * 4)

# the puff: a ring of grey-violet smoke that grows and thins
P = 48
Y2, X2 = np.mgrid[0:P, 0:P].astype(float)
D2 = np.hypot(X2 - (P - 1) / 2, Y2 - (P - 1) / 2)
A2 = np.arctan2(Y2 - (P - 1) / 2, X2 - (P - 1) / 2)
SM = ((60, 50, 90), (110, 95, 150), (170, 155, 205), (225, 215, 245))
lump = 1.0 + 0.07 * np.sin(A2 * 5 + 1.0) + 0.05 * np.sin(A2 * 9)
frames = []
for k in range(6):
    r = 6 + k * 2.4; th = max(2.0, 6 - k * 0.7)
    img = np.zeros((P, P, 4), 'uint8')
    dist = np.abs(D2 / lump - r)
    dith = np.tile(BAYER, (P // 4, P // 4))
    thin = k / 5.0
    body = dist <= th
    img[body & (dith >= thin * 0.7)] = SM[1] + (255,)
    img[(dist <= th * 0.6) & (dith >= thin * 0.5)] = SM[2] + (255,)
    img[(dist <= th * 0.25) & (dith >= thin * 0.4)] = SM[3] + (255,)
    edge = (dist > th) & (dist <= th + 1.2) & (dith >= thin * 0.5)
    img[edge] = SM[0] + (255,)
    frames.append(img)
save('puff', frames, [0.06, 0.06, 0.07, 0.07, 0.08, 0.1])
