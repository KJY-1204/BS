# Design

## Context

`SurvivorBot.Think` runs every `ReactionSeconds` (with jitter) and picks a walking direction: first a projectile dodge (`BotMath.Dodge`), then away from close mobs and the boss, then a fallen teammate, a rescue point, the boss. `BossSkillSim` keeps the announced skills as a private list (kind, position, remaining time, aim). The bots read only what a player could see, and an announcement is public (it is replicated as a warning event).

## Goals / Non-Goals

**Goals:** bots that leave an announced shockwave or beam, with no game change.
**Non-Goals:** avoiding shot rings and roars by this rule, any game balance change, perfect play (a bot that decides late or stands too close still gets hit).

## Decisions

1. **A read-only query on the sim:** `BossSkillSim.Evade(Vector2 position, float bodyRadius, out bool threatened)` returns the summed push away from every announced shockwave and beam that covers the position (shockwave: radially away from its centre; beam: perpendicular to its line, to the side the position is on; both with a margin of 0.3). Unannounced skills do not exist in the list, so nothing else is exposed.
2. **Bots call it in `Think`:** the evasion comes first, before the projectile dodge; the result is combined with a small pull to the arena centre so a bot at a wall does not push into it. Because `Think` runs every reaction time, a slower profile has less of the 0.8 to 1.2 second warning left, as a human would.
3. **Switch:** `BotMath.EvadeSkills` (static, default true, like `DodgeShots`). The harness measurements compare with the earlier ones (no evasion).
4. **Acceptance:** 360 matches per kind on new seeds with the evasion on; compare with the earlier 360-match regression (seeds 1001 to 1360 with no evasion). If a kind leaves the gate (35-70%, median 480-840 s), the boss's skill or survivor numbers are adjusted in a follow-up step of this change, not the evasion.

## Risks / Trade-offs

- The bots become stronger, so every survivor win share rises; the standard boss is at 57.8% and has 12 points to the ceiling. If it goes over, the boss's numbers are raised.
- Evasion plus the existing rules can make a bot dance at the edge of a circle; the margin and the pull to the centre keep that small and the harness only needs the average.
