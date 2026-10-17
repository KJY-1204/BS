# Spec Delta

## MODIFIED Requirements

### Requirement: The server decides when shots are fired
The server SHALL fire at most one attack per cooldown period for each survivor while that survivor's player is holding fire, in the direction of the latest aim it received, and one attack SHALL fire the number of shots its character defines. A client SHALL NOT be able to fire faster than the cooldown allows.

#### Scenario: Holding fire
- **WHEN** a player holds fire for one second and the cooldown is 0.25 seconds
- **THEN** the server fires four attacks

#### Scenario: Faster than the cooldown
- **WHEN** a client sends fire input far more often than the cooldown allows
- **THEN** the number of attacks stays the same as with normal input

#### Scenario: A multi-shot attack
- **WHEN** a character whose attack has five shots fires once
- **THEN** the server fires five shots for that one attack and starts one cooldown

### Requirement: Projectiles fly straight
A projectile SHALL start at the firing survivor's position and fly in a straight line at the speed of the shooter's character, and SHALL disappear after that character's lifetime or when it leaves the arena. Speed, lifetime and size SHALL follow the character that fired it.

#### Scenario: Open space
- **WHEN** a projectile is fired and hits nothing
- **THEN** it disappears after its lifetime and has travelled speed times lifetime in a straight line

#### Scenario: Arena edge
- **WHEN** a projectile reaches the arena boundary
- **THEN** it disappears there

#### Scenario: Different characters
- **WHEN** two survivors of different characters fire in open space
- **THEN** each projectile travels at its own character's speed for its own character's lifetime

### Requirement: A projectile hits the first mob in its path
A projectile that can hit one mob SHALL damage the first mob it touches and disappear, and SHALL NOT pass through that mob and hit another. A projectile that can hit several mobs SHALL follow the rules of piercing shots instead.

#### Scenario: Two mobs in a line
- **WHEN** a projectile that can hit one mob flies toward two mobs standing one behind the other
- **THEN** only the nearer mob takes damage and the projectile disappears

#### Scenario: Fast projectile
- **WHEN** a projectile moves farther in one simulation step than a mob's width
- **THEN** it still hits a mob that lies on its path
