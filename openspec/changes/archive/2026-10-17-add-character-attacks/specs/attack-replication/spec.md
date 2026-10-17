# Spec Delta

## MODIFIED Requirements

### Requirement: Flight is replayed by rule
Clients SHALL draw a projectile's flight as a straight line at the speed of the shooter's character from its start position and time, without receiving positions during flight. The drawn position SHALL stay within 0.5 world units of the server's position for the same time. A client SHALL know each shooter's character from the room, not from the event.

#### Scenario: Matching positions
- **WHEN** a projectile has flown for part of its lifetime
- **THEN** each client draws it within 0.5 world units of where the server has it for the same moment

#### Scenario: Different shooters
- **WHEN** a fast projectile and a slow projectile are in flight
- **THEN** each client draws each at its own character's speed

### Requirement: Hits and endings are reliable
The server SHALL tell clients reliably when a projectile hits a mob and when it ends, so a projectile is never left on screen after it ended. A hit that stops the projectile SHALL end it on every client at the hit position. A hit by a piercing projectile that continues SHALL be marked as such and SHALL NOT end the projectile; such a projectile SHALL end with its last hit or with an ending event.

#### Scenario: Hit event
- **WHEN** a projectile hits a mob and stops
- **THEN** every client removes the projectile at the hit position and shows a short hit effect

#### Scenario: Piercing hit
- **WHEN** a piercing projectile hits a mob and keeps flying
- **THEN** every client shows the hit effect at that moment and the projectile keeps flying on screen

#### Scenario: Lifetime end
- **WHEN** a projectile ends without hitting anything
- **THEN** every client removes it

#### Scenario: Missing end event
- **WHEN** a client has displayed a projectile for longer than its lifetime plus a margin
- **THEN** the client removes it

### Requirement: Skill events share the combat event stream
Skill activations SHALL travel as combat events of their own kind in the same reliable stream as shots and hits, carrying the skill, the survivor's slot, the place, the aim direction and the rank, and the same server time, so every effect is ordered with the shots and kills around it.

#### Scenario: Ordering
- **WHEN** a survivor fires a shot and then a skill in the same step
- **THEN** every client receives the events in that order

#### Scenario: Direction carried
- **WHEN** a survivor fires a skill that has a direction
- **THEN** every client receives the direction the server used
