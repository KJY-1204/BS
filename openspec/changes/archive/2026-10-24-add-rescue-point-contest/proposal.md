# Proposal

## Why

The rescue point gives the survivors a second thing to do, but the boss player has no answer to it: the survivors can stand in the circle and be healed at no cost, and the measurement shows the point lifting the survivors' win share by 9 to 19 points (the standard boss is at 68%, next to the 70% ceiling). A map objective should be contestable. The smallest contest that needs no new input is the boss's body: going to the point is the boss player's way to stop it.

## What Changes

- While the boss's body overlaps an open rescue point's circle, the point is contested: its progress does not rise, however many survivors stand inside. Progress already made is kept, and the point's life keeps running.
- The boss is not slowed, hurt or blocked by the point; mobs inside are still slowed.
- Every player sees that the point is contested: the bar changes colour and its text says so.
- The harness gets an override `rescueContest` (1 is the default, 0 turns the contest off) so the effect can be measured against the build where the boss cannot stop a capture. The harness boss bot already chases the nearest survivor, so it contests whenever survivors stand in the circle.
- The balance gate is kept: 35-70% survivor wins and a median match of 480-840 s for the standard, giant and sniper boss.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `rescue-points`: the capture rule gains the boss's contest, the slow requirement no longer says the boss's presence does not stop the progress, the visible state shows the contest, and the contest has a switch.

## Impact

- `RescuePointSim` (contest flag and rule), `MatchWorld` (tells the point whether the boss overlaps it), `RoleTable` (replicated contested flag), `MatchHud` (bar colour and text), `HarnessSettings` (`rescueContest`), tests, README, `tools/harness/rescue-contest/`.
