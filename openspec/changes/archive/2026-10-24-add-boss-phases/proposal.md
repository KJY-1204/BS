# Proposal

## Why

The survivors have an arc over a match: they level up, learn skills, earn an R skill and choose an evolution. The boss player has nothing like it. In minute fourteen the boss can do exactly what it could in minute one; only the shots and skills hit a little harder and cool down a little faster as its health falls, a continuous change that never makes a moment. A fifteen-minute match asks the boss player to play one note. A few moments at which the boss changes, announced so the survivors can answer, give the boss side a story of its own that matches the survivors' growth, and give the survivors something to build toward and prepare for.

## What Changes

- A match with a boss has three phases, decided by the boss's health: phase 1 from the start, phase 2 when its health first falls to 66%, phase 3 when it first falls to 33%. A phase never goes back, a new match starts at phase 1, and every client learns the current phase.
- Each phase change is announced and has a body. For everyone it starts with a warning ring around the boss, and when the warning ends a burst of mobs appears around the boss (the roar of the other kinds, a bigger one in phase 3). The boss is not made invulnerable during it.
- Phase 2 unlocks a third boss skill on the F key, one for each kind: the beam for the standard boss, the shot ring for the giant, the shockwave for the sniper (the effect each kind does not already have). Before phase 2 pressing F does nothing. The skill follows the rules of Q and E: it needs a running match and a standing boss, has a cooldown of its own, costs no summon energy and grows with the boss's lost health like the others. It starts ready the moment it is unlocked.
- Phase 3 shortens the cooldown of all three skills (to 85%), on top of the enrage that already grows with lost health.
- The boss player's screen shows a third skill slot (F), marked as locked until phase 2. Everyone's screen shows a phase banner when the phase changes and the current phase near the boss's health bar.
- The phases can be switched off with a harness override (`bossPhases`), so a match without them stays measurable. The harness records when each phase starts, and its boss bot also uses the third skill.

Not changed: the Q and E skills and the enrage of shots, the boss kinds' health and speed, the summoning, the orders, the devices, what survivors can do.

## Capabilities

### New Capabilities
- `boss-phases`: the three phases, the announced burst of mobs, the unlocked third skill, the faster cooldowns of the last phase, and how all of it is shown.

### Modified Capabilities
- `boss-skills`: the boss's Q and E skills are its base skills; a third skill that a phase unlocks is defined by `boss-phases`.

## Impact

- Code: boss data (`BossDef` gets a phase skill and a burst for each kind), a small `BossPhases` rule (thresholds, cooldown multiplier, burst size), `BossSkillSim` (a third slot, a locked result, the burst through the existing announcement and release, the phase-3 cooldown), `MatchWorld` (tracks the phase from the boss's health each combat step), `CombatServer` and `RoleTable` (replicate the phase and the third cooldown), `PlayerSummon` (F key, third cooldown), `SummonHud` and the match HUD (third slot, banner, phase mark), the harness (recorder, report, bot, the `bossPhases` override).
- Network: one replicated byte for the phase, one more cooldown value for the boss player only; the warning and the mobs use the skill events and mob replication that exist. No new message.
- Tests: EditMode tests for the phase rule, the data of the nine skills and bursts, the locked skill and its effects, the burst, the cooldown change, the harness pieces and the screen texts; a multi-process run that reaches phases 2 and 3 and checks that every client sees them; the pacing harness run with the phases on and off.
- Not in scope: phase-specific arena changes, a boss that cannot be hurt during a transition, changing the kinds' health or speed (the third skill's values are tuned first if the pacing moves), new art or sounds.
