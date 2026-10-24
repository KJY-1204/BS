# Tasks

## 1. Data and server state (boss-kind-traits)

- [x] 1.1 `BossDef` gets `SteadySeconds`, `SteadyShotMultiple`, `MovingContactMultiple`, `StillContactMultiple`; `BossCatalog` fills them (sniper 1 s and 1.6, giant 1.3 and 0.7, standard none) and `ForMatch` and `MatchSession` copy them
- [x] 1.2 `BossSim` tracks `Moving` and `StillSeconds` with `Advance(dt)` and returns `CurrentContactDamage`; `Reset` and `Clear` reset them
- [x] 1.3 `BossTraits` (static, pure): `ShotMultiple(def, stillSeconds)` and `ContactDamage(def, baseDamage, moving)`, rounded and never below one

## 2. Wiring (boss-kind-traits)

- [x] 2.1 `MatchWorld` calls `Boss.Advance` after `SetPosition` and multiplies the boss shot damage it passes to `SetModifiers`; `SurvivorSim` uses the boss's `CurrentContactDamage`
- [x] 2.2 `HarnessSettings` override `bossTraits` (0 or 1, default 1, other values refused) switches the traits off for a match

## 3. Tests (boss-kind-traits)

- [x] 3.1 Stillness: moving, standing, moving again, no boss, fallen boss
- [x] 3.2 Data: standard has no trait, every value is positive, a kind with a still time has a shot multiple above one
- [x] 3.3 Steady: after one second, before it, after moving, the shot in the air keeps its damage, enrage on top
- [x] 3.4 Momentum: moving, standing, the other kinds unchanged
- [x] 3.5 `bossTraits` 0 and 1 and a bad value; a same-seed match is repeatable with traits on

## 4. Balance and documentation (boss-kind-traits)

- [x] 4.1 Run the harness, 120 matches per kind with traits on and off on the same seeds; keep every kind in 35-70% survivor wins and the median match in 480-840 s with traits on; tune the trait numbers if a kind leaves the gate because of them; save the settings and a `results.md` in `tools/harness/traits/`
- [x] 4.2 README: the traits, the override, the measurement, the limits (invisible to players, bot-based values); update the test count; run the whole EditMode suite and confirm it passes
