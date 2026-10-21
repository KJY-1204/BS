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
Clients SHALL draw a projectile's flight as a straight line at the speed of the shooter's character from its start position and time, without receiving positions during flight. The drawn position SHALL stay within 0.5 world units of the server's position for the same time. A client SHALL know each shooter's character from the room, not from the event.

#### Scenario: Matching positions
- **WHEN** a projectile has flown for part of its lifetime
- **THEN** each client draws it within 0.5 world units of where the server has it for the same moment

#### Scenario: Different shooters
- **WHEN** a fast projectile and a slow projectile are in flight
- **THEN** each client draws each at its own character's speed

### Requirement: Shots line up with the mobs on screen
A client SHALL start, move and end a projectile on the same display clock it uses for mobs, so a projectile reaches a mob on screen at the moment the server registered the hit.

#### Scenario: Hit on screen
- **WHEN** a projectile hits a mob on the server
- **THEN** on every client the projectile ends at the mob's displayed position at that moment, and the mob disappears or is marked hit at that same moment

### Requirement: Hits and endings are reliable
The server SHALL tell clients reliably when a projectile hits a mob, the boss or a survivor, and when it ends, so a projectile is never left on screen after it ended. A hit that stops the projectile SHALL end it on every client at the hit position. A hit by a piercing projectile that continues SHALL be marked as such and SHALL NOT end the projectile; such a projectile SHALL end with its last hit or with an ending event. A hit event SHALL say whether its target was a survivor, so a client can tell a hit on a survivor from a hit on a mob.

#### Scenario: Hit event
- **WHEN** a projectile hits a mob and stops
- **THEN** every client removes the projectile at the hit position and shows a short hit effect

#### Scenario: Piercing hit
- **WHEN** a piercing projectile hits a mob and keeps flying
- **THEN** every client shows the hit effect at that moment and the projectile keeps flying on screen

#### Scenario: Hit on a survivor
- **WHEN** a boss projectile hits a survivor
- **THEN** every client removes the projectile at the hit position, shows a hit effect there and treats the target as a survivor, not as a mob

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
With four survivors firing continuously, the boss firing continuously at its fastest rate and using both of its skills whenever they are ready, 100 mobs alive and every ranged mob shooting at its own rate, the combat events sent to each client SHALL average no more than 10 KB per second, including the blasts of exploding mobs and the boss's shockwaves.

#### Scenario: Full fire
- **WHEN** four survivors and the boss hold fire for one minute with 100 mobs alive, among them ranged and exploding mobs, and the boss uses its skills as often as it can
- **THEN** the measured combat traffic to each client averages no more than 10 KB per second

### Requirement: Attack display does not affect the simulation
Showing, hiding or failing to show projectiles on a client SHALL NOT change the server's projectile or mob state.

#### Scenario: Client stops rendering
- **WHEN** a client stops displaying projectiles
- **THEN** the server's simulation and the other clients are unaffected

### Requirement: Skill events share the combat event stream
Skill activations SHALL travel as combat events of their own kind in the same reliable stream as shots and hits, carrying the skill, the survivor's slot, the place, the aim direction and the rank, and the same server time, so every effect is ordered with the shots and kills around it.

#### Scenario: Ordering
- **WHEN** a survivor fires a shot and then a skill in the same step
- **THEN** every client receives the events in that order

#### Scenario: Direction carried
- **WHEN** a survivor fires a skill that has a direction
- **THEN** every client receives the direction the server used

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

### Requirement: Everyone sees the shots of mobs
Every player SHALL see every projectile a mob fires, drawn by the same replay rule as other projectiles from the fire event alone, with the mob shot's own shape and a color unlike a survivor's or the boss's shots. A hit of a mob's shot on a survivor SHALL end the shot on every client at the hit position with a hit effect, as a boss shot does.

#### Scenario: A mob fires
- **WHEN** a ranged mob fires
- **THEN** every client shows the shot leaving the mob and flying along its aim in the mob shot's color

#### Scenario: A mob's shot hits
- **WHEN** a mob's shot hits a survivor
- **THEN** every client removes the shot at the hit position and shows a hit effect there

### Requirement: Blasts are reliable and drawn at the display time
The server SHALL tell clients reliably, in the same stream as the other combat events, when an exploding mob blasts, with where and how far. Every client SHALL draw the blast as a short placeholder effect of that size at the position when the display time of the event comes, so the effect appears at the moment the exploding mob disappears from the screen. The effect SHALL NOT affect the simulation.

#### Scenario: A blast is seen
- **WHEN** an exploding mob blasts
- **THEN** every client shows a short effect of the blast radius at the mob's position as the mob disappears

#### Scenario: Blast message size
- **WHEN** blasts happen
- **THEN** each blast uses a small fixed number of bytes in the combat message and no new kind of network message is needed

#### Scenario: Display only
- **WHEN** a client fails to show a blast effect
- **THEN** the server's survivors, mobs and projectiles are unchanged
