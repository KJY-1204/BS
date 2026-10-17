# mob-simulation Specification

## Purpose

Defines the server-owned simulation of enemy mobs: how they appear, how they move toward survivors, how many can exist at once, and the point where later systems can redirect them.

## Requirements

### Requirement: Server owns all mob state
The server SHALL be the only place where mobs are created, moved and removed. Clients SHALL NOT be able to create, move or remove a mob.

#### Scenario: Client tries to change a mob
- **WHEN** a client attempts to spawn, move or remove a mob
- **THEN** the server ignores the attempt and the mob state is unchanged

### Requirement: Mobs spawn automatically
While a room is running with at least one survivor alive, the server SHALL spawn mobs at a steady rate at positions on the arena edge, without any player command. When the match has a boss, the automatic spawn rate and the automatic target SHALL be lower than in a match without a boss, but SHALL NOT be zero. The values SHALL be adjustable without changing behavior elsewhere.

#### Scenario: Steady spawning
- **WHEN** a room is running and the automatic mob count is below the automatic target
- **THEN** new mobs appear at the arena edge at the configured spawn rate until the automatic target is reached

#### Scenario: No survivors
- **WHEN** no survivor is in the room
- **THEN** no new mobs are spawned

#### Scenario: A match with a boss
- **WHEN** the match has a boss and the boss does nothing
- **THEN** mobs still appear on their own, but more slowly and in smaller numbers than in a match without a boss

#### Scenario: A match without a boss
- **WHEN** the match has no boss
- **THEN** mobs spawn at the same rate and target as before this change

### Requirement: Mob count is capped
The system SHALL limit the number of simultaneous mobs to a hard cap shared by automatically spawned and summoned mobs, and SHALL spawn automatically only up to a smaller design target that counts automatically spawned mobs only. Both values SHALL be adjustable without changing behavior elsewhere.

#### Scenario: Target reached
- **WHEN** the number of automatically spawned mobs equals the design target
- **THEN** automatic spawning pauses until one of them is removed, even if summoned mobs are also on the field

#### Scenario: Spawn beyond the cap requested
- **WHEN** a spawn is requested while the mob count equals the hard cap
- **THEN** the spawn is refused and the mob count stays at the cap

#### Scenario: Summoned mobs take room
- **WHEN** summoned and automatic mobs together reach the hard cap
- **THEN** neither a summon nor an automatic spawn creates a mob until one is removed

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

### Requirement: A mob's target can be overridden by the server
The server SHALL provide a way to assign a mob a specific target, either a player or a position, that replaces nearest-survivor chasing until the assignment is cleared. A mob assigned a position SHALL return to chasing the nearest survivor once it reaches that position.

#### Scenario: Override to a player
- **WHEN** a mob is assigned a specific survivor as target
- **THEN** the mob moves toward that survivor even if another survivor is nearer

#### Scenario: Override to a position
- **WHEN** a mob is assigned a position
- **THEN** the mob moves toward that position even if a survivor is nearer

#### Scenario: Position reached
- **WHEN** a mob assigned a position reaches it
- **THEN** the assignment ends and the mob chases the nearest survivor

#### Scenario: Override cleared
- **WHEN** the assignment is cleared
- **THEN** the mob returns to chasing the nearest survivor

#### Scenario: Override target leaves
- **WHEN** an assigned player disconnects
- **THEN** the assignment is cleared and the mob returns to chasing the nearest survivor

#### Scenario: Override target is down
- **WHEN** an assigned survivor is knocked down
- **THEN** the mob chases the nearest living survivor until the assigned survivor is revived, and then returns to the assigned survivor if the assignment still stands

### Requirement: Mobs end with the room
The system SHALL remove all mobs when the room ends and SHALL start a new room with no mobs.

#### Scenario: Host closes the room
- **WHEN** the host leaves and the room ends
- **THEN** all mobs are removed on every remaining client

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

