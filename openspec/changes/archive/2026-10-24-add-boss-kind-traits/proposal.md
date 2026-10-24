# Proposal

## Why

The three boss kinds differ in numbers and skills, but they play alike: the boss player chases survivors and presses the same buttons. A kind should also ask for a different way of playing. The smallest way to get that, without a new system, is one trait per kind that rewards how the boss moves: standing still to aim, or keeping on the move.

## What Changes

- The server knows how long the boss has stood still and whether it is moving, from the positions the boss player sends.
- Each kind can have a trait as data. The sniper's trait is Steady: after it has stood still for one second its basic shots deal 60% more damage, until it moves again. The giant's trait is Momentum: while it is moving its contact damage is 30% higher, and while it stands still 30% lower. The standard kind has no trait.
- The trait is applied by the server only (the damage of a shot when it leaves, the damage of a contact when it is dealt). Clients need no new data and the wire format does not change.
- The harness gets an override `bossTraits` (1 is the default, 0 turns the traits off) so the effect on the survivors' win share can be measured against the same build without traits.
- The numbers are kept inside the existing pacing gate (35-70% survivor wins, median match 480-840 s) for the standard, giant and sniper boss.

## Capabilities

### New Capabilities
- `boss-kind-traits`: the boss's stillness and movement as server state, the trait data of a kind, the sniper's Steady and the giant's Momentum, and the switch that turns the traits off.

### Modified Capabilities
<!-- none: the kinds, the boss attack and the contact damage keep their requirements; the trait only scales the damage dealt -->

## Impact

- `BossDef` (trait fields), `BossCatalog` (values), `BossSim` (stillness tracking and the current contact damage), `SurvivorSim` (uses the current contact damage), `MatchWorld` (steps the boss's stillness and scales the shot damage), `HarnessSettings` (`bossTraits`), tests, README, `tools/harness/traits/`.
- No wire format, scene or prefab change.
