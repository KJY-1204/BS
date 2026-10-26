# Tasks

## 1. The reward (rescue-points)

- [x] 1.1 `BossSkillSim.CooldownRate` for the cooldowns only, `RescueConfig.ContestCooldownRate` (1.5) and `MatchWorld` sets the rate from `RescuePoint.Contested`

## 2. Tests (rescue-points)

- [x] 2.1 The sim: a rate above 1 runs the cooldowns down faster, the warning of an announced skill is not sped up, the default rate is 1
- [x] 2.2 The world: a boss in the circle has its cooldowns run 1.5 times as fast, outside or with no point the normal rate, with the contest off the normal rate

## 3. Balance and documentation

- [x] 3.1 Harness: 360 matches per kind (seeds 1001 to 1360), keep every kind in 35-70%; save in `tools/harness/contest-reward/`
- [x] 3.2 README (the reward and the measurement), the test count, the whole EditMode suite passes