### Requirement: Damage can hit every mob in a circle
The server SHALL be able to deal damage to every mob within a given radius of a point at once. Each affected mob SHALL be handled like any other damaged mob, including being removed at zero health, and the server SHALL report where each mob died.

#### Scenario: Mobs inside and outside
- **WHEN** area damage is dealt with three mobs inside the radius and one outside
- **THEN** the three take the damage and the one outside is unchanged

#### Scenario: Kills are reported
- **WHEN** area damage kills two mobs
- **THEN** both are removed and their death positions are reported to the caller

#### Scenario: Empty area
- **WHEN** no mob is within the radius
- **THEN** nothing changes

### Requirement: Mobs exist only while playing
The server SHALL spawn and move mobs only while the room is playing, SHALL remove every mob when the room returns to waiting, and SHALL start the next match with no mobs.

#### Scenario: Waiting room has no mobs
- **WHEN** players are in a waiting room
- **THEN** no mob exists on the server or on any client

#### Scenario: Return to waiting
- **WHEN** the host ends a match with mobs on the field
- **THEN** every mob is removed on the server and on every client

#### Scenario: Next match
- **WHEN** a match starts after an earlier one ended
- **THEN** the mob count starts from zero and spawning begins again

### Requirement: A shot can find every mob on its path
For a piercing projectile the server SHALL find every mob the projectile touched in a step in order from the nearest, excluding mobs that projectile already hit, so no mob is hit twice by the same projectile.

#### Scenario: Several in one step
- **WHEN** a fast piercing projectile crosses three mobs in one step
- **THEN** all three are found, nearest first

#### Scenario: Already hit
- **WHEN** a mob that a projectile already hit is still on its path
- **THEN** it is not found again for that projectile

### Requirement: Damage can hit every mob in a line
The server SHALL be able to damage every mob within a width of a straight line from a point along a direction up to a length, and SHALL report the places where mobs died so they can give experience.

#### Scenario: On the line
- **WHEN** a line of length 10 and half width 0.6 is applied with mobs on it, beside it and beyond its end
- **THEN** only the mobs inside the length and the width take damage

#### Scenario: Empty line
- **WHEN** no mob is on the line
- **THEN** nothing happens

### Requirement: All mobs can be stopped for a time
The server SHALL be able to stop every mob for a time: while stopped, mobs do not move, mobs do not spawn and mobs do not hurt survivors on contact, yet they can still take damage and die. When the time ends, everything resumes. A stop that is already running SHALL NOT be shortened by a shorter one.

#### Scenario: Stopped
- **WHEN** the mobs are stopped for two seconds
- **THEN** no mob changes position and no mob spawns for two seconds

#### Scenario: Shorter stop while stopped
- **WHEN** a one-second stop is applied while three seconds remain
- **THEN** three seconds remain

#### Scenario: Resume
- **WHEN** the stop ends
- **THEN** mobs chase survivors again and spawning resumes

### Requirement: Mobs can be pushed away from a point
The server SHALL be able to push every mob within a radius of a point directly away from it by a distance, keeping the mob inside the arena and not changing its health.

#### Scenario: Pushed
- **WHEN** a mob 1 unit from the point is pushed by 3 units
- **THEN** it ends 4 units from the point on the same line, or at the arena edge if that is nearer

#### Scenario: Out of range
- **WHEN** a mob stands outside the radius
- **THEN** it does not move

#### Scenario: On the point
- **WHEN** a mob stands exactly on the point
- **THEN** it is pushed in some direction without error

### Requirement: Mob identifiers never collide with the boss
Mob identifiers SHALL never use the value reserved for the boss, so a hit on the boss can be told from a hit on any mob by its identifier, however many mobs have spawned and however the identifier counter wraps around.

#### Scenario: A long match
- **WHEN** enough mobs have spawned for the identifier counter to wrap around
- **THEN** no mob ever receives the reserved boss identifier

#### Scenario: The reserved value is skipped
- **WHEN** the next free identifier would be the reserved one
- **THEN** the mob receives the following free identifier instead

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
