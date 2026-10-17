"""Per-owner summary of a server combat dump: python char_report.py host.log"""
import re
import sys
from collections import Counter, defaultdict

PATTERN = re.compile(r"\[BS\] combatdump server t=(\d+) (Fire|Hit|End|Skill) id=(\d+) mob=(\d+) kill=(\d) cont=(\d) angle=(\d+) owner=(\d+)")
NAMES = {0: "Reimu", 1: "Marisa", 2: "Sakuya", 3: "Sanae"}

fires = defaultdict(list)
hits = Counter()
cont = Counter()
ends = Counter()
skills = defaultdict(Counter)
per_projectile_hits = defaultdict(lambda: defaultdict(list))
for line in open(sys.argv[1], encoding="utf-8", errors="replace"):
    m = PATTERN.search(line)
    if not m:
        continue
    t, kind, ident, mob, kill, c, angle, owner = (int(m.group(1)), m.group(2), int(m.group(3)), int(m.group(4)),
                                                     int(m.group(5)), int(m.group(6)), int(m.group(7)), int(m.group(8)))
    if kind == "Fire":
        fires[owner].append((t, angle))
    elif kind == "Hit":
        hits[owner] += 1
        cont[owner] += c
        per_projectile_hits[owner][ident].append(mob)
    elif kind == "End":
        ends[owner] += 1
    elif kind == "Skill":
        skills[owner][ident] += 1

for owner in sorted(NAMES):
    f = fires[owner]
    if not f:
        print(f"{NAMES[owner]}: no shots")
        continue
    span = (f[-1][0] - f[0][0]) / 1000.0 or 1.0
    groups = Counter(t for t, _ in f)
    shots_per_attack = Counter(groups.values()).most_common(1)[0][0]
    multi = [ms for ms in per_projectile_hits[owner].values() if len(ms) > 1]
    repeats = sum(1 for ms in per_projectile_hits[owner].values() if len(set(ms)) != len(ms))
    print(f"{NAMES[owner]:7s} shots={len(f):4d} ({len(f) / span:5.1f}/s)  shots per attack={shots_per_attack}  attacks/s={len(groups) / span:4.1f}  "
          f"hits={hits[owner]:4d}  continuing hits={cont[owner]:3d}  projectiles with >1 hit={len(multi):3d}  "
          f"max hits by one projectile={max((len(ms) for ms in per_projectile_hits[owner].values()), default=0)}  same mob twice={repeats}  ends={ends[owner]}")
