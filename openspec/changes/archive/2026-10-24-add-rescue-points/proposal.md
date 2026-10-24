# Proposal

## Why

The survivors have one thing to do in a match: shoot the boss and dodge. Leveling and skills deepen that, but never give a second choice of what to do next. A shared objective on the map asks the survivors to decide together between fighting and going somewhere, which is where a team game gets its rhythm.

## What Changes

- A rescue point opens on the map at fixed intervals: the first at 90 seconds, then every 120 seconds after the previous one ended. Only one is open at a time. It closes when it is captured or when 45 seconds have passed.
- A living survivor standing inside the point's circle fills its progress. One survivor needs 12 seconds; more survivors fill it faster (the speed is the number of living survivors inside, so four fill it in 3 seconds). Progress does not fall while nobody is inside.
- Mobs inside the circle move at 60% speed while the point is open. The boss is not slowed and its presence does not stop the progress.
- When the progress is full, every living survivor inside the circle is healed by 50% of their maximum health. Nobody is revived, and no experience is given. The point then closes.
- The point's place comes from the match seed, inside the arena and away from the boss's start. The server owns the state; every client sees the circle and the progress bar, the boss player included.
- The harness gets an override `rescuePoints` (1 is the default, 0 turns them off) and the survivor bots walk to an open point when nothing is near them. The effect on the survivors' win share is measured against the build without points.

## Capabilities

### New Capabilities
- `rescue-points`: the open and close schedule, the place, the capture rule, the heal, the slow on mobs, the replicated state and its drawing, and the switch.

### Modified Capabilities
<!-- none: mob speed and healing keep their requirements; the point only adds a zone-bound slow and a heal -->

## Impact

- New `RescuePointSim` (plain C#), `MobSim` (a speed zone), `MatchWorld` (steps the point), `RoleTable` (replicated state), a world and HUD drawing, `Bots` (walk to the point), `HarnessSettings` (`rescuePoints`), tests, README, `tools/harness/rescue/`.
- A new replicated state on the room table; no change to existing messages.
