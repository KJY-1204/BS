# player-movement-sync Specification

## Purpose

Defines how player characters are spawned and moved: each player sends input, the server decides the resulting position, and every client sees the same positions.

## Requirements

### Requirement: Character spawns per player
The system SHALL spawn one placeholder character for each connected player, owned by that player and visible to all clients.

#### Scenario: Player joins
- **WHEN** a player finishes connecting to a room
- **THEN** a character for that player appears on every client at the spawn position of the player's role

### Requirement: Movement is server-authoritative
The server SHALL compute each character's position from the owning player's input, and clients SHALL NOT be able to set a character's position directly.

#### Scenario: Normal movement
- **WHEN** a player holds a movement direction
- **THEN** the character moves in that direction at the server-defined speed on every client

#### Scenario: Tampered client position
- **WHEN** a client claims a position that differs from the server's result
- **THEN** the server position is kept and the client is corrected to it

### Requirement: Movement is smooth for all players
The system SHALL present other players' characters moving without visible teleporting under normal network conditions.

#### Scenario: Remote character view
- **WHEN** a character moves continuously on a connection with typical latency under 100 ms
- **THEN** other clients see it move along a continuous path without jumps

#### Scenario: Local character responsiveness
- **WHEN** a player presses a movement key
- **THEN** the player's own character starts moving on their screen without waiting a full round trip

### Requirement: Movement stays inside the arena
The system SHALL keep every character inside the arena bounds.

#### Scenario: Pushing against the edge
- **WHEN** a player keeps moving toward the arena edge
- **THEN** the character stops at the boundary and does not leave the arena
