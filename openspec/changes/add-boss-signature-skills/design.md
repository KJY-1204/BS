# Design

## Context

A boss skill is data (`BossSkillDef`: effect, cooldown, damage, warning, count, radius, length, half width). `BossSkillSim.TryUse` starts the cooldown, stores a pending skill with the boss's position and the boss player's aim, and adds the skill event (carrying `Position` and `Angle`) that every client draws from; `Step` releases the pending skills. The clients draw the announcement from the event's place and the kind's definition of the effect; the survivor bots read `BossSkillSim.Evade`. `MatchWorld.UseBossSkill` is the only place the skill state is built, for the harness and the network server alike.

## Goals / Non-Goals

**Goals:** one signature skill per kind with no new key, no new effect, no new warning drawing and no wire change; a place chosen on the server so clients do not need to know survivor positions.
**Non-Goals:** player-chosen targets, a mark that follows a survivor, new effects (zones, leaps of the boss's own body), a client-side change.

## Decisions

1. **`BossSkillDef.AtTarget`** (bool). `BossSkillState` gets `HasTarget` and `TargetPosition`; `MatchWorld.UseBossSkill` fills them with the nearest living survivor to the boss (it already holds the survivors and their bodies).
2. **In `TryUse`:** when `def.AtTarget && state.HasTarget`, the pending place is `TargetPosition`; for a beam the place stays the boss's position and the aim is the angle from the boss to the target (`AimMath.QuantizeAngle(atan2)`). The announcement event is built from the same place and aim, so the clients draw it unchanged (rings at the event's position, the beam from the event's position along its angle).
3. **The three skills:** the giant's Q (shockwave) and the sniper's E (beam) are replaced in place, so the effect of each kind's skills stays distinct. The standard kind's F (phase skill) changes from the beam to the roar; that keeps its Q and E (ring, shockwave) and satisfies "the third skill's effect is not one of the Q and E effects". The client finds a roar's warning definition with `SkillWithEffect`, which looks at the kind's skills, then the phase skill, then the burst: the rally's warning time equals the burst's (1.0 s) so both draw alike.
4. **Names:** the skill cell text is the skill's `Name`; the skills are named "도약 강타", "표적 저격" and "집결". The standard kind's Q and E names still come from `BossSkills.Names`.
5. **Tests:** the many sim tests that use the default (standard) kind's Q and E are unchanged because the standard kind keeps them; the tests that assume the standard F is the beam (data, screen text, phase sim) are updated to the roar.
6. **Acceptance:** 360 matches per kind (seeds 1001 to 1360, the shipped build): survivors win 35-70%, compared with the last shipped measurement (standard 61.9, giant 62.8, sniper 56.9). If a kind leaves the gate, the new skill's damage, cooldown, warning or count is tuned, not the gate.

## Risks / Trade-offs

- A skill that lands on a survivor with a long warning is easier to dodge than one centred on the boss, but it reaches survivors who keep away from the boss; the values are first values and the bots now dodge, so the measurement decides.
- The rally adds mobs, and more mobs give the survivors more experience (seen before with the summon refill); the count and cooldown are small and the measurement checks it.
- The standard kind loses the beam; the sniper still has beams, and the standard kind keeps ring and shockwave.
