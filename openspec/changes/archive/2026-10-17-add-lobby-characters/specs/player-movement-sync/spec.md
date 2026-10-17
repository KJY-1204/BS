# Spec Delta

## MODIFIED Requirements

### Requirement: Character spawns per player
The system SHALL spawn one placeholder character for each player when the room starts playing, owned by that player and visible to all clients. A player who is connected to a waiting room SHALL NOT have a character until the match starts.

#### Scenario: Player joins
- **WHEN** a player finishes connecting to a waiting room and the host later starts the match
- **THEN** a character for that player appears on every client at the spawn position of the player's role, and not before the match starts

#### Scenario: Waiting room
- **WHEN** a player is connected to a waiting room
- **THEN** that player has no character in the world yet

## ADDED Requirements

### Requirement: Movement speed comes from the character
The server SHALL move each character at the movement speed of that survivor's character data, and the owner's local movement SHALL use the same speed. The boss SHALL keep a fixed default speed until boss characters are defined.

#### Scenario: Two speeds
- **WHEN** a faster and a slower character both hold a direction for one second
- **THEN** the faster one has moved farther, on the server and on every client
