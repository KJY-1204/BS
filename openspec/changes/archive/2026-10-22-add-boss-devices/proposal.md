# Proposal

## Why

The boss's only way to keep pressure on the survivors while it fights is to spend summon energy by hand. Survivors have nothing on the field to attack except mobs and the boss, so there is no choice between chasing the boss, clearing mobs and dealing with something the boss prepared. A device the boss places and the survivors can break gives the boss a decision about where and when to invest energy, and gives the survivors a target worth a detour. This follows the boss kinds, which gave the boss player more choices; the survivor side and the boss's field presence are the next thin spots.

## What Changes

- The boss player can place a device at the boss's position with a new key (R). Placing costs summon energy, has its own cooldown, and is refused when the room is not playing, the boss has fallen, the energy is too low, the cooldown has not passed, the mob hard cap is reached, or the boss already has the maximum number of devices standing.
- A device stands still and has health. At a fixed interval it creates a small group of basic summoned mobs around itself, under the existing mob hard cap and the existing safe distance from living survivors. Those mobs cost no energy, and they obey the boss's standing order like any other summoned mob.
- A device disappears by itself after a fixed lifetime. It also disappears when the match ends or the room returns to waiting.
- Survivors' projectiles and every skill that damages mobs also damage a device. Skills that stop mobs or push them away, the heal and the barrier do not affect it. A device does not hurt survivors by touch.
- A device that is destroyed gives experience to the nearby living survivors like a killed mob, with a larger base amount than one mob. A device that expires by itself gives none.
- Every client shows each device (position, kind, health bar over it) and sees it appear and disappear, including late joiners. The boss player sees its device count and the R key hint; survivors do not see the boss's energy or cooldown.
- A device is its own kind of thing on the field, not one of the mob kinds: it never moves, does not hurt by touch and cannot be summoned by hand, so the mob kind rules (rewards, orders, the keys 1 to 5) stay as they are. It counts as one toward the mob hard cap. It is shown with the existing mob appearance message and snapshots, so no new kind of network message is needed to see it.
- Balance of the new numbers is left to the balance pass planned right after this change.

## Capabilities

### New Capabilities
- `boss-devices`: the device as a thing on the field: placing it, its health, lifetime and mob production, how survivors destroy it, what destroying it gives, and what everyone sees of it.

### Modified Capabilities
- `survivor-leveling`: experience also comes from destroying a device (added requirement), with its own base amount and none when it expires by itself.

## Impact

- Code: `MobKinds`/`MobSim` (a stationary entry with health, a production timer and a lifetime, left out of freeze, push, orders and contact damage), `SummonSim`/`SummonServer` (place request, energy, cooldown, device limit), `PlayerSummon` and `SummonHud` (R key, count, hint, the summon-kind keys must not include the device), `MobView` (look and health bar), `LevelingSim`/`GrowthServer` (device experience, none on expiry), `CombatServer`/`ProjectileSim`/`SkillSim` (devices are hit as mobs; ordering of hits so they stay consistent).
- Network: one new boss-to-server request for placing a device; no new snapshot or event message type.
- Tests: EditMode tests for the placing rules, production, lifetime, hit rules, experience and the late joiner state; regression scripts for the multiplayer view.
- Out of scope: new art (placeholder shapes are used), balance tuning, boss-kind-specific devices, devices that move or shoot.
