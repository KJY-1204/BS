# Spec Delta

## ADDED Requirements

### Requirement: Mob identifiers never collide with the boss
Mob identifiers SHALL never use the value reserved for the boss, so a hit on the boss can be told from a hit on any mob by its identifier, however many mobs have spawned and however the identifier counter wraps around.

#### Scenario: A long match
- **WHEN** enough mobs have spawned for the identifier counter to wrap around
- **THEN** no mob ever receives the reserved boss identifier

#### Scenario: The reserved value is skipped
- **WHEN** the next free identifier would be the reserved one
- **THEN** the mob receives the following free identifier instead
