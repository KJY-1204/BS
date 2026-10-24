# Proposal

## Why

The harness survivor bots dodge hostile projectiles but ignore the announcements of boss skills, although every skill is announced (0.8 to 1.2 seconds) so that a player can step out of its way. The result is a harness that over-states how deadly the boss skills are: about one match in ten of the giant and the sniper ends within a minute and a half, with all four survivors falling at once, and experiments showed that the boss skills are the main cause (with their damage at 1 the early ends fall from 23 to 6 in 120 matches). A longer opening grace for the skills did not help, and the likeliest reason is the bots, not the game: players see the announcement and move.

## What Changes

- A survivor bot steps out of an announced shockwave (away from its centre, out of its radius) and out of an announced beam (sideways, out of its line) when the announcement is on its position. It decides each time it decides where to walk, so a bot with a longer reaction time has less time left to get clear, as a player would.
- Announced roars and shot rings are not avoided by this rule: the roar makes mobs, and the ring's shots are dodged as projectiles once they fly.
- The evasion is a bot rule only: no game rule, value, message or screen changes. A development switch turns it off (for tests and for comparing with the earlier measurements).
- Because the bots get stronger, the match balance is measured again for the three boss kinds (360 matches each, new seeds). The gate stays 35-70% survivor wins and a median match of 480-840 s; boss or survivor values are changed only if a kind leaves it.

## Capabilities

### New Capabilities
- `harness-bots`: how the harness survivor bots avoid announced boss skills and how the avoidance is switched.

### Modified Capabilities
<!-- none -->

## Impact

- `BossSkillSim` (a read-only way to ask where an announced skill threatens a position), `BotMath` and `SurvivorBot` (the evasion and its switch), tests, README, `tools/harness/evasion/`. No change to the network, the scene or the wire format.
