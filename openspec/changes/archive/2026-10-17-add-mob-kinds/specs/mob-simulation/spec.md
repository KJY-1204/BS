# Spec Delta

## MODIFIED Requirements

### Requirement: Mobs chase the nearest survivor
Each mob SHALL move in a straight line toward the nearest living survivor, at its kind's speed. The boss character SHALL NOT be chosen as a default target.

#### Scenario: Single survivor
- **WHEN** one survivor is in the room
- **THEN** every mob moves toward that survivor

#### Scenario: Nearest changes
- **WHEN** another survivor becomes closer to a mob than its current target
- **THEN** the mob switches to the closer survivor

#### Scenario: Kinds move at their own speeds
- **WHEN** mobs of different kinds chase the same survivor
- **THEN** each moves at the speed of its own kind

### Requirement: Mobs do not stack
The server SHALL push overlapping mobs apart so that mobs chasing the same target spread out instead of occupying the same position. Two mobs SHALL count as overlapping when their centers are closer than the sum of their body radii, so mobs of different sizes keep different distances.

#### Scenario: Group chasing one survivor
- **WHEN** twenty mobs chase the same survivor
- **THEN** after a short time no two mobs are at the same position

#### Scenario: Different sizes
- **WHEN** a big mob and a small mob overlap
- **THEN** they are pushed apart until their centers are at least the sum of their radii apart

### Requirement: Mobs stay inside the arena
The server SHALL keep every mob inside the arena bounds, using the mob's own body radius, so a bigger mob stops farther from the edge.

#### Scenario: Survivor at the edge
- **WHEN** a survivor stands at the arena edge and mobs approach
- **THEN** no mob moves outside the arena

#### Scenario: A big mob at the edge
- **WHEN** a big mob is pushed toward the arena edge
- **THEN** its whole body stays inside the arena

### Requirement: Mobs have health and can be killed
Every mob SHALL have health with a maximum given by its kind. The server SHALL reduce a mob's health by the damage it is given, SHALL report the health left as a percentage of that mob's own maximum, and SHALL remove the mob when its health reaches zero.

#### Scenario: Damage
- **WHEN** a mob with 30 health takes 10 damage
- **THEN** the mob stays alive with 20 health

#### Scenario: Kill
- **WHEN** a mob's health reaches zero
- **THEN** the server removes the mob and every client stops displaying it

#### Scenario: Damage to an unknown mob
- **WHEN** damage is applied to a mob that no longer exists
- **THEN** nothing changes

#### Scenario: A tougher kind
- **WHEN** a mob whose kind has three times the basic health takes damage equal to the basic health
- **THEN** it stays alive and its health percentage is about two thirds

### Requirement: Mobs hurt survivors on contact
A mob that touches a living survivor, by the sizes of both bodies, SHALL deal its kind's contact damage to that survivor. The survivor's protection time after a hit SHALL limit how often damage is dealt, and dead survivors SHALL NOT be damaged. When several mobs touch the survivor at once, one hit SHALL be dealt for that protection time, with the highest contact damage among the mobs touching.

#### Scenario: Touching a survivor
- **WHEN** a mob reaches a living survivor who is not protected
- **THEN** the survivor takes that mob's contact damage

#### Scenario: A crowd
- **WHEN** many mobs touch the same survivor at once
- **THEN** the survivor takes the contact damage once per protection time, not once per mob

#### Scenario: A mixed crowd
- **WHEN** a basic mob and a tougher mob with a higher contact damage touch the survivor at once
- **THEN** the survivor takes the higher damage once

#### Scenario: Dead survivor
- **WHEN** a mob touches a dead survivor
- **THEN** no damage is dealt

### Requirement: Every mob has an origin
The server SHALL know for each mob whether it appeared by automatic spawning or by a summon, SHALL keep that for the mob's whole life, and SHALL keep it apart from the mob's kind. Rules that apply only to summoned mobs (orders) or only to automatic mobs (the automatic target) SHALL use this origin, whatever the kind.

#### Scenario: Counting
- **WHEN** three automatic mobs and five summoned mobs are on the field
- **THEN** the automatic count is three and the summoned count is five

#### Scenario: Removal
- **WHEN** a mob of either origin is killed
- **THEN** it is removed like any other mob and the counts drop for its origin only

#### Scenario: Origin and kind are independent
- **WHEN** a summoned mob of the basic kind and a summoned mob of another kind are on the field
- **THEN** both count as summoned and both obey the boss's order
