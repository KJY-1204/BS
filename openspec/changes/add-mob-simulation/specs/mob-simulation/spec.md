# Spec Delta

## Purpose

Defines the server-owned simulation of enemy mobs: how they appear, how they move toward survivors, how many can exist at once, and the point where later systems can redirect them.

## ADDED Requirements

### Requirement: Server owns all mob state
The server SHALL be the only place where mobs are created, moved and removed. Clients SHALL NOT be able to create, move or remove a mob.

#### Scenario: Client tries to change a mob
- **WHEN** a client attempts to spawn, move or remove a mob
- **THEN** the server ignores the attempt and the mob state is unchanged

### Requirement: Mobs spawn automatically
While a room is running with at least one survivor alive, the server SHALL spawn mobs at a steady rate at positions on the arena edge, without any player command.

#### Scenario: Steady spawning
- **WHEN** a room is running and the mob count is below the target
- **THEN** new mobs appear at the arena edge at the configured spawn rate until the target count is reached

#### Scenario: No survivors
- **WHEN** no survivor is in the room
- **THEN** no new mobs are spawned

### Requirement: Mob count is capped
The system SHALL limit the number of simultaneous mobs to a hard cap, and SHALL spawn automatically only up to a smaller design target. Both values SHALL be adjustable without changing behavior elsewhere.

#### Scenario: Target reached
- **WHEN** the mob count equals the design target
- **THEN** automatic spawning pauses until a mob is removed

#### Scenario: Spawn beyond the cap requested
- **WHEN** a spawn is requested while the mob count equals the hard cap
- **THEN** the spawn is refused and the mob count stays at the cap

### Requirement: Mobs chase the nearest survivor
Each mob SHALL move in a straight line toward the nearest living survivor, at the mob's speed. The boss character SHALL NOT be chosen as a default target.

#### Scenario: Single survivor
- **WHEN** one survivor is in the room
- **THEN** every mob moves toward that survivor

#### Scenario: Nearest changes
- **WHEN** another survivor becomes closer to a mob than its current target
- **THEN** the mob switches to the closer survivor

### Requirement: Mobs do not stack
The server SHALL push overlapping mobs apart so that mobs chasing the same target spread out instead of occupying the same position.

#### Scenario: Group chasing one survivor
- **WHEN** twenty mobs chase the same survivor
- **THEN** after a short time no two mobs are at the same position

### Requirement: Mobs stay inside the arena
The server SHALL keep every mob inside the arena bounds.

#### Scenario: Survivor at the edge
- **WHEN** a survivor stands at the arena edge and mobs approach
- **THEN** no mob moves outside the arena

### Requirement: A mob's target can be overridden by the server
The server SHALL provide a way to assign a mob a specific target, either a player or a position, that replaces nearest-survivor chasing until the assignment is cleared.

#### Scenario: Override to a player
- **WHEN** a mob is assigned a specific survivor as target
- **THEN** the mob moves toward that survivor even if another survivor is nearer

#### Scenario: Override cleared
- **WHEN** the assignment is cleared
- **THEN** the mob returns to chasing the nearest survivor

#### Scenario: Override target leaves
- **WHEN** an assigned player disconnects
- **THEN** the assignment is cleared and the mob returns to chasing the nearest survivor

### Requirement: Mobs end with the room
The system SHALL remove all mobs when the room ends and SHALL start a new room with no mobs.

#### Scenario: Host closes the room
- **WHEN** the host leaves and the room ends
- **THEN** all mobs are removed on every remaining client
