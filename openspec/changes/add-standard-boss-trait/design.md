# Design

## Context

`MobSim.Chase` moves mobs by their kind speed; a mob type byte carries its origin (summoned or automatic). `MatchWorld.StepMobs` steps the mobs every step. The kinds' traits live on `BossDef`, `BossSim` keeps the match's `BossDef` and the `TraitsEnabled` switch (the override `bossTraits`) and exposes the current multiples (`CurrentContactDamage`, `ShotMultiple`). The trait text and the screens read `BossDef.TraitName` and `TraitText`.

## Goals / Non-Goals

**Goals:** an identity for the standard boss that is server-only, data-driven and switchable with the other traits.
**Non-Goals:** changes to the giant's or the sniper's traits, a new input, a change to the summon energy or to automatic mobs.

## Decisions

1. `BossDef.SummonedSpeedMultiple` (1 = none), 1.25 for the standard kind. `BossSim.SummonedSpeedMultiple` returns it, or 1 when the traits are off or there is no boss.
2. `MobSim.SummonedSpeedFactor` (default 1) multiplies the chase step of summoned mobs only; `MatchWorld.StepMobs` sets it from the boss each step. Devices already skip the chase.
3. The text: name "지휘", description "소환한 몹이 25% 더 빨리 움직인다", checked by a test to mention the multiple like the other traits.
4. Tests that said the standard kind has no trait are updated: the data test, the text test and the label test.
5. Acceptance: 120 matches for the standard boss, trait on (this build) against off (`bossTraits` 0), same seeds, with every survivor result inside the gate; the giant and sniper runs with traits on are unchanged by construction and are checked with one run each.

## Risks / Trade-offs

- Measured first with the summon energy refilling 40% faster: survivors won 79.2% against 68.3% (more mobs, more experience), above the gate, so that version was dropped. Faster mobs give no extra experience; the effect is measured again, and if the standard kind drops under 35% the multiple is reduced.
