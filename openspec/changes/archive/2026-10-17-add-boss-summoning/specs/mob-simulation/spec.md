# Spec Delta

## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: Every mob has an origin
The server SHALL know for each mob whether it appeared by automatic spawning or by a summon, and SHALL keep that for the mob's whole life. Rules that apply only to summoned mobs (orders) or only to automatic mobs (the automatic target) SHALL use this origin.

#### Scenario: Counting
- **WHEN** three automatic mobs and five summoned mobs are on the field
- **THEN** the automatic count is three and the summoned count is five

#### Scenario: Removal
- **WHEN** a mob of either origin is killed
- **THEN** it is removed like any other mob and the counts drop for its origin only
