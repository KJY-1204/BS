# Tasks

## 1. The evasion (harness-bots)

- [x] 1.1 `BossSkillSim.Evade(position, bodyRadius)` returns the push out of announced shockwaves and beams (zero when none covers the position)
- [x] 1.2 `BotMath.EvadeSkills` switch and the use in `SurvivorBot.Think` before the projectile dodge, with the wall pull

## 2. Tests (harness-bots)

- [x] 2.1 `Evade`: inside a shockwave pushes away from its centre, outside gives zero, nothing announced gives zero, a beam pushes across its line to the side the position is on, behind the start or beyond the end gives zero
- [x] 2.2 The bot: walks out of an announced shockwave and beam, is unchanged with the switch off, the game rules (warning time, damage) are untouched; existing bot tests that depend on bots ignoring skills still pass

## 3. Balance and documentation

- [x] 3.1 Harness: 360 matches per kind with the evasion on (seeds 1001 to 1360), compared with the earlier regression; report wins, median, the share of matches under 200 s; if a kind leaves the gate adjust the boss numbers and measure again; save in `tools/harness/evasion/`
- [x] 3.2 README (the bot rule and the measurement), the test count, the whole EditMode suite passes
