# Proposal

## Why

The giant's new leap strike lands on the nearest survivor from the first seconds of a match, and the four survivors start together. The number of matches that end within 200 seconds (the whole team falling in the first minutes) rose from 12 to 37 of 360 when it replaced the old Q, and the harness shows the link: with the leap strike held back for the first 20 seconds, 15 of 120 early ends fall to 2. The survivors need the opening minute to spread out and level up; a skill that targets a survivor directly should not be ready at once.

## What Changes

- A boss skill can start the match on cooldown: a new data value, the opening cooldown, is the cooldown the skill starts a match with (0, the default, starts it ready as before). Only the giant's leap strike has one: 20 seconds. The other skills of every kind start ready as before.
- The boss player's screen already shows each skill's cooldown, so the opening cooldown shows as the seconds until the skill is ready.
- With the leap strike held back the survivors win more (71.7% in the harness against 64.2%), so the giant's health is raised from 230000 to 236000 to keep the gate (61.7% on 120 matches).
- The balance gate stays: 35-70% survivor wins for the standard, giant and sniper boss on 360 matches each, and the share of matches under 200 seconds is reported.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `boss-skills`: a skill can start a match on cooldown (the giant's leap strike does), so a new match no longer starts with every skill ready.

## Impact

- `BossSkillDef` (`OpeningCooldownSeconds`), `BossSkillSim` (the starting cooldowns), `BossCatalog` (the giant's leap strike and the giant's health), tests that assume the giant's Q is ready at the start, README, `tools/harness/leap-delay/`.
