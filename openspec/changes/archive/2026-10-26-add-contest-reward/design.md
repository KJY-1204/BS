# Design

## Context

`MatchWorld.StepCombat` computes whether the boss's body overlaps the open point, passes it to `RescuePointSim.Step` (which sets `Contested`, honouring `ContestEnabled`), and then steps `BossSkillSim` with the step's `dt`. `BossSkillSim.Step` uses `dt` for both the cooldowns and the announced skills' remaining time.

## Goals / Non-Goals

**Goals:** a server-only reward tied to the existing contest, on cooldowns only.
**Non-Goals:** healing the boss, summon energy, faster shots, any screen change.

## Decisions

1. `BossSkillSim.CooldownRate` (default 1): the cooldown loop subtracts `dt * CooldownRate`; the pending skills still use `dt`.
2. `RescueConfig.ContestCooldownRate = 1.5f`. `MatchWorld.StepCombat` sets `BossSkills.CooldownRate = RescuePoint.Contested ? RescueConfig.ContestCooldownRate : 1f` right after the point is stepped, so the same condition and the same switch (`ContestEnabled`) decide it.
3. Because the point is stepped in the combat step and the skills in the same step, the rate applies from the step the contest starts and ends with the step it ends.
4. Acceptance: 360 matches per kind (seeds 1001 to 1360); every kind 35-70%. The harness boss bot chases the nearest survivor and contests the point only when survivors stand in it, so the measured effect is a lower bound; if a kind leaves the gate, the factor is lowered.

## Risks / Trade-offs

- The reward only pays when the boss player holds the point, which is a play decision the bot rarely makes; the measurement will show little change and the tests carry the rule.
- Faster cooldowns on a skill that lands on a survivor (the giant's leap strike, the sniper's targeted shot) make a held point dangerous for the survivors who try to take it; that is the intent.
