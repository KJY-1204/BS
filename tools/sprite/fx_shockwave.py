"""A shockwave hitting the ground, seen from above: an expanding ring with cracks, flung rock shards and a dust skirt.
Writes ArtSource/Effects/shockwave_giant (red) and shockwave_sniper (a cooler red) as 8-frame strips of 96x96."""
import json, math, os
import numpy as np
from PIL import Image
OUT = r'E:\Unity\BS\ArtSource\Effects'
S = 96; C = (S - 1) / 2
YY, XX = np.mgrid[0:S, 0:S].astype(float)
D = np.hypot(XX - C, YY - C); ANG = np.arctan2(YY - C, XX - C)
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
DITH = np.tile(BAYER, (S // 4, S // 4))

def make(pal, seed):
    rock, dark, mid, light, white, dust = pal
    rng = np.random.RandomState(seed)
    # crack lines: 9 jagged rays with a bend, fixed for the whole animation
    rays = []
    for j in range(9):
        a0 = j * 2 * math.pi / 9 + rng.uniform(-0.2, 0.2); pts = []; a = a0; rr = 3
        while rr < 46:
            pts.append((rr, a)); rr += rng.uniform(4, 7); a += rng.uniform(-0.18, 0.18)
        rays.append(pts)
    shards = [(rng.uniform(0, 2 * math.pi), rng.uniform(0.55, 1.0), rng.randint(2, 4)) for _ in range(16)]
    frames = []
    for k in range(8):
        img = np.zeros((S, S, 4), 'uint8')
        r = 8 + k * 5.4; th = max(2.0, 6 - k * 0.4); fade = k / 7.0
        # dust skirt behind the ring
        sk = (D < r - 3) & (D > r - 9 - k) & (DITH > 0.5 + fade * 0.45)
        img[sk] = dust + (255,)
        # ground glow inside the ring early on
        if k < 4:
            img[(D < r * 0.85) & (DITH > 0.3 + k * 0.15)] = dark + (255,)
        # cracks, longer as the ring grows, then dimming
        for pts in rays:
            for rr, a in pts:
                if rr > r: break
                x, y = int(round(C + rr * math.cos(a))), int(round(C + rr * math.sin(a)))
                for dx, dy in ((0, 0), (1, 0), (0, 1)):
                    if 0 <= x + dx < S and 0 <= y + dy < S: img[y + dy, x + dx] = (dark if k > 5 else mid if k > 3 else white if k < 2 else light) + (255,)
        # the ring itself
        band = np.abs(D - r)
        keep = DITH >= max(0.0, (fade - 0.55) * 1.8)
        img[(band <= th + 1.5) & (band > th) & keep] = mid + (255,)
        img[(band <= th) & keep] = light + (255,)
        img[(band <= th * 0.45) & keep] = white + (255,)
        # rock shards flung out
        for a, f, sz in shards:
            rr = r * f + k * 1.5
            if rr > 46: continue
            x, y = int(round(C + rr * math.cos(a))), int(round(C + rr * math.sin(a)))
            for dx in range(sz):
                for dy in range(sz):
                    px, py = x + dx, y + dy
                    if 0 <= px < S and 0 <= py < S and (dx + dy) < sz + 1: img[py, px] = (rock if dx + dy else light) + (255,)
        frames.append(img)
    return frames

def save(name, frames, seconds):
    sheet = Image.new('RGBA', (S * len(frames), S))
    for i, f in enumerate(frames): sheet.paste(Image.fromarray(f), (i * S, 0))
    sheet.save(os.path.join(OUT, name + '.png'))
    json.dump({'frames': [{'frame': {'x': i * S, 'y': 0, 'w': S, 'h': S}, 'duration': int(seconds[i] * 1000)} for i in range(len(frames))],
               'meta': {'size': {'w': S * len(frames), 'h': S}, 'frameTags': [{'name': 'fx', 'from': 0, 'to': len(frames) - 1}]}},
              open(os.path.join(OUT, name + '.json'), 'w'))
    print(name, len(frames))

RED = ((55, 38, 44), (90, 12, 26), (205, 40, 55), (255, 120, 110), (255, 240, 230), (120, 100, 105))
VIO = ((50, 40, 55), (70, 15, 50), (190, 45, 80), (255, 140, 130), (255, 245, 235), (125, 105, 115))
t = [0.05, 0.05, 0.06, 0.06, 0.07, 0.08, 0.1, 0.12]
save('shockwave_giant', make(RED, 3), t)
save('shockwave_sniper', make(VIO, 8), t)
