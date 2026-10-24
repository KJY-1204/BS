# Tasks

## 1. Data and server (boss-kind-traits)

- [x] 1.1 `BossDef.SummonedSpeedMultiple`, the standard kind's values (1.25, name and text), copied in `ForMatch` and `MatchSession`; `BossSim.SummonedSpeedMultiple`
- [x] 1.2 `MobSim.SummonedSpeedFactor` in the chase and `MatchWorld.StepMobs` sets it from the boss

## 2. Tests (boss-kind-traits, boss-kinds)

- [x] 2.1 Speed: a summoned mob of the standard boss moves 25% faster, an automatic mob, a device and the other kinds mobs are unchanged, traits off is unchanged
- [x] 2.2 Update the data, text and label tests that said the standard kind has no trait

## 3. Balance and documentation

- [x] 3.1 Harness: the standard boss with `bossTraits` 0 and 1, 120 matches, same seeds, plus one run each of the giant and the sniper; keep the gate; reduce the multiple if the standard kind leaves it; save in `tools/harness/standard-trait/`
- [x] 3.2 README (the trait table and the measurement), the test count, the whole EditMode suite passes
