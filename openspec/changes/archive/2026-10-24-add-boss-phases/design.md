# Design

## Context

A boss kind is data (`BossDef`): health, speed, a basic shot and two skills (`BossSkillDef`, the Q and E slots, each with an effect: shot ring, shockwave, beam or roar). `BossSkillSim` runs the cooldowns of the two skills and the announcements: `TryUse` checks the match and the boss, starts the cooldown (shortened by the enrage that grows with lost health), adds a `Pending` entry and a warning event; `Step` counts the warning down and `Release` applies the effect (a ring of hostile projectiles, a blast, a beam on a line, summoned mobs around the boss). `MatchWorld.StepCombat` runs it each step, and `CombatServer` publishes the events that the world and the skill requests make. The boss player's cooldowns reach only the boss player (`PlayerSummon`, one replicated byte per skill), the boss player's keys are Q, E, R (a device), Space (summon), 1 to 5 (summon kinds), C (clear order), the mouse (aim, fire, order), and F is unused. Clients draw a warning from the skill event and the playing kind (`BossDef.SkillWithEffect`, which looks the effect up in the kind's skills). The room-level values everyone sees (time left, winner) are replicated by `RoleTable`. The harness has a boss bot that presses every skill slot, a recorder that notes when the boss's health first falls to 50 and 25 percent, and an override table.

Places that assume two skills: `BossSkills.Count`, the cooldown array of `BossSkillSim`, `ServerWriteSkills(c0, c1)`, `PlayerSummon.SkillCooldown`, the key loop and the auto-skill development argument, `SummonHud.DrawSkills`, the bot's slot loop.

See proposal.md for why. The exploration chose: phases decided by the boss's health, a third skill unlocked in phase 2 plus an announced burst of mobs, phase 3 as faster cooldowns, no new mechanics (the effects reuse the four that exist).

## Goals / Non-Goals

**Goals:**
- Discrete, announced moments in the boss's match built from the pieces that exist (the announcement and release of boss skills, the roar, the replicated room values).
- A third boss skill for each kind with no new effect.
- A way to compare the match with and without phases in the harness.

**Non-Goals:**
- New boss mechanics, arena changes, invulnerable transitions, changing the kinds' health or speed.
- More than three phases or per-kind phase thresholds (one pair of thresholds for every kind).

## Decisions

**1. The phase lives in `BossSkillSim`, decided from the boss's health each combat step.**
`BossSkillSim.Phase` (1 to 3) is reset with the rest of the sim (`Reset`). Each combat step the world calls `UpdatePhase(healthFraction, enabled, ...)`: the next phase is the greater of the current one and the one the health gives (`BossPhases.PhaseOf`: 3 at 33 percent or less, 2 at 66 percent or less, else 1), so a phase never goes back. When it rises, `BeginPhase` announces the burst. With the phases switched off the phase stays 1. Keeping it in the skill sim means the match, the server and the harness all get the same rule and a reset clears it.
Alternative: compute the phase from the health wherever it is needed. Rejected: it would go back if the boss's health ever rose, and the unlock and the burst need one place that sees the change once.

**2. The burst is a roar through the announcement that exists.**
`BeginPhase` adds a `Pending` entry with the effect `Roar`, the kind's burst data (`BossDef.PhaseBurst`: warning seconds and the mob count of phase 2; phase 3 uses one and a half times as many) and the boss's place, and a warning event with the roar's warning number. When the warning ends `Release` creates the mobs through the code the roar already uses, up to the mob cap. It costs no summon energy and is dropped when the match is not running or the boss has fallen, as every announcement is. The event goes into the combat events the server publishes. Every kind's burst warning is the same length as the giant's roar (1.0 second), because a client finds a warning's length by looking the effect up in the kind's data and the giant has both.
Alternative: a new event kind for a phase. Rejected: the warning, the ring and the release already exist; a new kind would need codec, client and tests for nothing new to draw.

**3. The third skill is `BossDef.PhaseSkill`, slot 2, key F.**
`BossSkills.Count` stays 2 (the base skills) and a new `SlotCount` is 3. `TryUse` for slot 2 returns a new result `Locked` while the phase is below 2, without starting a cooldown; from phase 2 it behaves like any slot (it starts ready because its cooldown starts at zero). `BossDef.SkillWithEffect` also looks at the phase skill and the burst after the kind's skills, so a client finds the warning of the unlocked effect. The phase skill's effect is never one of the kind's two skills, so the lookup is unambiguous (a data test keeps that true). Starting values, to be checked by the harness:

| Kind | Q, E | Phase skill (F) | Burst |
|---|---|---|---|
| Standard | shot ring, shockwave | beam: cooldown 14 s, damage 25, length 20, half width 0.6, warning 1.2 s | 8 mobs, 12 in phase 3 |
| Giant | shockwave, roar | shot ring: cooldown 12 s, damage 10, 14 shots, warning 0.8 s | 8, 12 |
| Sniper | shot ring, beam | shockwave: cooldown 10 s, damage 30, radius 4.5, warning 0.8 s | 8, 12 |

**4. Phase 3 is a factor on the cooldowns of all three skills.**
`BossPhases.ThirdPhaseCooldownFactor` (0.85) multiplies the cooldown that `TryUse` starts when the phase is 3, together with the enrage multiplier already applied. It is decided when a cooldown starts, so a running cooldown is not changed.

**5. Replication: one byte to everyone, one more cooldown to the boss.**
`RoleTable` replicates the phase like it replicates the time left (a variable everyone reads, written by the server each match step, cleared with the match). The boss player's third cooldown is one more owner-only byte next to the other two. Clients see the burst as the warning event and then ordinary mob replication. The banner is drawn when the replicated phase changes; the "locked" mark on the boss's F slot is the replicated phase below 2.

**6. The phases can be switched off.**
`MatchWorld.BossPhasesEnabled` (default on), the harness override `bossPhases` (0 or 1) and the development argument `-bsBossPhases 0` turn them off: the phase stays 1, F stays locked, no burst. The pacing comparison and any later rollback use it.

**7. The harness measures the phases.**
The recorder notes when each phase starts, the report gives their statistics next to the boss's health times, and the boss bot presses all three slots (`SlotCount`). The acceptance is what `tune-boss-pacing` established for normal bots: survivors win 35 to 70 percent, the median match is 480 to 840 seconds, and the boss reaches half health in at least half of the matches. If the third skill pushes the pacing out of it, the third skill's damage and cooldown are tuned first, then the burst size; the boss's health is last.

*Measured (see `tools/harness/phases/results.md`):* with the first values (factor 0.7) the giant fell to 29 percent survivor wins, below the gate, while the standard boss (52 percent) and the sniper (39 percent) stayed inside. Removing the factor gave the giant 45 percent, so the factor cost it about 16 points and the burst with the third skill about 15; a factor of 0.85 brought all three inside the gate (standard 62, giant 39, sniper 41 percent). Shrinking the burst to 6 mobs moved nothing beyond the noise, so the burst stays at 8. The first thing tuned was therefore the shared factor (the giant's shockwave and roar are the skills it shortens most); the third skill's values are unchanged.

## Risks / Trade-offs

- [The boss becomes too strong and the survivors' win share falls out of range] → the harness comparison with the phases on and off, with the third skill's values and the burst size as the first things to turn.
- [A burst appears on top of survivors who stand next to the boss] → the mobs appear 2.5 units from the boss (the roar's distance), the warning ring shows that radius for the whole warning, and the boss is not made invulnerable so survivors can punish it.
- [The standard boss is tuned to die near the time limit, so any change moves its win share a lot] → measured with 120 matches or more, and a continuous measure (the time the boss dies, falls) is reported next to the win share, as learned in the last two changes.
- [A skill that grows with lost health plus the phase 3 factor makes the end of the match very fast] → the enrage lower bound (shots at half cooldown) is kept; the harness reports falls and the first fall.
- [The warning of the unlocked skill is looked up by effect, so two skills with the same effect would share a ring] → the data test that the phase skill's effect differs from the Q and E effects.

## Open Questions

- Whether the banner should also name what the phase changes (for example "the boss can use a beam"). The first version shows the phase number only.
