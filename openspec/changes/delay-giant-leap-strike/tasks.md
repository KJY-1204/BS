# Tasks

## 1. The opening cooldown (boss-skills)

- [x] 1.1 `BossSkillDef.OpeningCooldownSeconds`, the starting cooldowns in `BossSkillSim.Reset`, the giant's leap strike with 20 seconds
- [x] 1.2 The giant's health 236000 (catalog, the pacing test, the README table)
- [x] 1.3 Update the tests that use the giant's Q right at the start of a match

## 2. Tests (boss-skills)

- [x] 2.1 The sim: the opening cooldown sets the starting cooldown, a request is refused as `Cooldown` and then accepted, a reset starts it again, a skill without one starts ready, the giant's E is ready at once
- [x] 2.2 The data: only the giant's leap strike has an opening cooldown, of 20 seconds

## 3. Balance and documentation

- [x] 3.1 Harness: 360 matches per kind (seeds 1001 to 1360) with the shipped values; keep every kind in 35-70%; report the matches under 200 s; save in `tools/harness/leap-delay/`
- [x] 3.2 README (the opening cooldown and the measurement), the test count, the whole EditMode suite passes
