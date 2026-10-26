# Design

## Context

`BossSkillSim.Reset()` sets every slot's cooldown to 0. The boss's screen shows each slot's cooldown from the replicated server value. An earlier try to give every boss skill an opening grace (0, 20, 30, 45 s on the giant) showed no clear effect and was dropped; the cause of the early ends was then found in a skill that targets a survivor, so a delay on that skill alone is tested.

## Goals / Non-Goals

**Goals:** a data-driven starting cooldown per skill; the giant's leap strike held back; no screen, message or key change.
**Non-Goals:** delaying other skills or the boss's other attacks, a harness override (the value is data and the experiment was run by editing it).

## Decisions

1. `BossSkillDef.OpeningCooldownSeconds` (default 0). `BossSkillSim.Reset()` starts every slot's cooldown at `max(0, OpeningCooldownSeconds)` of that slot's skill (0 when the slot has no skill). Because `Reset(kind)` sets the kind first, a second match restarts it. The sim's other tests, which use the standard kind, see no change.
2. The giant's leap strike gets 20 seconds: the first values tried were 20 (measured) against a first-use that is otherwise at second 0.
3. The giant's health is raised to 236000 (a scan on 120 matches: 230000 gave 71.7%, 236000 gave 61.7%, 242000 gave 56.7%).
4. Acceptance: 360 matches per kind (seeds 1001 to 1360): every kind 35-70%; the giant's matches under 200 s reported against the 37 of the previous measurement.

## Risks / Trade-offs

- The time limit makes the win share steep in the boss's health; the chosen health sits between the steps of the scan and is confirmed on 360 matches.
- Tests that use the giant's Q at the start of a match need the opening cooldown run down first.
