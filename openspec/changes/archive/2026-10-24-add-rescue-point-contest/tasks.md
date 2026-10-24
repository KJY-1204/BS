# Tasks

## 1. The rule (rescue-points)

- [x] 1.1 `RescuePointSim.Step` takes `bossInside`; with the contest on, the progress does not rise while the boss overlaps, the life keeps running; `Contested` and `ContestEnabled`
- [x] 1.2 `MatchWorld` computes `bossInside` from the boss's body and the circle; override `rescueContest` (0 or 1, other values refused)

## 2. Replication and screen (rescue-points)

- [x] 2.1 `RoleTable` replicates `rescueContested`; `MatchHud` shows an orange bar and the word in the text when contested (`RescueBarText` gets the flag)

## 3. Tests (rescue-points)

- [x] 3.1 Contest: the boss in the circle stops the progress and keeps what was made, leaving resumes it, the life keeps running, next to the circle does not contest, a fallen boss does not contest
- [x] 3.2 Override: 0 lets the boss not contest, 1 and a bad value; the bar text; a same-seed match with the contest is repeatable

## 4. Balance and documentation (rescue-points)

- [x] 4.1 Harness: 120 matches per kind with the contest on and off, same seeds; keep every kind in the gate; ease the radius or the capture time if a kind drops under 35%; save settings and `results.md` in `tools/harness/rescue-contest/`
- [x] 4.2 README (the contest, the override, the measurement, the limits), the test count, and the whole EditMode suite passes; a multi-process run shows the contested flag on every client
