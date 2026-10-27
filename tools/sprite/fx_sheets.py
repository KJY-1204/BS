"""Reimu's skill effects as frame strips for the importer: ArtSource/Effects/<name>.png + <name>.json (Aseprite-style frame list).
usage: fx_sheets.py <scratch dir holding s1_clean.png (몽상봉인), s2_src.png (= Reference/픽셀 아트 결계 오라.png), s3_src.png (= Reference/레이무풍 몽상천생 마법 이펙트 시트.png)>"""
import json, os, sys
import numpy as np
from PIL import Image

OUT = r'E:\Unity\BS\ArtSource\Effects'
os.makedirs(OUT, exist_ok=True)
SCR = sys.argv[1] if len(sys.argv) > 1 else '.'

def load(n): return np.array(Image.open(os.path.join(SCR, n)).convert('RGBA'))

def centroid(a, box, thr=150):
    x0, y0, x1, y1 = box
    ys, xs = np.where(a[y0:y1, x0:x1, 3] >= thr)
    return int(xs.mean()) + x0, int(ys.mean()) + y0

def cut(a, cx, cy, hw, hh):
    h, w = a.shape[:2]
    out = np.zeros((2 * hh, 2 * hw, 4), 'uint8')
    sx0, sy0 = max(0, cx - hw), max(0, cy - hh); sx1, sy1 = min(w, cx + hw), min(h, cy + hh)
    out[sy0 - (cy - hh):sy1 - (cy - hh), sx0 - (cx - hw):sx1 - (cx - hw)] = a[sy0:sy1, sx0:sx1]
    return out

def shrink(a, S, floor=40):
    """Area-average downscale keeping the soft edges; faint glow noise below `floor` alpha is dropped."""
    f = a.astype(float); pm = f[..., :3] * (f[..., 3:4] / 255)
    h, w = a.shape[:2]; nh, nw = round(h * S), round(w * S)
    ch = [np.array(Image.fromarray((pm[..., c] if c < 3 else f[..., 3]).astype('float32'), 'F').resize((nw, nh), Image.BOX)) for c in range(4)]
    al = ch[3]; rgb = np.clip(np.dstack(ch[:3]) / np.maximum(al[..., None] / 255, 1e-3), 0, 255)
    al = np.where(al < floor, 0, al)
    return np.dstack([rgb, al]).astype('uint8')

def save(name, frames, seconds):
    h, w = frames[0].shape[:2]
    sheet = Image.new('RGBA', (w * len(frames), h))
    for i, f in enumerate(frames): sheet.paste(Image.fromarray(f), (i * w, 0))
    sheet.save(os.path.join(OUT, name + '.png'))
    data = {'frames': [{'frame': {'x': i * w, 'y': 0, 'w': w, 'h': h}, 'duration': int(seconds[i] * 1000)} for i in range(len(frames))],
            'meta': {'size': {'w': w * len(frames), 'h': h}, 'frameTags': [{'name': 'fx', 'from': 0, 'to': len(frames) - 1}]}}
    json.dump(data, open(os.path.join(OUT, name + '.json'), 'w'))
    print(name, len(frames), (w, h))

# 결계: the eight frames of the aura ring (a 4 x 2 grid), each centred on its ring
s2 = load('s2_src.png')
s2[0:128, 540:1130] = 0  # the title plate
frames = []
for r in range(2):
    for c in range(4):
        box = (c * 418, 130 if r == 0 else 495, (c + 1) * 418, 495 if r == 0 else 941)
        cx, cy = centroid(s2, box)
        frames.append(shrink(cut(s2, cx, cy, 225, 225), 0.34))
save('reimu_barrier', frames, [0.09] * 8)

# 몽상봉인 frame 9 (명중): the burst where the seal lands
s1 = load('s1_clean.png')
cx, cy = centroid(s1, (985, 585, 1300, 892), 200)
save('reimu_q_burst', [shrink(cut(s1, cx, cy, 235, 235), 0.4)], [0.4])

# 몽상천생 frames 7-10 (top view of the area): the label plates cover the lower rim, so the upper half is mirrored down over them
s3 = load('s3_src.png')
PLATE_TOP = 748
frames = []
CENTER_HOLE = (0.3, 0.3, 0.3, 0.0)  # share of the ring radius cleared in the middle (the drawn Reimu), per frame
for k, box in enumerate(((40, 395, 290, PLATE_TOP), (350, 395, 705, PLATE_TOP), (730, 395, 1090, PLATE_TOP), (1105, 395, 1510, PLATE_TOP))):
    x0, y0, x1, y1 = box
    sub = s3[y0:y1, x0:x1, 3] >= 150
    ys, xs = np.where(sub)
    cx = (int(xs.min()) + int(xs.max())) // 2 + x0
    half = (int(xs.max()) - int(xs.min())) // 2
    top = int(ys.min()) + y0
    # the circle's centre row is where its outline is widest
    rows = np.where(sub.any(1))[0]
    widths = np.array([np.where(sub[r])[0].max() - np.where(sub[r])[0].min() for r in rows])
    cy = int(rows[widths >= widths.max() * 0.96].mean()) + y0
    print('ring', k, 'center', (cx, cy), 'half', half)
    img = s3.copy()
    img[PLATE_TOP:PLATE_TOP + 60, x0:x1] = 0
    img[cy + 1:cy + half + 14, x0:x1] = 0
    for y in range(cy + 1, cy + half + 14):  # the lower half is the upper half turned over
        sy = 2 * cy - y
        if y0 <= sy < PLATE_TOP: img[y, x0:x1] = img[sy, x0:x1]
    yy, xx = np.mgrid[0:img.shape[0], 0:img.shape[1]]
    img[((xx - cx) ** 2 + (yy - cy) ** 2) < (half * CENTER_HOLE[k]) ** 2, 3] = 0
    # keep only this frame's own pixels
    keep = np.zeros(img.shape[:2], bool); keep[y0:y1 + 40, x0:x1] = True
    img[~keep, 3] = 0
    frames.append(shrink(cut(img, cx, cy, 225, 225), 0.34))
frames = [frames[0], frames[2], frames[3]]  # the second one's centre could not be found reliably
save('reimu_r_ring', frames, [0.1, 0.1, 0.15])
