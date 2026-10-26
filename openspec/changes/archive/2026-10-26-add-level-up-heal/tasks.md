# Tasks

## 1. The heal (survivor-leveling)

- [x] 1.1 `GrowthConfig.LevelUpHealFraction`, `MatchWorld.LevelUpHealFraction` and the heal in `ProcessGrowthEvents` after `ApplyStats` on a level-up
- [x] 1.2 `HarnessSettings` override `levelUpHeal` (0 to 1, else refused)
- [x] 1.3 Update the existing tests that expect a level-up to leave health unchanged

## 2. Tests (survivor-leveling)

- [x] 2.1 A hurt survivor gains 5% of the new maximum, full health stays full, a fallen survivor stays fallen, two levels heal twice, the heal never passes the maximum
- [x] 2.2 The override: 0 turns it off, 0.25 heals a quarter, a bad value is refused

## 3. Balance and documentation

- [x] 3.1 Harness: 360 matches per kind with the default heal (seeds 1001 to 1360), compared with the previous measurement; report wins, median, the share of matches under 200 s; keep the gate, adjust the fraction if a kind passes 70% or the heal does not reduce the early ends; save in `tools/harness/level-up-heal/`
- [x] 3.2 README (the heal, the override, the measurement), the test count, the whole EditMode suite passes
