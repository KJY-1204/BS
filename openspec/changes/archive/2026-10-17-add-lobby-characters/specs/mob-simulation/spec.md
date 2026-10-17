# Spec Delta

## ADDED Requirements

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
