# Tasks

## 1. The point's rules (rescue-points)

- [x] 1.1 `RescueConfig` constants and `RescuePointSim` (schedule, place from seed, progress, capture, close on life)
- [x] 1.2 `MobSim` speed zone (centre, radius, factor) applied in `Chase`; devices and the boss unaffected
- [x] 1.3 `MatchWorld` steps the point (only with a boss that stands and a running match), applies the zone while open, heals the survivors inside on capture; `rescuePoints` override (0 or 1, other values refused)

## 2. Replication and screens (rescue-points)

- [x] 2.1 `RoleTable` replicates open, centre, progress and seconds left from the world; reset at match start and end
- [x] 2.2 The circle in the world and the bar (progress, seconds left) for every player; a windowed capture checks the layout

## 3. Bots (rescue-points)

- [x] 3.1 A survivor bot with nothing near walks to an open point and stands in it; the boss bot is unchanged

## 4. Tests (rescue-points)

- [x] 4.1 Schedule: first at 90 s, gap after capture, close at 45 s, none without a boss or after the match
- [x] 4.2 Place: same seed same places, inside the arena, far from the boss start
- [x] 4.3 Capture: one survivor 12 s, four survivors 3 s, leaving keeps progress, a fallen survivor does not count
- [x] 4.4 Heal and slow: half the maximum for those inside only, no revival, no experience; mobs at 60% inside and full outside and after closing; devices and boss unaffected
- [x] 4.5 Override: 0, 1 and a bad value; a same-seed match is repeatable with points on; the bot walks to the point

## 5. Balance and documentation (rescue-points)

- [x] 5.1 Harness: 120 matches per kind, points on and off, same seeds; keep every kind in the 35-70% gate and the median match in 480-840 s; adjust the heal or the slow if a kind leaves it; save settings and `results.md` in `tools/harness/rescue/`
- [x] 5.2 Multi-process check: host and clients show the same circle and progress, no exceptions; windowed capture of the circle and bar
- [x] 5.3 README (the point, numbers, override, measurement, limits) and the test count; the whole EditMode suite passes
