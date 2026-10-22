# Design

## Context

Mobs live in `MobSim` as parallel arrays (id, type byte, health, position, order override, timer). The type byte carries the origin in bit 0 and the kind above it, and the server already tells every client a mob's type in the spawn event, in the full state for late joiners, and in the remove event. Every damage path (projectiles, area burst, beam line, knives) reaches a mob through its id and `ApplyDamage`, and a kill is reported as a death position that `GrowthServer` turns into experience. `MobSim.Step` returns at once while time is stopped, so nothing in it runs during a time stop. The boss's summon energy and cooldown live in `SummonSim`, driven by `SummonServer` and requested through RPCs on `PlayerSummon`. See proposal.md for the motivation and specs/boss-devices for the requirements.

## Goals / Non-Goals

**Goals:**
- A device the survivors can hit with every existing mob-damaging attack, seen by every client, without a new network message type.
- Reuse the mob id space, the hard cap, the spawn/remove events, the late-joiner state and the experience share.
- Keep the mob kind rules (rewards, orders, number keys) untouched.

**Non-Goals:**
- Real art, balance numbers, devices that move or shoot, device types per boss kind.
- A general "structure" system; this is one object type.

## Decisions

**1. A device is stored as an entry in `MobSim`, marked by a kind that is not one of the five mob kinds.**
Kind index 5 is added to the kind table as `Device`, but `MobKinds.Count` stays 5 and keeps meaning "the kinds of mob the boss can pick" (the number keys, the summon HUD row, the summon stats log), so those need no change; a new `IsSummonable(kind)` (kind < `Count`) replaces `IsValid` in the summon's unknown-kind check so a summon of the device kind is refused like an unknown kind. The origin bit is the summoned one so the existing type byte, spawn event, snapshot and full state carry it unchanged, and the view draws it from the type.
Alternative: a separate `DeviceSim` with its own events and snapshots. Rejected: hits from projectiles, three skills and the knives would each need a second target list, and replication would need a new message. Reusing the entry costs a handful of guards inside `MobSim` instead.
The device's stat multiples sit in the same kind data (health multiple large, speed 0, contact multiple 0, large color/size), so health and radius come from the existing `MaxHealthOf`/`RadiusOf`.

**2. Guards inside `MobSim` make a device inert where a mob would not be.**
A device is left out of: chasing (speed 0), `Separate` (mobs are not pushed off it and it is not pushed), `Push`, contact damage (`MaxContactMultipleWithin` skips it), `ApplyOrderToSummoned` and `SetTarget` (no order), the exploder/ranged behavior, and the summoned-count that orders and stats use for mobs. `Freeze` only sets the frozen timer, so the device pass runs before the early return in `Step`; the other passes stay behind it. The device keeps its health in the existing array.
Alternative: keep devices in the mob loop and accept they stop in a time stop. Rejected: the spec says a device keeps working, and a stopped device would make the Marisa/Sakuya time stop strictly better than before.

**3. Production and lifetime use the existing per-entry `timer` plus one new array for the remaining lifetime.**
`timer[i]` counts down to the next batch; a new `lifetime[i]` counts down to expiry. At zero the batch spawns by calling the same position-picking rule as the boss's summon (ring around the device, arena clamp, safe distance), moved into a helper shared with `SummonSim`, then `TrySpawn` of basic summoned mobs. The new mobs take the standing order the way summoned mobs from `SummonSim` do, so `MobSim` needs the standing order or a callback to `SummonSim`; the simpler choice is that `MobSim` keeps the last order it was given through `ApplyOrderToSummoned` (it already stores per-mob overrides) and applies it to mobs a device creates. Expiry calls `Remove` and produces no death record.
Alternative: have `SummonSim` run the devices. Rejected: it would need to iterate `MobSim` entries every step and would not run during the time stop either, since `SummonSim.Step` is only fed time while the match runs and cannot see the freeze.

**4. Placing is a new `SummonSim` operation with its own cooldown, limit and cost.**
`PlaceDevice(ctx, fromBoss)` follows `Summon`: not the boss → ignored, match not running or boss fallen → unavailable, cooldown, device limit, hard cap, energy, then the safe distance check around the boss against living survivors. The device count is read from `MobSim` (devices counted by kind), so a destroyed or expired device frees a slot with no extra bookkeeping. `SummonResult` gets new values for the device-specific refusals (limit, survivor too close). The result is sent back with the existing `ResultRpc`.
The boss-only information (device count out of the limit and the placing cooldown) goes in the boss-only stats the server already sends to the boss; no stats go to survivors. A new `PlaceDeviceRpc(Server)` on `PlayerSummon` is the only new request.

**5. Experience: the kill record carries its base amount.**
Kills reach `GrowthServer` as a list of death positions. They become (position, base amount) so a device kill carries `XpPerDevice` and a mob kill carries `XpPerKill`; `Leveling.AwardKill` takes the base amount and applies the experience passive to it as before. Expiry never enters the kill list.
Alternative: a second list of device kills. Rejected: it would duplicate the share-distance loop.

**6. Display: a standing health bar for devices only.**
`CombatView` shows a mob bar for a short time after a hit. A device gets a bar that is always drawn from the health percentage the client already receives with each hit effect, starting full at spawn. Devices are recognised on the client by the kind in the type byte. Placeholder shape: a larger square/diamond in a color not used by any mob kind.

**7. Spec boundaries.**
`boss-devices` states that a device counts as one toward the hard cap and has no rewards of a mob kind; `mob-kinds`, `mob-simulation` and `summon-kind-selection` are not modified because a device is not one of their mob kinds. The counts used by their specs (`Count` of kinds for the number keys, `AutoCount`) are kept the same by decision 1 and 2.

## Risks / Trade-offs

- [A guard is missed and a device behaves like a mob somewhere, e.g. pushed, ordered or counted for auto-spawn] → One EditMode test per spec scenario for freeze, push, orders, contact and `AutoCount`, and a test that walks every `MobSim` pass with a device present.
- [Devices count toward the hard cap, so a full field also blocks devices and a device takes a slot mobs could use] → Intended by the spec; the cap is large and the limit on devices is small.
- [Device-made mobs plus device kills form an experience loop] → Left to the balance pass; the interval, group size, lifetime and device experience are all data values.
- [A late joiner sees a damaged device with a full bar] → Accepted and written into the spec: the full state sends spawn events only, and the bar shows the true health from the next hit on the device.
- [Code that loops over the kind table sees the device] → `Count` stays 5 for the mob kinds and `IsSummonable` guards the summon; tests check that the device is not summonable and differs in color and size from every mob kind.

## Open Questions

- The first values (health, lifetime, interval, group size, limit, cost, cooldown, device experience) are starting assumptions in data and are adjusted in the balance pass that follows.
