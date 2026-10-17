# Spec Delta

## ADDED Requirements

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
