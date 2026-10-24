# Design

## Context

`MatchWorld.Step` already receives the boss player's body position every step and calls `Boss.SetPosition`. The boss's shots get their damage from `Attacks.SetModifiers(BossOwnerId, damage, cooldownMultiplier)` once per step, from `BossAttack.Damage(BossShotDamage, healthFraction)`. Contact damage is read from `BossSim.ContactDamage` in `SurvivorSim.ApplyBossContactDamage`. Both are server-only, so a scaling there needs no wire change. The boss kinds are data in `BossCatalog`, copied into the match by `ForMatch` and `MatchSession`.

## Goals / Non-Goals

**Goals:**
- Two traits, one for the sniper (stand to aim) and one for the giant (keep moving), each a small data record.
- Server-only scaling; the same seed and settings still give the same match.
- A measurable on/off switch for the harness.

**Non-Goals:**
- A trait for the standard kind, a new boss skill, a new key, or anything the survivors choose.
- Showing the trait in the waiting room or the HUD (a later change; the README describes the traits).
- Changing any existing value of a kind. The traits are balanced around them.

## Decisions

1. **Stillness lives in `BossSim`.** `BossSim.Advance(dt)` compares the position set this step with the one before it: a move under 0.02 units in a step counts as still. It keeps `Moving` and `StillSeconds`, resets on `Reset` and `Clear`, and `MatchWorld` calls it right after `SetPosition`. Alternative: derive it from the body's `Moving` flag. Rejected: `MatchBody.Moving` is the input flag of the player, which is true while a key is held against a wall; position is what the survivors see.
2. **Trait data on `BossDef`:** `SteadySeconds` (0 = none), `SteadyShotMultiple`, `MovingContactMultiple` and `StillContactMultiple` (both 1 = none). `ForMatch` and `MatchSession` copy them. Alternative: a separate trait class. Rejected as more ceremony than four numbers.
3. **Shots:** `MatchWorld` multiplies the damage it hands to `SetModifiers` by `BossTraits.ShotMultiple(def, stillSeconds)`; the multiple is 1 unless the traits are on and the still time has reached `SteadySeconds`. The projectile keeps the damage it left with, which is how projectiles already work.
4. **Contact:** `BossSim.CurrentContactDamage` returns the contact damage scaled by the moving or still multiple (rounded, at least one). `SurvivorSim` uses it. With no trait it equals `ContactDamage`.
5. **Pure helper outside the network classes:** `BossTraits` is a static class with `ShotMultiple` and `ContactDamage` so tests need no Netcode.
6. **Switch:** `HarnessSettings` override `bossTraits` (0 or 1) sets a flag on the world that `BossTraits` consults through the match, default on. There is no dev argument; the dev builds play with traits.
7. **Acceptance:** with the standard, giant and sniper boss, 120 matches each with `bossTraits` 1 and 0, same seeds; the survivors' win share stays inside 35-70% and the median match in 480-840 s with traits on, and the difference between on and off is reported. Values are tuned only if a kind leaves the gate because of the trait.

## Risks / Trade-offs

- The giant is the closest kind to the 35% gate (39% with phases). Momentum costs it nothing while moving only if it keeps moving; a harness bot that stands still against the survivors would make it weaker. The numbers are therefore checked per kind, and Momentum is tuned (smaller bonus or penalty) if the giant drops under the gate.
- The sniper bot may never stand still, in which case Steady is never measured. The measurement notes this, and a test covers the rule itself.
- The traits are invisible to the players until a later change shows them. The README says so.
