# Design

## Context

`MatchWorld.ProcessGrowthEvents` drains the growth events of the step; for each `LevelUp` it re-works the survivor's stats (`ApplyStats`, which sets the new maximum health, not touching the current health) and keeps the event for the server and the clients. `SurvivorSim.Heal(id, amount)` heals a living survivor up to the maximum and ignores a fallen one. The harness and the network server both step the same `MatchWorld`, so one change covers both.

## Goals / Non-Goals

**Goals:** a small, regular recovery that scales with how fast the survivors level; one place, server-side, plain C#; switchable.
**Non-Goals:** passive regeneration, healing on other events, any change to the amount of experience, the maximum health per level or the other heals.

## Decisions

1. **Where:** in `ProcessGrowthEvents`, right after `ApplyStats` for a `LevelUp` event: `Survivors.Heal(id, Max(1, Round(MaxHealthOf(id) * LevelUpHealFraction)))`. One level-up event is emitted per level gained, so several levels heal several times with no extra code.
2. **Value:** `GrowthConfig.LevelUpHealFraction = 0.05f` is the default; `MatchWorld.LevelUpHealFraction` (settable) holds the value in use; the override `levelUpHeal` (0 to 1) sets it before the match starts.
3. **The fallen:** `SurvivorSim.Heal` already returns 0 for a survivor with no health, so a fallen survivor is left alone and revival keeps its own rule.
4. **Acceptance:** 360 matches per boss kind on the seeds of the earlier regression (1001 to 1360), heal 0.05 (with the retuned boss health, see the measurement) against the previous measurement (evasion build: wins 61.1 / 48.6 / 62.5 for standard / giant / sniper, ends under 200 s 24 / 67 / 81). Keep every kind in 35-70% and the median in 480-840 s. If a kind passes 70%, lower the fraction (0.05) or raise the boss numbers and say which. The share of early ends is reported; it is the point of the change.
5. **Existing tests** that level a survivor up and expect its health unchanged either use `levelUpHeal` 0 or are updated to the new rule.

## Risks / Trade-offs

- Survivors become stronger overall, so the standard and sniper bosses (about 61-62%) may exceed 70%; a smaller fraction or a boss retune would follow, not a gate change.
- A heal on a level-up rewards fast levelling, which is the survivors' strength anyway; if the early ends do not fall the idea is dropped (as the grace was) and the other candidates (out-of-combat regeneration, an earlier first rescue point) are looked at.
