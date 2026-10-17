# attack-replication Specification

## Purpose

Defines how attacks are shown to every player: each shot appears and flies the same way on every screen, hits line up with what players see of the mobs, hit mobs show their health briefly, and the traffic stays small.

## Requirements

### Requirement: Every player sees every shot
Every client SHALL show each projectile that the server fires, starting at the shooter's position and flying in the same direction.

#### Scenario: A teammate fires
- **WHEN** one survivor fires a shot
- **THEN** every other client shows a projectile leaving that survivor in the firing direction

### Requirement: Flight is replayed by rule
Clients SHALL draw a projectile's flight as a straight line at the projectile's fixed speed from its start position and time, without receiving positions during flight. The drawn position SHALL stay within 0.5 world units of the server's position for the same time.

#### Scenario: Matching positions
- **WHEN** a projectile has flown for part of its lifetime
- **THEN** each client draws it within 0.5 world units of where the server has it for the same moment

### Requirement: Shots line up with the mobs on screen
A client SHALL start, move and end a projectile on the same display clock it uses for mobs, so a projectile reaches a mob on screen at the moment the server registered the hit.

#### Scenario: Hit on screen
- **WHEN** a projectile hits a mob on the server
- **THEN** on every client the projectile ends at the mob's displayed position at that moment, and the mob disappears or is marked hit at that same moment

### Requirement: Hits and endings are reliable
The server SHALL tell clients reliably when a projectile hits a mob and when it ends without a hit, so a projectile is never left on screen after it ended.

#### Scenario: Hit event
- **WHEN** a projectile hits a mob
- **THEN** every client removes the projectile at the hit position and shows a short hit effect

#### Scenario: Lifetime end
- **WHEN** a projectile ends without hitting anything
- **THEN** every client removes it

#### Scenario: Missing end event
- **WHEN** a client has displayed a projectile for longer than its lifetime plus a margin
- **THEN** the client removes it

### Requirement: Hit mobs show their health
When a mob is hit but not killed, clients SHALL show a small health bar over it for about 1.5 seconds, showing the mob's remaining health.

#### Scenario: Wounded mob
- **WHEN** a mob with 30 health takes 10 damage
- **THEN** a bar showing two thirds appears over it for about 1.5 seconds

### Requirement: Combat traffic stays within budget
With four survivors firing continuously and 100 mobs alive, the combat events sent to each client SHALL average no more than 10 KB per second.

#### Scenario: Full fire
- **WHEN** four survivors hold fire for one minute with 100 mobs alive
- **THEN** the measured combat traffic to each client averages no more than 10 KB per second

### Requirement: Attack display does not affect the simulation
Showing, hiding or failing to show projectiles on a client SHALL NOT change the server's projectile or mob state.

#### Scenario: Client stops rendering
- **WHEN** a client stops displaying projectiles
- **THEN** the server's simulation and the other clients are unaffected

### Requirement: Skill events share the combat event stream
Skill activations SHALL travel as combat events of their own kind in the same reliable stream as shots and hits, carrying the skill, the survivor's slot, the place and the rank, and the same server time, so every effect is ordered with the shots and kills around it.

#### Scenario: Ordering
- **WHEN** a survivor fires a shot and then a skill in the same step
- **THEN** every client receives the events in that order

### Requirement: Skill effects are drawn at the display time
A client SHALL show a skill effect when its display clock reaches the server time stamped on the event, not when the event arrives.

#### Scenario: Early arrival
- **WHEN** a skill event arrives before the display clock reaches its time
- **THEN** the effect starts only when the display clock reaches that time

#### Scenario: Late arrival
- **WHEN** a skill event arrives after the display clock has passed its time
- **THEN** the effect starts at once

### Requirement: Skill effects do not affect the simulation
Showing or failing to show a skill effect on a client SHALL NOT change the server's state.

#### Scenario: Client stops rendering
- **WHEN** a client stops drawing effects
- **THEN** the server and the other clients are unaffected
