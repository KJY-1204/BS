# Design

## Context

The server steps one `MatchWorld` (plain C#) for both the harness and the network host. `MobSim.Chase` moves mobs by `SpeedOf(kind) * dt`; mob positions already reach the clients as snapshots, so a server-side slow needs no client change. `SurvivorSim.Heal(clientId, amount)` exists. `RoleTable` replicates the boss phase with a `NetworkVariable`; the test assembly has no Netcode reference, so rules live in plain classes. The arena is 32 x 20 units.

## Goals / Non-Goals

**Goals:** one team objective with a schedule, a capture rule, a modest heal and a zone slow; server-owned; measurable on and off in the harness.
**Non-Goals:** boss counterplay to the point, experience rewards, several points at once, reviving, any change to mob kinds.

## Decisions

1. **`RescuePointSim` (pure):** state Closed / Open with `Center`, `Progress` (0..1), `OpenSeconds`, `NextOpenAt`, `Captured` count. `Step(dt, time, survivor positions, alive flags)` returns whether a capture happened this step. Constants in `RescueConfig` (first 90 s, gap 120 s, life 45 s, radius 2.5, capture 12 s, heal 0.5, slow 0.6, min distance 12).
2. **Place:** chosen when it opens from a `System.Random` seeded by the match seed and the point's index, rejection sampling inside the arena shrunk by the radius until the centre is 12 units from the boss start (a fixed number of tries, then the farthest corner). Same seed, same points.
3. **Capture rate:** living survivors inside divided by `CaptureSeconds`, per second. No decay. Four survivors take 3 s, as the proposal says.
4. **Heal:** at capture `SurvivorSim.Heal(id, MaxHealthOf(id) / 2)` for living survivors inside. Fallen survivors are skipped; revival has its own rule.
5. **Slow:** `MobSim` gets an optional speed zone (centre, radius, factor) set by the world while a point is open and cleared when it closes; `Chase` multiplies the step by the factor when the mob is inside. Devices already skip `Chase`. The boss is not a mob.
6. **Replication:** `RoleTable` replicates open (byte), the centre (`Vector2`), progress (byte, 0..100) and seconds left (byte). Clients only draw. Alternative: derive the schedule on clients from the seed and time: rejected, progress depends on positions.
7. **Drawing:** a world circle drawn like the existing devices and a bar in `MatchHud`. The layout is checked with a windowed capture.
8. **Bots:** a survivor bot with no mob or shot within its danger range and with a point open walks to the point and stands in it. This bot change is what the measurement needs; it is made in `SurvivorBot` only.
9. **Switch:** the `rescuePoints` override sets `world.RescuePointsEnabled`.
10. **Acceptance:** 120 matches per kind (standard, giant, sniper), points on and off, same seeds. With points on, survivor wins stay in 35-70% and the median match in 480-840 s; the difference is reported. If a kind leaves the gate because of the points, the heal or the slow is reduced.

## Risks / Trade-offs

- Bots walking to the point stop shooting the boss, which can lower survivor wins; the heal is meant to offset it. If it does not, the bot rule or the numbers change, not the gate.
- A boss player cannot contest the point. Accepted for this change; the slow also hurts mobs the boss player summoned.
- A point near a wall may be hard to reach for survivors chased by the boss; the radius is 2.5 and the circle is kept inside the arena.
