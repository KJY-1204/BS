# Design

## Context

`RescuePointSim.Step(dt, active, bodies, survivors)` counts living survivors inside and raises the progress. `MatchWorld.StepCombat` calls it with the survivor bodies and already holds `Boss` (position, radius, `CanBeHit`). `RoleTable` replicates the point's open flag, centre, progress and seconds left; `MatchHud.DrawRescuePoint` draws the bar and the dotted circle.

## Goals / Non-Goals

**Goals:** a contest the boss player can do with its body only; visible to all; measurable on and off.
**Non-Goals:** a boss reward for holding the point, mobs contesting, a new boss input, any change to the heal or the slow values.

## Decisions

1. **The world tells the point:** `Step` gets a `bossInside` flag computed in `MatchWorld` as `Boss.Exists && Boss.CanBeHit && distance(Boss.Position, Center) <= Radius + Boss.Radius` (overlap, not centre-in-circle). The sim stays free of boss types.
2. **Contest rule:** with `ContestEnabled && bossInside` the survivors counted are zero for the progress; the heal at a capture is unchanged (a capture can only happen uncontested). `OpenSeconds` keeps running. `Contested` is a property of the last step.
3. **Replication:** a new `rescueContested` byte on `RoleTable`, written with the other point values. Declared before the open flag like the centre.
4. **Drawing:** the bar is orange instead of teal and `RescueBarText` appends the word "방해" when contested; a pure helper keeps it testable.
5. **Switch:** override `rescueContest` sets `RescuePoint.ContestEnabled`.
6. **Acceptance:** 120 matches per kind with the contest on and off (points on in both), same seeds; with the contest on survivor wins stay in 35-70% and the median match in 480-840 s. The harness boss bot is unchanged: it chases the nearest survivor, so it contests when survivors stand in the circle. The contest is expected to lower the survivors' win share compared to the previous measurement; if a kind drops under 35%, the radius or the capture time is eased, not the contest.

## Risks / Trade-offs

- A boss that sits in the circle makes the objective pointless for the survivors; they must pull it away. That is the intended tension, and the 45-second life bounds it.
- The giant (38% before) is the kind most likely to leave the gate downward.
