# Tasks

## 1. The aimed skill (boss-skills)

- [x] 1.1 `BossSkillDef.AtTarget`, `BossSkillState.HasTarget` and `TargetPosition`, `MatchWorld.UseBossSkill` finds the nearest living survivor, `BossSkillSim.TryUse` uses its place (and, for a beam, the aim toward it) for the pending skill and the announcement
- [x] 1.2 The three skills in `BossCatalog` (giant Q, sniper E, standard F) with the first values, and the copy in `ForMatch` and `MatchSession` keeps `AtTarget`

## 2. Tests (boss-skills, boss-phases)

- [x] 2.1 The aimed skill: a shockwave, a beam and a roar at the target, the target moves during the warning, nobody alive, the nearest of two, an unmarked skill unchanged, the clone keeps the flag
- [x] 2.2 The data: each kind has its signature skill on the right key with the right effect, no new key, effects do not repeat, the values are data
- [x] 2.3 Update the tests that assume the standard kind's F is the beam (phase data, screen text, phase sim) and the bot evasion test of the beam
- [x] 2.4 A multi-process run: the announcement of each aimed skill is drawn at the target on every client, no exceptions

## 3. Balance and documentation

- [x] 3.1 Harness: 360 matches per kind (seeds 1001 to 1360) with the shipped values; keep every kind in 35-70%; tune the new skills if a kind leaves it; save in `tools/harness/signature-skills/`
- [x] 3.2 README (the three skills, the aimed-at-target rule, the measurement), the test count, the whole EditMode suite passes
