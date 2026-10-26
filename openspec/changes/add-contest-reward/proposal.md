# Proposal

## Why

A boss that stands in an open rescue point stops the survivors from capturing it, but gains nothing from it: the point costs the boss player time spent away from chasing, and the only result is a denied heal. That makes contesting a sacrifice the boss player has little reason to choose. A small reward that is not about the number of mobs (more mobs have turned out to give the survivors experience) makes it a real choice: hold the point, or chase the team.

## What Changes

- While the boss contests an open rescue point (its body overlaps the circle, which already stops the capture), the cooldowns of its three skills run 50% faster. Nothing else changes: warnings, damage, the boss's shots and the summon energy are not sped up, and the cooldowns run at the normal rate when the boss is not contesting.
- The effect follows the existing contest exactly (the same condition and the same switch): when the contest is off (`rescueContest` 0) there is no reward either.
- It is applied by the server only; no screen, message or wire format changes. The boss player already sees the cooldowns shrinking faster.
- The balance gate stays: 35-70% survivor wins for the standard, giant and sniper boss on 360 matches each, compared with the last measurement (standard 48.1%, giant 61.4%, sniper 57.2%).

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `rescue-points`: a boss that contests a point has its skill cooldowns run faster.

## Impact

- `RescueConfig` (the factor), `BossSkillSim` (a cooldown rate), `MatchWorld` (sets the rate from the contest), tests, README, `tools/harness/contest-reward/`.
